using System.Buffers.Binary;
using System.IO.Compression;
using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

public class SplitZipTests
{
    private static byte[] Payload(int length, int seed) =>
        Enumerable.Range(0, length).Select(i => (byte)((i * 131 + seed) ^ (i >> 5))).ToArray();

    private static string BuildZip(TempDir dir, out byte[] a, out byte[] b)
    {
        a = Payload(40_000, 1);
        b = Payload(25_000, 7);
        dir.WriteBytes("src/a.bin", a);
        dir.WriteBytes("src/b.bin", b);
        string zip = dir.File("whole.zip");
        ZipWriter.Create(
            zip,
            [dir.File("src/a.bin"), dir.File("src/b.bin")],
            new CreationOptions { Level = CompressionLevel.NoCompression },
            progress: null,
            CancellationToken.None);
        return zip;
    }

    /// <summary>PKZIP/Info-ZIP 방식(.z01…, 마지막 .zip, 디스크 번호·상대 오프셋)으로 나눕니다.</summary>
    private static void SplitPkzip(string zip, string baseStem, int volumeSize)
    {
        byte[] data = File.ReadAllBytes(zip);
        int eocd = data.AsSpan().LastIndexOf(new byte[] { 0x50, 0x4B, 0x05, 0x06 });
        int cdOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(eocd + 16));
        int cdSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(eocd + 12));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(eocd + 10));

        int fullVolumes = cdOffset / volumeSize;
        int lastStart = fullVolumes * volumeSize;

        int cursor = cdOffset;
        for (int i = 0; i < count; i++)
        {
            int absolute = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor + 42));
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(cursor + 34), (ushort)(absolute / volumeSize));
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(cursor + 42), (uint)(absolute % volumeSize));
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor + 28));
            int extraLength = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor + 30));
            int commentLength = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor + 32));
            cursor += 46 + nameLength + extraLength + commentLength;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(eocd + 4), (ushort)fullVolumes);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(eocd + 6), (ushort)fullVolumes);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(eocd + 16), (uint)(cdOffset - lastStart));

        for (int i = 0; i < fullVolumes; i++)
        {
            File.WriteAllBytes($"{baseStem}.z{i + 1:D2}", data.AsSpan(i * volumeSize, volumeSize).ToArray());
        }

        File.WriteAllBytes(baseStem + ".zip", data.AsSpan(lastStart).ToArray());
        Assert.True(cdSize > 0);
    }

    private static void AssertContents(string archivePath, byte[] a, byte[] b)
    {
        using var output = new TempDir();
        using IArchiveSource source = ArchiveService.Open(archivePath);
        Assert.Equal(ArchiveFormat.Zip, source.Format);
        Assert.Equal(2, source.Entries.Count);

        source.Extract([], new ExtractionOptions { DestinationDirectory = output.Path }, null, CancellationToken.None);

        string root = Directory.GetFiles(output.Path, "a.bin", SearchOption.AllDirectories).Single();
        string dir = Path.GetDirectoryName(root)!;
        Assert.True(a.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, "a.bin"))));
        Assert.True(b.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, "b.bin"))));
    }

    [Fact]
    public void Pkzip_split_opens_from_first_volume_and_from_last_zip()
    {
        using var dir = new TempDir();
        string zip = BuildZip(dir, out byte[] a, out byte[] b);
        string stem = dir.File("split");
        SplitPkzip(zip, stem, 16_000);

        Assert.True(File.Exists(stem + ".z01"));
        Assert.True(File.Exists(stem + ".z02"));

        AssertContents(stem + ".z01", a, b);
        AssertContents(stem + ".z02", a, b);
        AssertContents(stem + ".zip", a, b);
    }

    [Fact]
    public void Pkzip_split_passes_verify()
    {
        using var dir = new TempDir();
        string zip = BuildZip(dir, out _, out _);
        string stem = dir.File("split");
        SplitPkzip(zip, stem, 9_000);

        using var source = (ZipArchiveSource)ArchiveService.Open(stem + ".z01");
        Assert.True(source.IsSplit);
        Assert.True(source.VolumeCount > 2);
        Assert.Equal(2, source.Verify(null, CancellationToken.None));
    }

    [Fact]
    public void Raw_byte_split_numbered_volumes_open_from_first_volume()
    {
        using var dir = new TempDir();
        string zip = BuildZip(dir, out byte[] a, out byte[] b);
        byte[] data = File.ReadAllBytes(zip);

        const int size = 20_000;
        int volumes = (data.Length + size - 1) / size;
        for (int i = 0; i < volumes; i++)
        {
            int length = Math.Min(size, data.Length - i * size);
            File.WriteAllBytes(dir.File($"raw.zip.{i + 1:D3}"), data.AsSpan(i * size, length).ToArray());
        }

        AssertContents(dir.File("raw.zip.001"), a, b);
    }

    [Fact]
    public void Missing_middle_volume_gives_clear_error()
    {
        using var dir = new TempDir();
        string zip = BuildZip(dir, out _, out _);
        string stem = dir.File("split");
        SplitPkzip(zip, stem, 16_000);
        File.Delete(stem + ".z02");

        Assert.Throws<ArchiveException>(() => ArchiveService.Open(stem + ".zip"));
    }

    [Fact]
    public void Split_archive_rejects_modification()
    {
        using var dir = new TempDir();
        string zip = BuildZip(dir, out _, out _);
        string stem = dir.File("split");
        SplitPkzip(zip, stem, 16_000);

        Assert.Throws<UnsupportedArchiveException>(() =>
            ZipRewriter.DeleteEntries(stem + ".zip", [0], CancellationToken.None));
    }
}
