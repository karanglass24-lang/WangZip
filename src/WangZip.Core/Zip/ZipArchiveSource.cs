using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using WangZip.Core.Safety;

namespace WangZip.Core.Zip;

/// <summary>
/// ZIP을 직접 파싱해 읽는 구현.
/// 파일명 원본 바이트를 확보할 수 있어 한글 인코딩 판별에 유리하고,
/// 항목별 CRC 검증·취소·진행률을 정확하게 처리합니다.
/// </summary>
public sealed class ZipArchiveSource : IArchiveSource
{
    private const uint LocalHeaderSignature = 0x04034B50;

    private readonly string _archivePath;
    private readonly List<RawZipEntry> _raw;

    private ZipArchiveSource(
        string archivePath,
        List<RawZipEntry> raw,
        IReadOnlyList<ArchiveEntryInfo> entries,
        IReadOnlyList<string> warnings,
        string encodingDescription)
    {
        _archivePath = archivePath;
        _raw = raw;
        Entries = entries;
        Warnings = warnings;
        EncodingDescription = encodingDescription;
    }

    public string ArchivePath => _archivePath;
    public ArchiveFormat Format => ArchiveFormat.Zip;
    public IReadOnlyList<ArchiveEntryInfo> Entries { get; }
    public IReadOnlyList<string> Warnings { get; }
    public string EncodingDescription { get; }

    public static ZipArchiveSource Open(string archivePath, NameEncodingMode mode = NameEncodingMode.Auto)
    {
        List<RawZipEntry> raw = ZipCentralDirectory.Read(archivePath);
        var entries = new List<ArchiveEntryInfo>(raw.Count);
        var encodings = new Dictionary<string, int>(StringComparer.Ordinal);
        var warnings = new List<string>();

        foreach (RawZipEntry item in raw)
        {
            string name = ZipNameDecoder.Decode(
                item.RawName,
                (item.Flags & 0x800) != 0,
                item.UnicodePathUtf8,
                mode,
                out string encodingName);

            item.DecodedName = name;
            item.EncodingName = encodingName;
            encodings[encodingName] = encodings.GetValueOrDefault(encodingName) + 1;

            item.IsDirectory = name.EndsWith('/') || (item.ExternalAttributes & 0x10) != 0;
            ushort unixMode = (ushort)((item.ExternalAttributes >> 16) & 0xFFFF);
            item.IsSymlink = (unixMode & 0xF000) == 0xA000;

            if (item.CompressedSize < 0 || item.UncompressedSize < 0 ||
                item.LocalHeaderOffset < 0 || item.LocalHeaderOffset >= new FileInfo(archivePath).Length)
            {
                warnings.Add($"항목 위치가 비정상입니다: {name}");
                continue;
            }

            entries.Add(new ArchiveEntryInfo
            {
                Index = item.Index,
                Name = name,
                Size = item.UncompressedSize,
                CompressedSize = item.CompressedSize,
                Crc32 = item.Crc,
                Modified = new DateTimeOffset(item.ModifiedLocal),
                Method = item.Method,
                Flags = item.Flags,
                ExternalAttributes = item.ExternalAttributes,
                IsDirectory = item.IsDirectory,
                IsEncrypted = item.IsEncrypted,
                IsSymlink = item.IsSymlink,
                LocalHeaderOffset = item.LocalHeaderOffset,
                Comment = item.Comment,
                EncodingName = item.EncodingName,
            });
        }

        return new ZipArchiveSource(archivePath, raw, entries, warnings, DescribeEncodings(encodings));
    }

