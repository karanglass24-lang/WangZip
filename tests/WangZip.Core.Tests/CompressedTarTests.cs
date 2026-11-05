using System.Formats.Tar;
using System.IO.Compression;

namespace WangZip.Core.Tests;

public class CompressedTarTests
{
    [Fact]
    public void Tar_gz_lists_and_extracts_entries()
    {
        using var dir = new TempDir();
        string archive = dir.File("묶음.tar.gz");

        using (var file = File.Create(archive))
        using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "문서/메모.txt")
            {
                DataStream = new MemoryStream("압축된 TAR 테스트"u8.ToArray()),
            };
            tar.WriteEntry(entry);
        }

        using IArchiveSource source = ArchiveService.Open(archive);

        Assert.Equal(ArchiveFormat.GZip, source.Format);
        Assert.Contains("문서/메모.txt", source.Entries.Select(entry => entry.Name));

        string destination = dir.File("풀기");
        ExtractionResult result = source.Extract(
            [],
            new ExtractionOptions { DestinationDirectory = destination },
            progress: null,
            CancellationToken.None);

        Assert.Equal(1, result.ExtractedFiles);
        Assert.Equal(
            "압축된 TAR 테스트",
            File.ReadAllText(Path.Combine(destination, "문서", "메모.txt")));
    }
}
