using System.Buffers.Binary;
using System.Text;
using WangZip.Core.Safety;

namespace WangZip.Core.Zip;

internal sealed class RawZipEntry
{
    public int Index;
    public ushort Flags;
    public ushort Method;
    public ushort ModTime;
    public ushort ModDate;
    public uint Crc;
    public long CompressedSize;
    public long UncompressedSize;
    public uint ExternalAttributes;
    public long LocalHeaderOffset;
    public byte[] RawName = [];
    public byte[] Extra = [];
    public string Comment = string.Empty;
    public string DecodedName = string.Empty;
    public string? EncodingName;
    public bool IsDirectory;
    public bool IsEncrypted;
    public bool IsSymlink;
    public byte[]? UnicodePathUtf8;
    public byte? AesStrength;
    public ushort? AesMethod;

    public bool HasAes => AesStrength is not null;
    /// <summary>중앙 디렉터리 Method(99 = AES) 대신 실제 압축 방식.</summary>
    public ushort RealMethod => HasAes && AesMethod is ushort m ? m : Method;

    public DateTime ModifiedLocal => DosToDateTime(ModDate, ModTime);

    private static DateTime DosToDateTime(ushort date, ushort time)
    {
        int year = ((date >> 9) & 0x7F) + 1980;
        int month = (date >> 5) & 0x0F;
        int day = date & 0x1F;
        int hour = (time >> 11) & 0x1F;
        int minute = (time >> 5) & 0x3F;
        int second = (time & 0x1F) * 2;

        if (year < 1980) year = 1980;
        if (month is < 1 or > 12) month = 1;
        day = Math.Clamp(day, 1, DateTime.DaysInMonth(year, month));
        hour = Math.Clamp(hour, 0, 23);
        minute = Math.Clamp(minute, 0, 59);
        second = Math.Clamp(second, 0, 59);

        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
    }
}

internal static class ZipCentralDirectory
{
    private const uint EocdSignature = 0x06054B50;
    private const uint Zip64EocdSignature = 0x06064B50;
    private const uint Zip64LocatorSignature = 0x07064B50;
    private const uint CentralHeaderSignature = 0x02014B50;

    public static List<RawZipEntry> Read(string path)
    {
        using var fs = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, FileOptions.RandomAccess);

        if (fs.Length < 22)
        {
            throw new ArchiveException("ZIP 파일이 너무 작거나 손상되었습니다.");
        }

        (long cdOffset, long cdSize, long entryCount) = ReadEndOfCentralDirectory(fs);

        if (cdOffset < 0 || cdSize < 0 || cdOffset + cdSize > fs.Length)
        {
            throw new ArchiveException("ZIP 중앙 디렉터리 위치가 올바르지 않습니다. 파일이 손상되었을 수 있습니다.");
        }

        fs.Position = cdOffset;
        var entries = new List<RawZipEntry>((int)Math.Min(entryCount, 1_000_000));
        var header = new byte[46];

