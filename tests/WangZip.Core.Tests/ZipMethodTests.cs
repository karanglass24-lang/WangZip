using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using WangZip.Core.Safety;
using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

public class ZipMethodTests
{
    /// <summary>지정한 압축 방식 값과 이미 압축된 바이트로 항목 1개짜리 ZIP을 직접 만듭니다.</summary>
    private static void WriteSingleEntryZip(string path, string name, ushort method, byte[] compressed, byte[] original)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        uint crc = Crc32.Compute(original);

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(0x04034B50u); w.Write((ushort)20); w.Write((ushort)0x800); w.Write(method);
        w.Write((ushort)0); w.Write((ushort)0x21); w.Write(crc);
        w.Write((uint)compressed.Length); w.Write((uint)original.Length);
        w.Write((ushort)nameBytes.Length); w.Write((ushort)0); w.Write(nameBytes); w.Write(compressed);

        uint cdOffset = (uint)ms.Length;
        w.Write(0x02014B50u); w.Write((ushort)20); w.Write((ushort)20); w.Write((ushort)0x800); w.Write(method);
        w.Write((ushort)0); w.Write((ushort)0x21); w.Write(crc);
        w.Write((uint)compressed.Length); w.Write((uint)original.Length);
        w.Write((ushort)nameBytes.Length); w.Write((ushort)0); w.Write((ushort)0);
        w.Write((ushort)0); w.Write((ushort)0); w.Write(0u); w.Write(0u); w.Write(nameBytes);
        uint cdSize = (uint)ms.Length - cdOffset;

        w.Write(0x06054B50u); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)1);
        w.Write(cdSize); w.Write(cdOffset); w.Write((ushort)0);
        w.Flush();
        File.WriteAllBytes(path, ms.ToArray());
    }

    private static byte[] SamplePayload() =>
        Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 400).Select(i => $"줄 {i}: 왕집 테스트 {i * 37 % 101}\n")));

    private static void ExtractAndCompare(string zip, byte[] expected)
    {
        using var output = new TempDir();
        using IArchiveSource source = ArchiveService.Open(zip);
        ExtractionResult result = source.Extract([], new ExtractionOptions { DestinationDirectory = output.Path }, null, CancellationToken.None);
        Assert.Equal(1, result.ExtractedFiles);
        Assert.True(expected.SequenceEqual(File.ReadAllBytes(Path.Combine(output.Path, "data.txt"))));
    }

    [Fact]
    public void Bzip2_method_12_extracts_and_verifies()
    {
        using var dir = new TempDir();
        byte[] original = SamplePayload();

        using var packed = new MemoryStream();
        using (var bz = SharpCompress.Compressors.BZip2.BZip2Stream.Create(
                   packed, SharpCompress.Compressors.CompressionMode.Compress, decompressConcatenated: false, leaveOpen: true))
        {
            bz.Write(original);
        }

        string zip = dir.File("bz2.zip");
        WriteSingleEntryZip(zip, "data.txt", 12, packed.ToArray(), original);

        ExtractAndCompare(zip, original);
        using var source = (ZipArchiveSource)ArchiveService.Open(zip);
        Assert.Equal(1, source.Verify(null, CancellationToken.None));
    }

    [Fact]
    public void Deflate64_method_9_decodes_plain_deflate_compatible_stream()
    {
        using var dir = new TempDir();
        // 긴 반복(길이 코드 285)이 없는 데이터는 Deflate와 Deflate64 비트스트림이 같습니다.
        byte[] original = new byte[4096];
        new Random(42).NextBytes(original);

        using var packed = new MemoryStream();
        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(original);
        }

        string zip = dir.File("d64.zip");
        WriteSingleEntryZip(zip, "data.txt", 9, packed.ToArray(), original);

        ExtractAndCompare(zip, original);
    }

    [Fact]
    public void Unknown_method_is_still_skipped_with_warning()
    {
        using var dir = new TempDir();
        byte[] original = SamplePayload();
        string zip = dir.File("lzma.zip");
        WriteSingleEntryZip(zip, "data.txt", 14, [1, 2, 3], original);

        using var output = new TempDir();
        using IArchiveSource source = ArchiveService.Open(zip);
        ExtractionResult result = source.Extract([], new ExtractionOptions { DestinationDirectory = output.Path }, null, CancellationToken.None);
        Assert.Equal(0, result.ExtractedFiles);
        Assert.Equal(1, result.SkippedFiles);
    }
}
