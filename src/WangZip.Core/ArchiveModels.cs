using System.IO.Compression;

namespace WangZip.Core;

public enum ArchiveFormat
{
    Zip,
    SevenZip,
    Tar,
    GZip,
    BZip2,
    Xz,
    Rar,
    Unknown,
}

/// <summary>압축 파일 안의 파일명을 어떤 문자셋으로 해석할지 결정합니다.</summary>
public enum NameEncodingMode
{
    /// <summary>UTF-8 플래그 → 유니코드 경로 필드 → CP949 → UTF-8 순으로 자동 판별.</summary>
    Auto,
    Utf8,
    Cp949,
    /// <summary>해석하지 않고 바이트를 그대로 1:1 매핑 (진단용).</summary>
    Raw,
}

public enum OverwritePolicy
{
    Overwrite,
    Skip,
    Rename,
}

public enum CollisionPolicy
{
    Rename,
    Skip,
    Throw,
}

public sealed record ArchiveEntryInfo
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public long Size { get; init; }
    public long CompressedSize { get; init; }
    public uint Crc32 { get; init; }
    public DateTimeOffset Modified { get; init; }
    public ushort Method { get; init; }
    public uint Flags { get; init; }
    public uint ExternalAttributes { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsEncrypted { get; init; }
    public bool IsSymlink { get; init; }
    public long LocalHeaderOffset { get; init; }
    public string? Comment { get; init; }
    public string? EncodingName { get; init; }

    public double Ratio => Size <= 0 ? 0 : 1.0 - (double)CompressedSize / Size;

    public string MethodName => Method switch
    {
        0 => "저장",
        8 => "Deflate",
        9 => "Deflate64",
        12 => "BZip2",
        14 => "LZMA",
        93 => "Zstandard",
        95 => "XZ",
        99 => "AES",
        _ => $"방식 {Method}",
    };

    public string SizeText => Format.Bytes(Size);
    public string CompressedText => Format.Bytes(CompressedSize);
    public string RatioText => Size <= 0 ? "-" : $"{Ratio * 100:0.0}%";
    public string ModifiedText => Modified == default ? "-" : Modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    public string TypeText => IsDirectory ? "폴더" : IsSymlink ? "링크" : "파일";
}

public sealed class ExtractionOptions
{
    public required string DestinationDirectory { get; set; }
    public OverwritePolicy Overwrite { get; set; } = OverwritePolicy.Overwrite;
    public bool PreserveTimestamps { get; set; } = true;
    public bool CreateDirectoryEntries { get; set; } = true;
    public bool CheckFreeSpace { get; set; } = true;
    /// <summary>압축 파일 안에서 중복/대소문자 충돌이 날 때의 처리 방식.</summary>
    public CollisionPolicy Collision { get; set; } = CollisionPolicy.Rename;
    /// <summary>암호화 항목에 사용할 비밀번호. null이면 암호화 항목을 만나면 거부합니다.</summary>
    public string? Password { get; set; }
}

public sealed class ExtractionResult
{
    public int ExtractedFiles { get; init; }
    public int SkippedFiles { get; init; }
    public long TotalBytes { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public TimeSpan Elapsed { get; init; }
}

public sealed class CreationOptions
{
    public CompressionLevel Level { get; set; } = CompressionLevel.Optimal;
    /// <summary>Auto는 UTF-8로 기록합니다(현대 표준). CP949는 구형 Windows 호환용입니다.</summary>
    public NameEncodingMode NameEncoding { get; set; } = NameEncodingMode.Auto;
    public bool IncludeEmptyDirectories { get; set; } = true;
    public string? Comment { get; set; }
}

public sealed class CreationResult
{
    public required string OutputPath { get; init; }
    public int EntryCount { get; init; }
    public long SourceBytes { get; init; }
    public long OutputBytes { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public double Ratio => SourceBytes <= 0 ? 0 : 1.0 - (double)OutputBytes / SourceBytes;
}

public readonly record struct ProgressReport(long Processed, long Total, string? CurrentName)
{
    public double Percent => Total <= 0 ? 0 : Math.Min(100.0, Processed * 100.0 / Total);
}

public class ArchiveException : Exception
{
    public ArchiveException(string message) : base(message) { }
    public ArchiveException(string message, Exception inner) : base(message, inner) { }
}

public sealed class EncryptedArchiveException : ArchiveException
{
    public EncryptedArchiveException(string message) : base(message) { }
}

public sealed class UnsafeArchiveEntryException : ArchiveException
{
    public UnsafeArchiveEntryException(string message) : base(message) { }
}

public sealed class CrcMismatchException : ArchiveException
{
    public CrcMismatchException(string message) : base(message) { }
}

public sealed class UnsupportedArchiveException : ArchiveException
{
    public UnsupportedArchiveException(string message) : base(message) { }
    public UnsupportedArchiveException(string message, Exception inner) : base(message, inner) { }
}

public static class Format
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Bytes(long value)
    {
        if (value < 0) return "-";
        double size = value;
        int unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:N0} B" : $"{size:0.##} {Units[unit]}";
    }

    /// <summary>압축률을 사람이 읽기 좋은 문장으로 만듭니다 (음수는 "증가").</summary>
    public static string CompressionRate(double ratio) => ratio >= 0
        ? $"{ratio * 100:0.0}% 절감"
        : $"{-ratio * 100:0.0}% 증가";
}
