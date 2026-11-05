using System.Text;
using WangZip.Core.SharpCompressSupport;
using WangZip.Core.Zip;

namespace WangZip.Core;

public static class ArchiveService
{
    static ArchiveService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static readonly string[] ZipExtensions =
    [
        ".zip", ".zipx", ".cbz", ".jar", ".war", ".apk", ".docx", ".xlsx", ".pptx", ".odt", ".epub",
    ];

    public static IArchiveSource Open(
        string archivePath,
        NameEncodingMode encodingMode = NameEncodingMode.Auto,
        string? password = null)
    {
        string fullPath = Path.GetFullPath(archivePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"압축 파일을 찾을 수 없습니다: {archivePath}", fullPath);
        }

        ArchiveFormat format = DetectFormat(fullPath);

        if (format == ArchiveFormat.Zip)
        {
            return ZipArchiveSource.Open(fullPath, encodingMode);
        }

        if (format == ArchiveFormat.Unknown)
        {
            throw new UnsupportedArchiveException(
                $"지원하지 않는 형식입니다: {Path.GetFileName(fullPath)}\n" +
                "지원 형식: ZIP, 7z, TAR, GZ, BZ2, XZ, RAR(읽기)");
        }

        if (IsCompressedTar(fullPath))
        {
            return ReaderArchiveSource.Open(fullPath, format, password);
        }

        return SharpCompressSource.Open(fullPath, format, password);
    }

    /// <summary>.tar.gz / .tgz 처럼 압축 스트림 안에 TAR이 있는 형식인지 확인합니다.</summary>
    public static bool IsCompressedTar(string archivePath)
    {
        string name = Path.GetFileName(archivePath).ToLowerInvariant();
        return name.EndsWith(".tar.gz") || name.EndsWith(".tgz")
            || name.EndsWith(".tar.bz2") || name.EndsWith(".tbz") || name.EndsWith(".tbz2")
            || name.EndsWith(".tar.xz") || name.EndsWith(".txz");
    }

    public static ArchiveFormat DetectFormat(string archivePath)
    {
        try
        {
            using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> header = stackalloc byte[512];
            int read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            ReadOnlySpan<byte> data = header[..read];

            if (read >= 4 && data[0] == 0x50 && data[1] == 0x4B &&
                (data[2] == 0x03 || data[2] == 0x05 || data[2] == 0x07))
            {
                return ArchiveFormat.Zip;
            }

            if (read >= 6 && data[..6].SequenceEqual(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }))
            {
                return ArchiveFormat.SevenZip;
            }

            if (read >= 7 && data[..7].SequenceEqual(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }))
            {
                return ArchiveFormat.Rar;
            }

            if (read >= 7 && data[..7].SequenceEqual(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01 }))
            {
                return ArchiveFormat.Rar;
            }

            if (read >= 2 && data[0] == 0x1F && data[1] == 0x8B)
            {
                return ArchiveFormat.GZip;
            }

            if (read >= 3 && data[0] == 0x42 && data[1] == 0x5A && data[2] == 0x68)
            {
                return ArchiveFormat.BZip2;
            }

            if (read >= 6 && data[..6].SequenceEqual(new byte[] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 }))
            {
                return ArchiveFormat.Xz;
            }

            if (read >= 262 && data.Slice(257, 5).SequenceEqual("ustar"u8))
            {
                return ArchiveFormat.Tar;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }

        return DetectByExtension(archivePath);
    }

    private static ArchiveFormat DetectByExtension(string archivePath)
    {
        string extension = Path.GetExtension(archivePath).ToLowerInvariant();

        if (ZipExtensions.Contains(extension)) return ArchiveFormat.Zip;
        if (extension is ".7z") return ArchiveFormat.SevenZip;
        if (extension is ".rar") return ArchiveFormat.Rar;
        if (extension is ".tar") return ArchiveFormat.Tar;
        if (extension is ".gz" or ".tgz") return ArchiveFormat.GZip;
        if (extension is ".bz2" or ".tbz" or ".tbz2") return ArchiveFormat.BZip2;
        if (extension is ".xz" or ".txz") return ArchiveFormat.Xz;

        return ArchiveFormat.Unknown;
    }

    public static string FormatDisplayName(ArchiveFormat format) => format switch
    {
        ArchiveFormat.Zip => "ZIP",
        ArchiveFormat.SevenZip => "7z",
        ArchiveFormat.Tar => "TAR",
        ArchiveFormat.GZip => "GZ",
        ArchiveFormat.BZip2 => "BZ2",
        ArchiveFormat.Xz => "XZ",
        ArchiveFormat.Rar => "RAR",
        _ => "알 수 없음",
    };

    /// <summary>이 형식을 생성(쓰기)할 수 있는지 여부.</summary>
    public static bool CanCreate(ArchiveFormat format) => format == ArchiveFormat.Zip;

    public static string FormatForPath(string outputPath) =>
        FormatDisplayName(DetectFormat(outputPath));
}
