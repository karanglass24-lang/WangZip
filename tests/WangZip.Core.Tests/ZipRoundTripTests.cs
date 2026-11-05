using System.IO.Compression;
using System.Text;
using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

public class ZipRoundTripTests
{
    [Fact]
    public void Create_then_extract_round_trips_content_and_names()
    {
        using var work = new TempDir();
        using var output = new TempDir();

        work.Write("작업/문서/보고서.txt", "안녕하세요. 왕집 테스트입니다.");
        byte[] payload = Enumerable.Range(0, 8192).Select(index => (byte)(index * 31)).ToArray();
        work.WriteBytes("작업/이미지.bin", payload);

        string zip = output.File("결과.zip");
        CreationResult created = ZipWriter.Create(
            zip,
            [work.File("작업")],
            new CreationOptions { Level = CompressionLevel.Optimal },
            progress: null,
            CancellationToken.None);

        Assert.True(File.Exists(zip));
        Assert.Equal(4, created.EntryCount); // 파일 2 + 폴더 항목 2

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        Assert.Contains("작업/문서/보고서.txt", source.Entries.Select(entry => entry.Name));

        string destination = output.File("풀기");
        ExtractionResult extracted = source.Extract(
            [],
            new ExtractionOptions { DestinationDirectory = destination },
            progress: null,
            CancellationToken.None);

        Assert.Equal(2, extracted.ExtractedFiles);
        Assert.Equal(
            "안녕하세요. 왕집 테스트입니다.",
            File.ReadAllText(Path.Combine(destination, "작업", "문서", "보고서.txt")));
        Assert.True(payload.SequenceEqual(
            File.ReadAllBytes(Path.Combine(destination, "작업", "이미지.bin"))));
    }

    [Fact]
    public void Cp949_creation_is_detectable_on_reopen()
    {
        using var work = new TempDir();
        using var output = new TempDir();

        work.Write("한글 문서.txt", "내용");
        string zip = output.File("cp949.zip");

        ZipWriter.Create(
            zip,
            [work.File("한글 문서.txt")],
            new CreationOptions { NameEncoding = NameEncodingMode.Cp949 },
            progress: null,
            CancellationToken.None);

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);

