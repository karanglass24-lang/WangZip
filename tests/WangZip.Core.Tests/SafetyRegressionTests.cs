using System.Security.Cryptography;
using WangZip.Core.Safety;
using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

/// <summary>코드 리뷰에서 나온 데이터 손실·안전 문제의 회귀 테스트.</summary>
public class SafetyRegressionTests
{
    private static string NewFolder()
    {
        string dir = Path.Combine(Path.GetTempPath(), "wangzip-safety", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Cancelled_create_keeps_existing_output_file()
    {
        string dir = NewFolder();
        string source = Path.Combine(dir, "data.bin");
        File.WriteAllBytes(source, RandomNumberGenerator.GetBytes(50_000));
        string zip = Path.Combine(dir, "out.zip");
        File.WriteAllText(zip, "original");

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            ZipWriter.Create(zip, [source], new CreationOptions(), null, cts.Token));

        Assert.Equal("original", File.ReadAllText(zip));
        Assert.Empty(Directory.GetFiles(dir, "*.wang-tmp"));
    }

    [Fact]
    public void Cancelled_encrypted_create_keeps_existing_output_file()
    {
        string dir = NewFolder();
        string source = Path.Combine(dir, "data.bin");
        File.WriteAllBytes(source, RandomNumberGenerator.GetBytes(50_000));
        string zip = Path.Combine(dir, "out.zip");
        File.WriteAllText(zip, "original");

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            ZipWriter.Create(zip, [source], new CreationOptions { Password = "pw" }, null, cts.Token));

        Assert.Equal("original", File.ReadAllText(zip));
        Assert.Empty(Directory.GetFiles(dir, "*.wang-tmp"));
    }

    [Fact]
    public void Rewriter_adds_empty_and_incompressible_files_and_leaves_no_temp_files()
    {
        string dir = NewFolder();
        string seed = Path.Combine(dir, "seed.txt");
        File.WriteAllText(seed, "seed");
        string zip = Path.Combine(dir, "t.zip");
        ZipWriter.Create(zip, [seed], new CreationOptions(), null, CancellationToken.None);

        byte[] random = RandomNumberGenerator.GetBytes(100_000);
        File.WriteAllBytes(Path.Combine(dir, "random.bin"), random);
        File.WriteAllBytes(Path.Combine(dir, "empty.txt"), []);

        ZipRewriter.AddEntries(zip, [Path.Combine(dir, "random.bin"), Path.Combine(dir, "empty.txt")]);

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        Assert.Equal(3, source.Entries.Count);
        Assert.Equal(3, source.Verify(null, CancellationToken.None));

        string outDir = Path.Combine(dir, "out");
        source.Extract([], new ExtractionOptions { DestinationDirectory = outDir }, null, CancellationToken.None);
        Assert.Equal(random, File.ReadAllBytes(Path.Combine(outDir, "random.bin")));
        Assert.Empty(File.ReadAllBytes(Path.Combine(outDir, "empty.txt")));

        Assert.Empty(Directory.GetFiles(dir, "*.wang-tmp"));
        Assert.Empty(Directory.GetFiles(dir, "*.wang-pay"));
    }

    [Fact]
    public void Long_unc_paths_use_the_UNC_extended_prefix()
    {
        string path = @"\\server\share\" + new string('a', 260);
        Assert.StartsWith(@"\\?\UNC\server\share\", PathGuard.ToExtended(path));
        Assert.StartsWith(@"\\?\C:\", PathGuard.ToExtended(@"C:\" + new string('a', 260)));
    }

    [Fact]
    public void Extraction_carries_mark_of_the_web_from_archive()
    {
        string dir = NewFolder();
        string seed = Path.Combine(dir, "a.txt");
        File.WriteAllText(seed, "hello");
        string zip = Path.Combine(dir, "t.zip");
        ZipWriter.Create(zip, [seed], new CreationOptions(), null, CancellationToken.None);

        byte[] zone = System.Text.Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n");
        MarkOfTheWeb.Apply(zone, zip);
        if (MarkOfTheWeb.Read(zip) is null) return; // NTFS가 아닌 임시 폴더면 검사할 수 없습니다.

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        string outDir = Path.Combine(dir, "out");
        source.Extract([], new ExtractionOptions { DestinationDirectory = outDir }, null, CancellationToken.None);

        Assert.Equal(zone, MarkOfTheWeb.Read(Path.Combine(outDir, "a.txt")));
    }
}