    public ExtractionResult Extract(
        IReadOnlyCollection<int> indices,
        ExtractionOptions options,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        List<RawZipEntry> selected = Select(indices);

        RawZipEntry? encrypted = selected.FirstOrDefault(item => item.IsEncrypted);
        if (encrypted is not null)
        {
            throw new EncryptedArchiveException(
                $"암호가 설정된 항목이 있습니다: {encrypted.DecodedName}\n" +
                "암호 지원은 v0.2에서 추가될 예정입니다.");
        }

        long totalBytes = selected
            .Where(item => !item.IsDirectory && !item.IsSymlink)
            .Sum(item => item.UncompressedSize);

        if (options.CheckFreeSpace)
        {
            ExtractionHelpers.EnsureFreeSpace(options.DestinationDirectory, totalBytes);
        }

        var guard = new PathGuard(options.DestinationDirectory);
        var throttle = new ProgressThrottle(progress);
        var warnings = new List<string>(Warnings);
        var stopwatch = Stopwatch.StartNew();

        int extractedFiles = 0;
        int skippedFiles = 0;
        long processedBytes = 0;

        foreach (RawZipEntry item in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? target = guard.Resolve(item.DecodedName, options.Collision);
            if (target is null)
            {
                skippedFiles++;
                continue;
            }

            if (item.IsDirectory)
            {
                if (options.CreateDirectoryEntries)
                {
                    Directory.CreateDirectory(PathGuard.ToExtended(target));
                }

                continue;
            }

            if (item.IsSymlink)
            {
                warnings.Add($"심볼릭 링크는 건너뜁니다: {item.DecodedName}");
                skippedFiles++;
                continue;
            }

            if (item.Method is not (0 or 8))
            {
                warnings.Add($"지원하지 않는 압축 방식({item.Method})이라 건너뜁니다: {item.DecodedName}");
                skippedFiles++;
                continue;
            }

            if (File.Exists(PathGuard.ToExtended(target)) || Directory.Exists(PathGuard.ToExtended(target)))
            {
                if (options.Overwrite == OverwritePolicy.Skip)
                {
                    skippedFiles++;
                    continue;
                }

                if (options.Overwrite == OverwritePolicy.Rename)
                {
                    target = guard.FindFreeOnDisk(target);
                }
            }

            string? directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(PathGuard.ToExtended(directory));
            }

            ExtractEntry(item, target, options, processedBytes, totalBytes, throttle, cancellationToken);
            processedBytes += item.UncompressedSize;
            extractedFiles++;
        }

        throttle.Report(processedBytes, totalBytes, null, force: true);
        warnings.AddRange(guard.Warnings);

