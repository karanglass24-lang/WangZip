using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using WangZip.Core.Safety;

namespace WangZip.Core.Zip;

/// <summary>ZIP 항목 추가/삭제 결과.</summary>
public sealed record ZipModResult
{
    public int ChangedEntries { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public TimeSpan Elapsed { get; init; }
}

/// <summary>
/// 열려 있는 ZIP 파일에 항목을 추가하거나 삭제합니다.
/// 기존 항목은 압축 데이터를 그대로 복사(raw copy)하고, 바뀐 부분만 다시 써서
/// 새 파일로 원자적으로 교체합니다. 완전 재압축이 아니므로 빠릅니다.
/// </summary>
public static class ZipRewriter
{
    public static ZipModResult AddEntries(
        string zipPath,
        IReadOnlyList<string> sources,
        NameEncodingMode nameEncoding = NameEncodingMode.Utf8,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sources.Count == 0)
        {
            throw new ArchiveException("추가할 파일이나 폴더를 선택해 주세요.");
        }

        string fullPath = Path.GetFullPath(zipPath);
        var warnings = new List<string>();
        var items = ZipWriter.CollectItems(
            sources, new CreationOptions { IncludeEmptyDirectories = true }, fullPath, warnings);

        if (items.Count == 0)
        {
            throw new ArchiveException("추가할 파일이 없습니다.");
        }

        return Rewrite(fullPath, items, nameEncoding, [], progress, cancellationToken, warnings);
    }

    public static ZipModResult DeleteEntries(
        string zipPath,
        IReadOnlyCollection<int> indices,
        CancellationToken cancellationToken = default)
    {
        if (indices.Count == 0)
        {
            throw new ArchiveException("삭제할 항목을 선택해 주세요.");
        }

        return Rewrite(
            Path.GetFullPath(zipPath), [], NameEncodingMode.Utf8, indices, null, cancellationToken, []);
    }

    private static ZipModResult Rewrite(
        string zipPath,
        IReadOnlyList<FileItem> newItems,
        NameEncodingMode nameEncoding,
        IReadOnlyCollection<int> deleteIndices,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken,
        List<string> warnings)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        List<RawZipEntry> entries = ZipCentralDirectory.Read(zipPath);

        // 삭제/이름 비교를 위해 파일명을 미리 해독합니다.
        foreach (RawZipEntry entry in entries)
        {
            entry.DecodedName = ZipNameDecoder.Decode(
                entry.RawName,
                (entry.Flags & 0x800) != 0,
                entry.UnicodePathUtf8,
                NameEncodingMode.Auto,
                out _);
        }

        long fileLength = new FileInfo(zipPath).Length;
        if (fileLength >= 0xFFFFFFF0)
        {
            throw new UnsupportedArchiveException(
                "ZIP64(대용량) ZIP은 항목 추가/삭제를 아직 지원하지 않습니다.");
        }

        if (entries.Count + newItems.Count >= ushort.MaxValue)
        {
            throw new UnsupportedArchiveException("항목 수가 65,535개를 넘어 추가할 수 없습니다.");
        }

        if (entries.Any(item => (item.Flags & 0x0008) != 0))
        {
            throw new UnsupportedArchiveException(
                "데이터 디스크립터를 사용하는 ZIP은 항목 추가/삭제를 지원하지 않습니다.");
        }

        var deleteSet = new HashSet<int>(deleteIndices);
        int deletedCount = deleteSet.Count(index => index >= 0 && index < entries.Count);

        if (deleteSet.Contains(-1) || deleteSet.Any(index => index < 0 || index >= entries.Count))
        {
            throw new ArchiveException("삭제할 항목 번호가 올바르지 않습니다.");
        }

        // 새 항목 이름 중복 제거 (기존 항목과, 그리고 서로 간에)
        var existingNames = new HashSet<string>(
            entries.Select(entry => entry.DecodedName.TrimEnd('/')),
            StringComparer.OrdinalIgnoreCase);
        var wantedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fresh = new List<FileItem>();
        foreach (FileItem item in newItems)
        {
            string name = item.EntryName.TrimEnd('/');
            if (existingNames.Contains(name) || !wantedNames.Add(item.EntryName))
            {
                warnings.Add($"이름이 이미 있어 건너뜁니다: {item.EntryName}");
                continue;
            }

            fresh.Add(item);
        }

