using System.Text;
using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

/// <summary>열려 있는 ZIP에 항목 추가/삭제(raw copy 재구성) 테스트.</summary>
public class ZipRewriterTests
{
    private static string NewFolder() =>
        Path.Combine(Path.GetTempPath(), "wangzip-rewriter", Guid.NewGuid().ToString("N"));

    private static byte[] ReadBytes(string path) => File.ReadAllBytes(path);

    [Fact]
    public void Add_then_delete_entries_keeps_original_data()
    {
        string dir = NewFolder();
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        byte[] a = Encoding.UTF8.GetBytes("AAA 텍스트 파일");
        byte[] b = Enumerable.Range(0, 10_000).Select(i => (byte)(i % 251)).ToArray();
        File.WriteAllBytes(Path.Combine(dir, "src", "a.txt"), a);
        File.WriteAllBytes(Path.Combine(dir, "src", "b.bin"), b);
        File.WriteAllBytes(Path.Combine(dir, "extra.txt"), b);
        string zip = Path.Combine(dir, "test.zip");

        ZipWriter.Create(zip, [Path.Combine(dir, "src")], new CreationOptions(), null, CancellationToken.None);

        using (var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto))
        {
            Assert.Equal(3, source.Entries.Count);

            // 추가
            var added = ZipRewriter.AddEntries(zip, [Path.Combine(dir, "extra.txt")]);
            Assert.Equal(1, added.ChangedEntries);
        }

        byte[] extraContent = ReadBytes(Path.Combine(dir, "extra.txt"));
        using (var reopened = ZipArchiveSource.Open(zip, NameEncodingMode.Auto))
        {
            Assert.Equal(4, reopened.Entries.Count);
            Assert.Contains(reopened.Entries, entry => entry.Name == "extra.txt");

            int checkedCount = reopened.Verify(null, CancellationToken.None);
            Assert.Equal(3, checkedCount);

            string outDir = Path.Combine(dir, "out1");
            reopened.Extract([], new ExtractionOptions { DestinationDirectory = outDir }, null, CancellationToken.None);
            Assert.Equal(extraContent, ReadBytes(Path.Combine(outDir, "extra.txt")));
        }

        // 삭제
        using (var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto))
        {
            var target = source.Entries.First(entry => entry.Name == "extra.txt");
            var removed = ZipRewriter.DeleteEntries(zip, [target.Index]);
            Assert.Equal(1, removed.ChangedEntries);
        }

        using (var final = ZipArchiveSource.Open(zip, NameEncodingMode.Auto))
        {
            Assert.Equal(3, final.Entries.Count);
            int checkedCount = final.Verify(null, CancellationToken.None);
            Assert.Equal(2, checkedCount);

            string outDir = Path.Combine(dir, "out2");
            final.Extract([], new ExtractionOptions { DestinationDirectory = outDir }, null, CancellationToken.None);
            Assert.Equal(a, ReadBytes(Path.Combine(outDir, "src", "a.txt")));
            Assert.Equal(b, ReadBytes(Path.Combine(outDir, "src", "b.bin")));
        }
    }

    [Fact]
    public void Add_folder_recursively()
    {
        string dir = NewFolder();
        Directory.CreateDirectory(Path.Combine(dir, "src2", "내부"));
        byte[] data = [1, 2, 3, 4, 5];
        File.WriteAllBytes(Path.Combine(dir, "src2", "내부", "깊은 파일.dat"), data);
        string zip = Path.Combine(dir, "folder.zip");

        ZipWriter.Create(zip, [Path.Combine(dir, "src2")], new CreationOptions(), null, CancellationToken.None);
        byte[] single = [9, 9];
        File.WriteAllBytes(Path.Combine(dir, "one.dat"), single);
        ZipRewriter.AddEntries(zip, [Path.Combine(dir, "one.dat")]);
        System.Console.WriteLine("scratchzip=" + zip);

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        Assert.Equal(4, source.Entries.Count);   // 폴더 항목 2개 + 파일 2개
        var options = new ExtractionOptions { DestinationDirectory = Path.Combine(dir, "out") };
        source.Extract([], options, null, CancellationToken.None);
        Assert.Equal(single, ReadBytes(Path.Combine(dir, "out", "one.dat")));
    }

    [Fact]
    public void Cannot_add_existing_name()
    {
        string dir = NewFolder();
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "same.txt"), [1]);
        string zip = Path.Combine(dir, "dupe.zip");
        ZipWriter.Create(zip, [Path.Combine(dir, "same.txt")], new CreationOptions(), null, CancellationToken.None);

        var result = ZipRewriter.AddEntries(zip, [Path.Combine(dir, "same.txt")]);
        Assert.Equal(0, result.ChangedEntries);
        Assert.Contains(result.Warnings, warning => warning.Contains("건너뜁니다"));
    }
}
