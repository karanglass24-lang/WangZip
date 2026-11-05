using System.IO.Compression;
using System.Text;

namespace WangZip.Core.Tests;

public class ArchiveServiceTests
{
    [Fact]
    public void Detects_zip_by_signature_even_without_extension()
    {
        using var dir = new TempDir();
        string zip = dir.File("archive.bin");

        using (var file = File.Create(zip))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("a.txt");
            using Stream stream = entry.Open();
            stream.Write("a"u8);
        }

        Assert.Equal(ArchiveFormat.Zip, ArchiveService.DetectFormat(zip));
    }

    [Fact]
    public void Unknown_file_throws_friendly_exception()
    {
        using var dir = new TempDir();
        string text = dir.File("memo.txt");
        File.WriteAllText(text, "이 파일은 압축 파일이 아닙니다.", Encoding.UTF8);

        Assert.Equal(ArchiveFormat.Unknown, ArchiveService.DetectFormat(text));
        Assert.Throws<UnsupportedArchiveException>(() => ArchiveService.Open(text));
    }

    [Fact]
    public void Only_zip_is_writable_in_v01()
    {
        Assert.True(ArchiveService.CanCreate(ArchiveFormat.Zip));
        Assert.False(ArchiveService.CanCreate(ArchiveFormat.Rar));
        Assert.False(ArchiveService.CanCreate(ArchiveFormat.SevenZip));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    public void Byte_formatting_is_human_readable(long value, string expected)
    {
        Assert.Equal(expected, Format.Bytes(value));
    }

    [Theory]
    [InlineData(0.25, "25.0% 절감")]
    [InlineData(0.0, "0.0% 절감")]
    [InlineData(-0.5, "50.0% 증가")]
    public void Compression_rate_reads_naturally(double ratio, string expected)
    {
        Assert.Equal(expected, Format.CompressionRate(ratio));
    }
}