        // 복사 범위: 로컬 헤더 + 이름/확장 필드 + 압축 데이터 (데이터 디스크립터는 없다고 검증함)
        var ordered = entries.OrderBy(entry => entry.LocalHeaderOffset).ToList();
        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].LocalHeaderOffset <= ordered[i - 1].LocalHeaderOffset)
            {
                throw new ArchiveException("ZIP 항목 위치가 비정상입니다. 손상되었을 수 있습니다.");
            }
        }

        long copyBytes = 0;
        var copies = new List<(RawZipEntry Entry, long Start, int Length)>();
        Span<byte> head = stackalloc byte[30];
        foreach (RawZipEntry entry in ordered)
        {
            int localNameLength, localExtraLength;
            using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Position = entry.LocalHeaderOffset;
                fs.ReadExactly(head);
                if (BinaryPrimitives.ReadUInt32LittleEndian(head) != 0x04034B50)
                {
                    throw new ArchiveException($"로컬 헤더가 손상되었습니다: {entry.DecodedName}");
                }

                localNameLength = BinaryPrimitives.ReadUInt16LittleEndian(head.Slice(26));
                localExtraLength = BinaryPrimitives.ReadUInt16LittleEndian(head.Slice(28));
            }

            long length = 30 + localNameLength + localExtraLength + entry.CompressedSize;
            if (entry.LocalHeaderOffset + length > fileLength)
            {
                throw new ArchiveException($"항목 데이터가 파일 범위를 벗어납니다: {entry.DecodedName}");
            }

            copies.Add((entry, entry.LocalHeaderOffset, (int)length));
            copyBytes += length;
        }

        long newBytes = fresh.Where(item => !item.EntryName.EndsWith('/')).Sum(item => item.Length);
        long totalWork = copyBytes + newBytes;
        var throttle = new ProgressThrottle(progress);
        long processed = 0;

        bool useUtf8Flag = nameEncoding != NameEncodingMode.Cp949;
        Encoding? legacyEncoding = nameEncoding == NameEncodingMode.Cp949
            ? Encoding.GetEncoding(949)
            : null;

        string tempPath = Path.Combine(
            Path.GetDirectoryName(zipPath)!,
            "." + Guid.NewGuid().ToString("N") + ".wang-tmp");
        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(256 * 1024);

        try
        {
            using (var output = new FileStream(
                       PathGuard.ToExtended(tempPath), FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, bufferSize: 128 * 1024, FileOptions.SequentialScan))
            {
                var central = new List<CentralRecord>();

                using (var source = new FileStream(
                           zipPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                           bufferSize: 128 * 1024, FileOptions.SequentialScan))
                {
                    foreach (var (entry, start, length) in copies)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (deleteSet.Contains(entry.Index))
                        {
                            continue;
                        }

                        long newOffset = output.Position;
                        source.Position = start;
                        int remaining = length;
                        while (remaining > 0)
                        {
                            int read = source.Read(buffer, 0, Math.Min(remaining, buffer.Length));
                            if (read <= 0) throw new EndOfStreamException();
                            output.Write(buffer, 0, read);
                            remaining -= read;
                            processed += read;
                            throttle.Report(Math.Min(processed, totalWork), totalWork, entry.DecodedName);
                        }

                        central.Add(new CentralRecord(
                            entry.Crc, entry.Method, entry.Flags,
                            entry.ModTime, entry.ModDate,
                            Clamp(entry.UncompressedSize), Clamp(entry.CompressedSize),
                            entry.RawName, entry.Extra, newOffset));
                    }
                }

                foreach (FileItem item in fresh)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] nameBytes = EncodeName(item.EntryName, legacyEncoding);
                    ushort flags = useUtf8Flag ? (ushort)0x0800 : (ushort)0;
                    ushort modTime = DosTime(item.LastWriteTime);
                    ushort modDate = DosDate(item.LastWriteTime);

                    if (item.EntryName.EndsWith('/'))
                    {
                        long newOffset = output.Position;
                        WriteLocalEntry(output, nameBytes, flags, 0, 0, modTime, modDate, 0, []);
                        central.Add(new CentralRecord(
                            0, 0, flags, modTime, modDate, 0, 0, nameBytes, [], newOffset));
                        continue;
                    }

                    long fileOffset = output.Position;
                    uint crc;
                    byte[] payload;
                    using (var input = new FileStream(
                               PathGuard.ToExtended(item.FullPath), FileMode.Open, FileAccess.Read,
                               FileShare.Read, bufferSize: 128 * 1024, FileOptions.SequentialScan))
                    {
                        var compressed = new MemoryStream();
                        crc = 0;
                        using (var deflate = new DeflateStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                        {
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                crc = Crc32.Append(crc, buffer.AsSpan(0, read));
                                deflate.Write(buffer, 0, read);
                                processed += read;
                                throttle.Report(Math.Min(processed, totalWork), totalWork, item.EntryName);
                            }
                        }

                        payload = compressed.ToArray();
                        if (payload.Length >= item.Length)
                        {
                            input.Position = 0;
                            payload = new byte[item.Length];
                            ReadFully(input, payload);
                        }
                    }

                    ushort method = payload.Length < item.Length ? (ushort)8 : ( ushort)0;
                    WriteLocalEntry(output, nameBytes, flags, method, crc, modTime, modDate,
                        (uint)item.Length, payload);
                    central.Add(new CentralRecord(
                        crc, method, flags, modTime, modDate,
                        Clamp(item.Length), Clamp(payload.Length), nameBytes, [], fileOffset));
                }

                // 중앙 디렉터리
                long centralOffset = output.Position;
                foreach (CentralRecord record in central)
                {
                    var header = new byte[46];
                    BinaryPrimitives.WriteUInt32LittleEndian(header, 0x02014B50);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), 20);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), 20);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(8), record.Flags);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(10), record.Method);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(12), record.ModTime);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(14), record.ModDate);
                    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), record.Crc);
                    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20), record.CompressedSize);
                    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24), record.UncompressedSize);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(28), (ushort)record.RawName.Length);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(30), (ushort)record.Extra.Length);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(32), 0);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(34), 0);
                    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(36), 0);
                    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(38), 0);
                    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(42), (uint)record.NewOffset);
                    output.Write(header);
                    output.Write(record.RawName);
                    output.Write(record.Extra);
                }
                long centralSize = output.Position - centralOffset;

                Span<byte> eocd = stackalloc byte[22];
                BinaryPrimitives.WriteUInt32LittleEndian(eocd, 0x06054B50);
                BinaryPrimitives.WriteUInt16LittleEndian(eocd[4..], 0);
                BinaryPrimitives.WriteUInt16LittleEndian(eocd[6..], 0);
                BinaryPrimitives.WriteUInt16LittleEndian(eocd[8..], (ushort)central.Count);
                BinaryPrimitives.WriteUInt16LittleEndian(eocd[10..], (ushort)central.Count);
                BinaryPrimitives.WriteUInt32LittleEndian(eocd[12..], (uint)centralSize);
                BinaryPrimitives.WriteUInt32LittleEndian(eocd[16..], (uint)centralOffset);
                BinaryPrimitives.WriteUInt16LittleEndian(eocd[20..], 0);
                output.Write(eocd);
            }

            File.Move(tempPath, zipPath, overwrite: true);
            throttle.Report(totalWork, totalWork, null, force: true);

            return new ZipModResult
            {
                ChangedEntries = deletedCount + fresh.Count,
                Warnings = warnings,
                Elapsed = stopwatch.Elapsed,
            };
        }
        catch
        {
            ExtractionHelpers.TryDelete(tempPath);
            throw;
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private record CentralRecord(
        uint Crc,
        ushort Method,
        ushort Flags,
        ushort ModTime,
        ushort ModDate,
        uint UncompressedSize,
        uint CompressedSize,
        byte[] RawName,
        byte[] Extra,
        long NewOffset);

    private static uint Clamp(long value) => value > uint.MaxValue ? uint.MaxValue : (uint)value;

    private static byte[] EncodeName(string name, Encoding? legacyEncoding) =>
        legacyEncoding is null
            ? Encoding.UTF8.GetBytes(name)
            : legacyEncoding.GetBytes(name);

    private static void WriteLocalEntry(
        Stream output,
        byte[] name,
        ushort flags,
        ushort method,
        uint crc,
        ushort modTime,
        ushort modDate,
        uint uncompressedSize,
        byte[] payload)
    {
        var head = new byte[30];
        BinaryPrimitives.WriteUInt32LittleEndian(head, 0x04034B50);
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(4), 20);
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(6), flags);
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(8), method);
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(10), modTime);
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(12), modDate);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(14), crc);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(18), (uint)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(22), uncompressedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(26), (ushort)name.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(28), 0);
        output.Write(head);
        output.Write(name);
        output.Write(payload);
    }

    private static void ReadFully(Stream source, byte[] target)
    {
        int total = 0;
        while (total < target.Length)
        {
            int read = source.Read(target, total, target.Length - total);
            if (read <= 0) throw new EndOfStreamException();
            total += read;
        }
    }

    private static ushort DosTime(DateTime time)
    {
        time = time.ToLocalTime();
        return (ushort)(((time.Second / 2) & 0x1F)
            | (((time.Minute) & 0x3F) << 5)
            | (((time.Hour) & 0x1F) << 11));
    }

    private static ushort DosDate(DateTime time)
    {
        time = time.ToLocalTime();
        int year = Math.Clamp(time.Year, 1980, 2107);
        return (ushort)((time.Day & 0x1F)
            | ((time.Month & 0x0F) << 5)
            | (((year - 1980) & 0x7F) << 9));
    }
}