        Assert.Equal("한글 문서.txt", Assert.Single(source.Entries).Name);
        Assert.Contains("CP949", source.EncodingDescription);
    }

    [Fact]
    public void Empty_directories_are_preserved()
    {
        using var work = new TempDir();
        using var output = new TempDir();

        work.Write("작업/파일.txt", "내용");
        Directory.CreateDirectory(Path.Combine(work.File("작업"), "빈폴더"));

        string zip = output.File("empty.zip");
        ZipWriter.Create(
            zip,
            [work.File("작업")],
            new CreationOptions { IncludeEmptyDirectories = true },
            progress: null,
            CancellationToken.None);

        string destination = output.File("풀기");
        using (var source = ZipArchiveSource.Open(zip))
        {
            source.Extract(
                [],
                new ExtractionOptions { DestinationDirectory = destination },
                progress: null,
                CancellationToken.None);
        }

        Assert.True(Directory.Exists(Path.Combine(destination, "작업", "빈폴더")));
    }

    [Fact]
    public void Zip_slip_entry_is_rejected_and_not_written()
    {
        using var output = new TempDir();
        string zip = output.File("slip.zip");

        using (var file = File.Create(zip))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("../evil.txt");
            using Stream stream = entry.Open();
            stream.Write("공격"u8);
        }

        string destination = Path.Combine(output.Path, "안전한곳");
        string escaped = Path.Combine(output.Path, "evil.txt");

        using var source = ZipArchiveSource.Open(zip);

        Assert.Throws<UnsafeArchiveEntryException>(() =>
            source.Extract(
                [],
                new ExtractionOptions { DestinationDirectory = destination },
                progress: null,
                CancellationToken.None));

        Assert.False(File.Exists(escaped));
    }

    [Fact]
    public void Corrupted_data_is_detected_by_crc_check()
    {
        using var output = new TempDir();
        string zip = output.File("crc.zip");

        using (var file = File.Create(zip))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("data.txt", CompressionLevel.NoCompression);
            using Stream stream = entry.Open();
            stream.Write("HELLO-CRC-PAYLOAD"u8);
        }

        byte[] bytes = File.ReadAllBytes(zip);
        byte[] pattern = "HELLO-CRC-PAYLOAD"u8.ToArray();
        int offset = IndexOf(bytes, pattern);
        Assert.True(offset > 0);

        bytes[offset] ^= 0xFF;
        File.WriteAllBytes(zip, bytes);

        using var source = ZipArchiveSource.Open(zip);

        Assert.Throws<CrcMismatchException>(() =>
            source.Extract(
                [],
                new ExtractionOptions { DestinationDirectory = output.File("풀기") },
                progress: null,
                CancellationToken.None));
    }

    [Fact]
    public void Skip_policy_leaves_existing_files_untouched()
    {
        using var work = new TempDir();
        using var output = new TempDir();

        work.Write("원본.txt", "첫 번째");
        string zip = output.File("skip.zip");
        ZipWriter.Create(zip, [work.File("원본.txt")], new CreationOptions(), null, CancellationToken.None);

        string destination = output.File("풀기");
        using var source = ZipArchiveSource.Open(zip);

        ExtractionResult first = source.Extract(
            [],
            new ExtractionOptions { DestinationDirectory = destination },
            progress: null,
            CancellationToken.None);

        ExtractionResult second = source.Extract(
            [],
            new ExtractionOptions { DestinationDirectory = destination, Overwrite = OverwritePolicy.Skip },
            progress: null,
            CancellationToken.None);

        Assert.Equal(1, first.ExtractedFiles);
        Assert.Equal(0, second.ExtractedFiles);
        Assert.Equal(1, second.SkippedFiles);
        Assert.Equal("첫 번째", File.ReadAllText(Path.Combine(destination, "원본.txt")));
    }

    [Fact]
    public void Verify_checks_crc_without_extracting()
    {
        using var work = new TempDir();
        using var output = new TempDir();

        work.Write("검사.txt", "무결성");
        string zip = output.File("verify.zip");
        ZipWriter.Create(zip, [work.File("검사.txt")], new CreationOptions(), null, CancellationToken.None);

        using var source = ZipArchiveSource.Open(zip);

        Assert.Equal(1, source.Verify(progress: null, CancellationToken.None));
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        }

        return -1;
    }
}

public class FolderEntryAndTreeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Created_zip_records_folder_entries_including_empty(bool encrypted)
    {
        using var work = new TempDir();
        using var output = new TempDir();
        work.Write("루트/a/b/파일.txt", "x");
        Directory.CreateDirectory(work.File("루트/빈폴더"));

        string zip = output.File("dirs.zip");
        ZipWriter.Create(
            zip,
            [work.File("루트")],
            new CreationOptions { Password = encrypted ? "pw1234" : null },
            null,
            CancellationToken.None);

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        var dirs = source.Entries.Where(e => e.IsDirectory).Select(e => e.Name.TrimEnd('/')).ToHashSet();
        Assert.Contains("루트", dirs);
        Assert.Contains("루트/a", dirs);
        Assert.Contains("루트/a/b", dirs);
        Assert.Contains("루트/빈폴더", dirs);
    }

    [Fact]
    public void Tree_builder_links_nested_folders_from_dirs_and_files()
    {
        FolderNode root = FolderTreeBuilder.Build("t.zip",
        [
            ("DB/", true),
            ("DB/sub/", true),
            ("EX/x/y.txt", false),
            ("top.txt", false),
        ]);

        Assert.True(root.IsRoot);
        Assert.Equal(["DB", "EX"], root.Children.Select(c => c.Name).ToArray());
        Assert.Equal("DB/sub", root.Children[0].Children.Single().FullPath);
        FolderNode x = root.Children[1].Children.Single();
        Assert.Equal("EX/x", x.FullPath);
        Assert.Empty(x.Children);
    }
}
