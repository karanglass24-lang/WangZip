using System.Security.Cryptography;
using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

/// <summary>
/// 암호화된 ZIP 풀기 테스트.
/// EncryptedZipWriter가 PKWARE ZipCrypto와 WinZip AES를 표준대로 직접 기록하므로,
/// 여기서 통과하면 실제 도구(7-Zip, 알집, 반디집)가 만든 암호화 ZIP도 호환으로 볼 수 있습니다.
/// </summary>
public class EncryptedZipTests
{
    private static byte[] MakeTestBytes(int size) =>
        RandomNumberGenerator.GetBytes(size);

    private static string TemporaryZip(string testKey, byte[] data, params EncryptedZipWriter.TestEntry[] entries)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "wangzip-tests",
            $"{testKey}-{Guid.NewGuid():N}.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        EncryptedZipWriter.Write(path, entries);
        return path;
    }

    private static byte[] ReadExtracted(string directory, string relativeName) =>
        File.ReadAllBytes(Path.Combine(directory, relativeName));

    [Fact]
    public void ZipCrypto_store_and_deflate_extract_with_password()
    {
        byte[] data = MakeTestBytes(4096);
        string zip = TemporaryZip("zc", data,
            new EncryptedZipWriter.TestEntry("plain/hello.txt", "왕집 암호 테스트"u8.ToArray(), 0, "pwd123!"),
            new EncryptedZipWriter.TestEntry("deflated.bin", data, 8, "pwd123!"));

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        Assert.All(source.Entries, entry => Assert.True(entry.IsEncrypted));

        string dir = Path.Combine(Path.GetDirectoryName(zip)!, "out-zc");
        var options = new ExtractionOptions { DestinationDirectory = dir, Password = "pwd123!" };
        source.Extract([], options, null, CancellationToken.None);

        Assert.Equal("왕집 암호 테스트"u8.ToArray(), ReadExtracted(dir, Path.Combine("plain", "hello.txt")));
        Assert.Equal(data, ReadExtracted(dir, "deflated.bin"));
    }

    [Fact]
    public void ZipCrypto_wrong_password_is_rejected()
    {
        byte[] data = MakeTestBytes(1024);
        string zip = TemporaryZip("zcx", data,
            new EncryptedZipWriter.TestEntry("secret.txt", data, 0, "correct"));

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        var options = new ExtractionOptions
        {
            DestinationDirectory = Path.Combine(Path.GetDirectoryName(zip)!, "out-zcx"),
            Password = "wrong",
        };

        Assert.Throws<EncryptedArchiveException>(
            () => source.Extract([], options, null, CancellationToken.None));
    }

    [Fact]
    public void ZipCrypto_without_password_is_rejected_with_hint()
    {
        byte[] data = MakeTestBytes(256);
        string zip = TemporaryZip("zcn", data,
            new EncryptedZipWriter.TestEntry("a.txt", data, 8, "p"));

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        var options = new ExtractionOptions
        {
            DestinationDirectory = Path.Combine(Path.GetDirectoryName(zip)!, "out-zcn"),
        };

        var exception = Assert.Throws<EncryptedArchiveException>(
            () => source.Extract([], options, null, CancellationToken.None));
        Assert.Contains("비밀번호", exception.Message);
    }

    [Theory]
    [InlineData(3, 8)]   // AES-256 + DEFLATE
    [InlineData(1, 8)]   // AES-128 + DEFLATE
    [InlineData(3, 0)]   // AES-256 + STORE
    public void WinZip_aes_roundtrip(byte strength, ushort method)
    {
        byte[] data = MakeTestBytes(8192);
        string zip = TemporaryZip($"aes{strength}-{method}", data,
            new EncryptedZipWriter.TestEntry("aes/비밀파일.bin", data, method, "한글암호!?#", AesStrength: strength),
            new EncryptedZipWriter.TestEntry("aes/second.txt", "두 번째 항목"u8.ToArray(), method, "한글암호!?#", AesStrength: strength));

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        Assert.All(source.Entries, entry => Assert.Equal(99, entry.Method));

        string dir = Path.Combine(Path.GetDirectoryName(zip)!, "out-aes");
        var options = new ExtractionOptions { DestinationDirectory = dir, Password = "한글암호!?#" };
        var result = source.Extract([], options, null, CancellationToken.None);

        Assert.Equal(2, result.ExtractedFiles);
        Assert.Equal(data, ReadExtracted(dir, Path.Combine("aes", "비밀파일.bin")));
        Assert.Equal("두 번째 항목"u8.ToArray(), ReadExtracted(dir, Path.Combine("aes", "second.txt")));
    }

    [Fact]
    public void WinZip_aes_wrong_password_is_rejected()
    {
        byte[] data = MakeTestBytes(512);
        string zip = TemporaryZip("aesx", data,
            new EncryptedZipWriter.TestEntry("x.bin", data, 8, "real-password", AesStrength: 3));

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        var options = new ExtractionOptions
        {
            DestinationDirectory = Path.Combine(Path.GetDirectoryName(zip)!, "out-aesx"),
            Password = "nope",
        };

        Assert.Throws<EncryptedArchiveException>(
            () => source.Extract([], options, null, CancellationToken.None));
    }

    [Fact]
    public void Verify_with_password_checks_encrypted_entries()
    {
        byte[] data = MakeTestBytes(2048);
        string zip = TemporaryZip("vz", data,
            new EncryptedZipWriter.TestEntry("one.bin", data, 8, "pw"),
            new EncryptedZipWriter.TestEntry("two.bin", "data two"u8.ToArray(), 8, "pw"));

        using var source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);

        // 비밀번호를 주면 검사 통과
        string outDir = Path.Combine(Path.GetDirectoryName(zip)!, "unused");
        int checkedCount = source.Verify(null, CancellationToken.None, "pw");
        Assert.Equal(2, checkedCount);

        // 비밀번호가 없으면 안내 예외
        Assert.Throws<EncryptedArchiveException>(
            () => source.Verify(null, CancellationToken.None));
    }
}
