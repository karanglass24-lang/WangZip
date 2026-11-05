using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

/// <summary>
/// WinZip AES-256으로 만든 ZIP을 다시 열어 내용이 같은지 확인합니다.
/// (우리 리더 + 구조 검사. 외부 도구 호환은 pyzipper/7-Zip으로 별도 확인)
/// </summary>
public class EncryptedZipCreationTests
{
    private static byte[] RandomBytes(int size) => RandomNumberGenerator.GetBytes(size);

    private static void AssertPlaintext(RawZipEntry entry)
    {
        Assert.True(entry.IsEncrypted, $"암호화 플래그가 없습니다: {entry.DecodedName}");
        Assert.Equal(99, entry.Method);
        Assert.Equal((byte)3, entry.AesStrength);
        Assert.True(entry.RealMethod is 0 or 8);
        Assert.Equal(0u, entry.Crc);   // AE-2: CRC 필드 미사용
    }

    [Fact]
    public void Aes256_roundtrip_with_korean_names_and_binary()
    {
        using var work = new TempDir();

        string textPath = work.File("보고서.txt");
        File.WriteAllText(textPath, "왕집 암호 압축 테스트입니다.", new UTF8Encoding(false));

        byte[] payload = RandomBytes(256 * 1024);
        string binaryPath = work.File("data.bin");
        File.WriteAllBytes(binaryPath, payload);

        string emptyPath = work.File("empty.txt");
        File.WriteAllBytes(emptyPath, []);

        string zip = work.File("encrypted.zip");
        CreationResult created = ZipWriter.Create(
            zip,
            [textPath, binaryPath, emptyPath],
            new CreationOptions { Password = "한글암호!?#", Level = CompressionLevel.Optimal },
            null,
            CancellationToken.None);

        Assert.Equal(3, created.EntryCount);
        Assert.True(new FileInfo(zip).Length > 0);

        foreach (RawZipEntry entry in ZipCentralDirectory.Read(zip).Where(entry => !entry.IsDirectory))
        {
            AssertPlaintext(entry);
        }

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        string outDir = work.File("out");
        ExtractionResult result = source.Extract(
            [],
            new ExtractionOptions { DestinationDirectory = outDir, Password = "한글암호!?#" },
            null,
            CancellationToken.None);

        Assert.Equal(3, result.ExtractedFiles);
        Assert.Equal(
            new UTF8Encoding(false).GetBytes("왕집 암호 압축 테스트입니다."),
            File.ReadAllBytes(Path.Combine(outDir, "보고서.txt")));
        Assert.Equal(payload, File.ReadAllBytes(Path.Combine(outDir, "data.bin")));
        Assert.Empty(File.ReadAllBytes(Path.Combine(outDir, "empty.txt")));
    }

    [Fact]
    public void Aes256_store_level_roundtrip()
    {
        using var work = new TempDir();

        byte[] payload = RandomBytes(64 * 1024);
        string file = work.File("store.bin");
        File.WriteAllBytes(file, payload);

        string zip = work.File("store.zip");
        ZipWriter.Create(
            zip,
            [file],
            new CreationOptions { Password = "pw-store", Level = CompressionLevel.NoCompression },
            null,
            CancellationToken.None);

        RawZipEntry entry = Assert.Single(ZipCentralDirectory.Read(zip));
        AssertPlaintext(entry);
        Assert.Equal((ushort)0, entry.RealMethod);

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        string outDir = work.File("out");
        source.Extract(
            [],
            new ExtractionOptions { DestinationDirectory = outDir, Password = "pw-store" },
            null,
            CancellationToken.None);

        Assert.Equal(payload, File.ReadAllBytes(Path.Combine(outDir, "store.bin")));
    }

    [Fact]
    public void Aes256_wrong_password_is_rejected()
    {
        using var work = new TempDir();

        string file = work.File("secret.txt");
        File.WriteAllText(file, "비밀", new UTF8Encoding(false));

        string zip = work.File("secret.zip");
        ZipWriter.Create(
            zip,
            [file],
            new CreationOptions { Password = "correct-password" },
            null,
            CancellationToken.None);

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        var options = new ExtractionOptions
        {
            DestinationDirectory = work.File("out"),
            Password = "wrong-password",
        };

        Assert.Throws<EncryptedArchiveException>(
            () => source.Extract([], options, null, CancellationToken.None));
    }

    [Fact]
    public void Aes256_verify_accepts_password()
    {
        using var work = new TempDir();

        byte[] payload = RandomBytes(4096);
        string file = work.File("verify.bin");
        File.WriteAllBytes(file, payload);

        string zip = work.File("verify.zip");
        ZipWriter.Create(
            zip,
            [file],
            new CreationOptions { Password = "verify-password" },
            null,
            CancellationToken.None);

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        Assert.Equal(1, source.Verify(null, CancellationToken.None, "verify-password"));
        Assert.Throws<EncryptedArchiveException>(
            () => source.Verify(null, CancellationToken.None, "nope"));
    }

    [Fact]
    public void Cp949_names_with_password_roundtrip()
    {
        using var work = new TempDir();

        string folder = work.File("자료");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "메모.txt");
        File.WriteAllText(file, "CP949 이름 + 암호", new UTF8Encoding(false));

        string zip = work.File("cp949.zip");
        ZipWriter.Create(
            zip,
            [folder],
            new CreationOptions
            {
                Password = "cp949-password",
                NameEncoding = NameEncodingMode.Cp949,
            },
            null,
            CancellationToken.None);

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        string outDir = work.File("out");
        source.Extract(
            [],
            new ExtractionOptions { DestinationDirectory = outDir, Password = "cp949-password" },
            null,
            CancellationToken.None);

        Assert.Equal(
            new UTF8Encoding(false).GetBytes("CP949 이름 + 암호"),
            File.ReadAllBytes(Path.Combine(outDir, "자료", "메모.txt")));
    }

    [Fact]
    public void Empty_password_writes_plain_zip()
    {
        using var work = new TempDir();

        string file = work.File("plain.txt");
        File.WriteAllText(file, "그냥 ZIP", new UTF8Encoding(false));

        string zip = work.File("plain.zip");
        ZipWriter.Create(
            zip,
            [file],
            new CreationOptions { Password = string.Empty },
            null,
            CancellationToken.None);

        RawZipEntry entry = Assert.Single(ZipCentralDirectory.Read(zip));
        Assert.False(entry.IsEncrypted);
        Assert.NotEqual((ushort)99, entry.Method);
    }
}