        return new ExtractionResult
        {
            ExtractedFiles = extractedFiles,
            SkippedFiles = skippedFiles,
            TotalBytes = processedBytes,
            Warnings = warnings,
            Elapsed = stopwatch.Elapsed,
        };
    }

    /// <summary>압축을 풀지 않고 각 항목의 CRC만 검사합니다.</summary>
    public int Verify(IProgress<ProgressReport>? progress, CancellationToken cancellationToken)
    {
        long totalBytes = _raw.Where(item => !item.IsDirectory).Sum(item => item.UncompressedSize);
        var throttle = new ProgressThrottle(progress);
        long processed = 0;
        int checkedCount = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);

        try
        {
            using var fs = new FileStream(
                _archivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 128 * 1024, FileOptions.SequentialScan);

            foreach (RawZipEntry item in _raw)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.IsDirectory) continue;

                if (item.IsEncrypted)
                {
                    throw new EncryptedArchiveException($"암호가 설정된 항목은 검사할 수 없습니다: {item.DecodedName}");
                }

                if (item.Method is not (0 or 8))
                {
                    continue;
                }

                uint crc = 0;
                long written = 0;

                using Stream data = OpenEntryData(item, fs);
                int read;
                while ((read = data.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    crc = Crc32.Append(crc, buffer.AsSpan(0, read));
                    written += read;
                    processed += read;
                    throttle.Report(processed, totalBytes, item.DecodedName);
                }

                if (written != item.UncompressedSize)
                {
                    throw new ArchiveException(
                        $"항목 크기가 일치하지 않습니다: {item.DecodedName} " +
                        $"(기대 {item.UncompressedSize:N0}바이트, 실제 {written:N0}바이트)");
                }

                if (crc != item.Crc)
                {
                    throw new CrcMismatchException(
                        $"CRC가 일치하지 않습니다: {item.DecodedName} " +
                        $"(기대 {item.Crc:X8}, 실제 {crc:X8})");
                }

                checkedCount++;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        throttle.Report(processed, totalBytes, null, force: true);
        return checkedCount;
    }

    private void ExtractEntry(
        RawZipEntry item,
        string target,
        ExtractionOptions options,
        long processedBefore,
        long totalBytes,
        ProgressThrottle throttle,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(target)!;
        string temporary = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".wang-tmp");

        try
        {
            using (var fs = new FileStream(
                       _archivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                       bufferSize: 128 * 1024, FileOptions.SequentialScan))
            using (Stream data = OpenEntryData(item, fs))
            using (var output = new FileStream(
                       PathGuard.ToExtended(temporary), FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, bufferSize: 128 * 1024, FileOptions.SequentialScan))
            {
                var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
                uint crc = 0;
                long written = 0;

                try
                {
                    int read;
                    while ((read = data.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        output.Write(buffer, 0, read);
                        crc = Crc32.Append(crc, buffer.AsSpan(0, read));
                        written += read;
                        throttle.Report(processedBefore + written, totalBytes, item.DecodedName);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if (written != item.UncompressedSize)
                {
                    throw new ArchiveException(
                        $"항목 크기가 일치하지 않습니다: {item.DecodedName} " +
                        $"(기대 {item.UncompressedSize:N0}바이트, 실제 {written:N0}바이트)");
                }

                if (crc != item.Crc)
                {
                    throw new CrcMismatchException(
                        $"CRC가 일치하지 않습니다: {item.DecodedName} " +
                        $"(기대 {item.Crc:X8}, 실제 {crc:X8})");
                }
            }

            if (options.PreserveTimestamps)
            {
                File.SetLastWriteTime(PathGuard.ToExtended(temporary), item.ModifiedLocal);
            }

            File.Move(PathGuard.ToExtended(temporary), PathGuard.ToExtended(target), overwrite: true);
        }
        catch
        {
            ExtractionHelpers.TryDelete(temporary);
            throw;
        }
    }

    private static Stream OpenEntryData(RawZipEntry item, FileStream fs)
    {
        fs.Position = item.LocalHeaderOffset;

        Span<byte> header = stackalloc byte[30];
        fs.ReadExactly(header);

        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != LocalHeaderSignature)
        {
            throw new ArchiveException($"지역 헤더가 손상되었습니다: {item.DecodedName}");
        }

        int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header[26..]);
        int extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header[28..]);

        fs.Position = item.LocalHeaderOffset + 30 + nameLength + extraLength;

        var bounded = new BoundedReadStream(fs, item.CompressedSize);
        return item.Method == 0
            ? bounded
            : new DeflateStream(bounded, CompressionMode.Decompress);
    }

    private List<RawZipEntry> Select(IReadOnlyCollection<int> indices)
    {
        if (indices.Count == 0) return _raw;

        var selected = new List<RawZipEntry>(indices.Count);
        foreach (int index in indices)
        {
            if (index >= 0 && index < _raw.Count) selected.Add(_raw[index]);
        }

        return selected;
    }

    private static string DescribeEncodings(Dictionary<string, int> encodings)
    {
        if (encodings.Count == 0) return "알 수 없음";
        if (encodings.Count == 1) return encodings.Keys.First();

        return string.Join(", ", encodings
            .OrderByDescending(pair => pair.Value)
            .Select(pair => $"{pair.Key} {pair.Value}개"));
    }

    public void Dispose()
    {
        // 모든 파일 핸들은 작업 단위로 열고 닫으므로 유지할 자원이 없습니다.
    }
}