        for (long i = 0; i < entryCount; i++)
        {
            fs.ReadExactly(header);

            uint signature = BinaryPrimitives.ReadUInt32LittleEndian(header);
            if (signature != CentralHeaderSignature)
            {
                throw new ArchiveException($"ZIP 중앙 디렉터리가 손상되었습니다 (항목 {i + 1}).");
            }

            var entry = new RawZipEntry
            {
                Index = (int)i,
                Flags = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8)),
                Method = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(10)),
                ModTime = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(12)),
                ModDate = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(14)),
                Crc = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16)),
                CompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20)),
                UncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24)),
                ExternalAttributes = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(38)),
                LocalHeaderOffset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(42)),
            };

            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28));
            int extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(30));
            int commentLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(32));
            int diskStart = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(34));

            entry.RawName = new byte[nameLength];
            fs.ReadExactly(entry.RawName);

            entry.Extra = new byte[extraLength];
            fs.ReadExactly(entry.Extra);

            var commentBytes = new byte[commentLength];
            fs.ReadExactly(commentBytes);
            entry.Comment = (entry.Flags & 0x800) != 0
                ? Encoding.UTF8.GetString(commentBytes)
                : ZipNameDecoder.DecodeLegacy(commentBytes);

            ApplyZip64Extra(entry, diskStart);
            TryReadUnicodePath(entry);

            entry.IsEncrypted = (entry.Flags & 0x0001) != 0;
            if (ZipCryptography.FindAesExtension(entry.Extra) is { } aes)
            {
                entry.AesStrength = aes.Strength;
                entry.AesMethod = aes.Method;
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static (long Offset, long Size, long Count) ReadEndOfCentralDirectory(FileStream fs)
    {
        long tailLength = Math.Min(fs.Length, 65_557 + 22);
        fs.Position = fs.Length - tailLength;
        var tail = new byte[tailLength];
        fs.ReadExactly(tail);

        int eocd = -1;
        for (int i = tail.Length - 22; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) != EocdSignature) continue;

            int commentLength = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 20));
            if (i + 22 + commentLength <= tail.Length)
            {
                eocd = i;
                break;
            }
        }

        if (eocd < 0)
        {
            throw new ArchiveException("ZIP 끝 레코드(EOCD)를 찾지 못했습니다. ZIP 파일이 아니거나 손상되었습니다.");
        }

        ushort diskNumber = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(eocd + 4));
        ushort cdStartDisk = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(eocd + 6));
        long count = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(eocd + 10));
        long cdSize = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(eocd + 12));
        long cdOffset = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(eocd + 16));

        bool needsZip64 = count == 0xFFFF || cdSize == 0xFFFFFFFF || cdOffset == 0xFFFFFFFF;

        if (needsZip64)
        {
            long locatorPosition = fs.Length - tailLength + eocd - 20;
            if (locatorPosition >= 0)
            {
                fs.Position = locatorPosition;
                var locator = new byte[20];
                fs.ReadExactly(locator);

                if (BinaryPrimitives.ReadUInt32LittleEndian(locator) == Zip64LocatorSignature)
                {
                    long zip64Offset = BinaryPrimitives.ReadInt64LittleEndian(locator.AsSpan(8));
                    if (zip64Offset >= 0 && zip64Offset + 56 <= fs.Length)
                    {
                        fs.Position = zip64Offset;
                        var record = new byte[56];
                        fs.ReadExactly(record);

                        if (BinaryPrimitives.ReadUInt32LittleEndian(record) == Zip64EocdSignature)
                        {
                            diskNumber = (ushort)Math.Min(
                                BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(16)), ushort.MaxValue);
                            cdStartDisk = (ushort)Math.Min(
                                BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(20)), ushort.MaxValue);
                            count = BinaryPrimitives.ReadInt64LittleEndian(record.AsSpan(32));
                            cdSize = BinaryPrimitives.ReadInt64LittleEndian(record.AsSpan(40));
                            cdOffset = BinaryPrimitives.ReadInt64LittleEndian(record.AsSpan(48));
                        }
                    }
                }
            }
        }

        if (diskNumber != 0 || cdStartDisk != 0)
        {
            throw new UnsupportedArchiveException(
                "분할 ZIP(.z01 / .001)은 아직 지원하지 않습니다. v0.2에서 추가될 예정입니다.");
        }

        return (cdOffset, cdSize, count);
    }

    private static void ApplyZip64Extra(RawZipEntry entry, int diskStart)
    {
        bool needUncompressed = entry.UncompressedSize == 0xFFFFFFFF;
        bool needCompressed = entry.CompressedSize == 0xFFFFFFFF;
        bool needOffset = entry.LocalHeaderOffset == 0xFFFFFFFF;
        bool needDisk = diskStart == 0xFFFF;

        if (!needUncompressed && !needCompressed && !needOffset && !needDisk) return;

        ReadOnlySpan<byte> extra = entry.Extra;
        int position = 0;
        while (position + 4 <= extra.Length)
        {
            ushort id = BinaryPrimitives.ReadUInt16LittleEndian(extra[position..]);
            ushort size = BinaryPrimitives.ReadUInt16LittleEndian(extra[(position + 2)..]);
            position += 4;

            if (position + size > extra.Length) break;

            ReadOnlySpan<byte> data = extra.Slice(position, size);
            if (id == 0x0001)
            {
                int cursor = 0;
                if (needUncompressed && cursor + 8 <= data.Length)
                {
                    entry.UncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(data[cursor..]);
                    cursor += 8;
                }

                if (needCompressed && cursor + 8 <= data.Length)
                {
                    entry.CompressedSize = BinaryPrimitives.ReadInt64LittleEndian(data[cursor..]);
                    cursor += 8;
                }

                if (needOffset && cursor + 8 <= data.Length)
                {
                    entry.LocalHeaderOffset = BinaryPrimitives.ReadInt64LittleEndian(data[cursor..]);
                    cursor += 8;
                }
            }

            position += size;
        }
    }

    private static void TryReadUnicodePath(RawZipEntry entry)
    {
        ReadOnlySpan<byte> extra = entry.Extra;
        int position = 0;
        while (position + 4 <= extra.Length)
        {
            ushort id = BinaryPrimitives.ReadUInt16LittleEndian(extra[position..]);
            ushort size = BinaryPrimitives.ReadUInt16LittleEndian(extra[(position + 2)..]);
            position += 4;

            if (position + size > extra.Length) break;

            if (id == 0x7075 && size >= 5)
            {
                ReadOnlySpan<byte> data = extra.Slice(position, size);
                if (data[0] == 1)
                {
                    uint expected = BinaryPrimitives.ReadUInt32LittleEndian(data[1..]);
                    if (expected == Crc32.Compute(entry.RawName))
                    {
                        entry.UnicodePathUtf8 = data[5..].ToArray();
                    }
                }
            }

            position += size;
        }
    }
}
