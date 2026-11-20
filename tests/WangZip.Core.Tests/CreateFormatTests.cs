using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

public class CreateFormatTests
{
    [Theory]
    [InlineData(CreateFormat.SevenZip, ".7z", ArchiveFormat.SevenZip)]
    [InlineData(CreateFormat.Tar, ".tar", ArchiveFormat.Tar)]
    [InlineData(CreateFormat.TarGz, ".tar.gz", ArchiveFormat.GZip)]
    [InlineData(CreateFormat.TarBz2, ".tar.bz2", ArchiveFormat.BZip2)]
    public void Created_archive_round_trips(CreateFormat format, string extension, ArchiveFormat expected)
    {
        using var dir = new TempDir();
        byte[] big = new byte[200_000];
        new Random(3).NextBytes(big);
        dir.WriteBytes("src/folder/big.bin", big);
        dir.Write("src/folder/한글.txt", "안녕하세요");
        Directory.CreateDirectory(dir.File("src/folder/empty"));

        string output = dir.File("out" + extension);
        CreationResult result = ZipWriter.Create(
            output, [dir.File("src/folder")], new CreationOptions { Format = format }, null, CancellationToken.None);

        Assert.Equal(output, result.OutputPath);
        using var dest = new TempDir();
        using IArchiveSource source = ArchiveService.Open(output);
        Assert.Equal(expected, source.Format);
        source.Extract([], new ExtractionOptions { DestinationDirectory = dest.Path }, null, CancellationToken.None);

        Assert.True(big.SequenceEqual(File.ReadAllBytes(Path.Combine(dest.Path, "folder", "big.bin"))));
        Assert.Equal("안녕하세요", File.ReadAllText(Path.Combine(dest.Path, "folder", "한글.txt")));
    }

    [Fact]
    public void Password_or_split_with_non_zip_is_rejected()
    {
        using var dir = new TempDir();
        dir.Write("src/a.txt", "x");
        Assert.Throws<UnsupportedArchiveException>(() => ZipWriter.Create(
            dir.File("a.7z"), [dir.File("src/a.txt")],
            new CreationOptions { Format = CreateFormat.SevenZip, Password = "pw" }, null, CancellationToken.None));
        Assert.Throws<UnsupportedArchiveException>(() => ZipWriter.Create(
            dir.File("a.tar"), [dir.File("src/a.txt")],
            new CreationOptions { Format = CreateFormat.Tar, SplitSizeBytes = 100_000 }, null, CancellationToken.None));
    }

    [Fact]
    public void Cancelled_non_zip_creation_leaves_no_output()
    {
        using var dir = new TempDir();
        dir.Write("src/a.txt", "x");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        string output = dir.File("a.7z");
        Assert.ThrowsAny<OperationCanceledException>(() => ZipWriter.Create(
            output, [dir.File("src/a.txt")], new CreationOptions { Format = CreateFormat.SevenZip }, null, cts.Token));
        Assert.False(File.Exists(output));
    }
}
