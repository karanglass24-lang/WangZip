using System.IO.Compression;
using System.Text;
using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

public class KoreanEncodingTests
{
    [Fact]
    public void Cp949_names_are_detected_automatically()
    {
        using var dir = new TempDir();
        string zip = dir.File("cp949.zip");

        // 구형 Windows 도구가 만든 ZIP과 동일한 형태 (UTF-8 플래그 없음)
        TestArchives.CreateZip(
            zip,
            ["한글 문서.txt", "사진/강아지.png"],
            Encoding.GetEncoding(949));

        using ZipArchiveSource source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);

        var names = source.Entries.Select(entry => entry.Name).ToList();
        Assert.Contains("한글 문서.txt", names);
        Assert.Contains("사진/강아지.png", names);
        Assert.Contains("CP949", source.EncodingDescription);
    }

    [Fact]
    public void Utf8_flagged_names_are_detected()
    {
        using var dir = new TempDir();
        string zip = dir.File("utf8.zip");

        TestArchives.CreateZip(zip, ["한글 문서.txt"], nameEncoding: null);

        using ZipArchiveSource source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);

        Assert.Equal("한글 문서.txt", Assert.Single(source.Entries).Name);
        Assert.Contains("UTF-8", source.EncodingDescription);
    }

    [Fact]
    public void Mac_style_nfd_names_are_normalized_to_nfc()
    {
        using var dir = new TempDir();
        string zip = dir.File("nfd.zip");

        string nfdName = "한글".Normalize(NormalizationForm.FormD) + ".txt";
        TestArchives.CreateZip(zip, [nfdName], nameEncoding: null);

        using ZipArchiveSource source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);

        Assert.Equal("한글.txt", Assert.Single(source.Entries).Name);
    }

    [Fact]
    public void User_can_force_utf8_decoding()
    {
        using var dir = new TempDir();
        string zip = dir.File("cp949.zip");
        TestArchives.CreateZip(zip, ["한글.txt"], Encoding.GetEncoding(949));

        using ZipArchiveSource source = ZipArchiveSource.Open(zip, NameEncodingMode.Utf8);

        // CP949 바이트를 UTF-8로 강제 해석하면 원래 이름과 달라져야 합니다.
        Assert.NotEqual("한글.txt", Assert.Single(source.Entries).Name);
    }

    [Fact]
    public void Unicode_path_extra_field_has_priority()
    {
        using var dir = new TempDir();
        string zip = dir.File("unicode-extra.zip");

        // CP949로 기록한 뒤, 유니코드 경로 추가 필드(0x7075)를 직접 덧붙입니다.
        TestArchives.CreateZip(zip, ["임시.txt"], Encoding.GetEncoding(949));
        byte[] bytes = File.ReadAllBytes(zip);

        byte[] rawName = Encoding.GetEncoding(949).GetBytes("임시.txt");
        byte[] utf8Name = Encoding.UTF8.GetBytes("한글이름.txt");
        Assert.True(TryAddUnicodePathExtra(bytes, rawName, utf8Name, out byte[] patched));
        File.WriteAllBytes(zip, patched);

        using ZipArchiveSource source = ZipArchiveSource.Open(zip, NameEncodingMode.Auto);
        Assert.Equal("한글이름.txt", Assert.Single(source.Entries).Name);
    }

    private static bool TryAddUnicodePathExtra(byte[] zip, byte[] rawName, byte[] utf8Name, out byte[] patched)
    {
        patched = zip;

        // 중앙 디렉터리 항목을 찾아 extra field 끝에 0x7075를 추가합니다.
        for (int i = 0; i + 46 <= zip.Length; i++)
        {
            if (BitConverter.ToUInt32(zip, i) != 0x02014B50) continue;

            int nameLength = BitConverter.ToUInt16(zip, i + 28);
            int extraLength = BitConverter.ToUInt16(zip, i + 30);

            if (nameLength != rawName.Length || !zip.AsSpan(i + 46, nameLength).SequenceEqual(rawName)) continue;

            int extraStart = i + 46 + nameLength;
            int insertAt = extraStart + extraLength;

            uint crc = Safety.Crc32.Compute(rawName);
            var extra = new byte[4 + 5 + utf8Name.Length];
            BitConverter.GetBytes((ushort)0x7075).CopyTo(extra, 0);
            BitConverter.GetBytes((ushort)(5 + utf8Name.Length)).CopyTo(extra, 2);
            extra[4] = 1;
            BitConverter.GetBytes(crc).CopyTo(extra, 5);
            utf8Name.CopyTo(extra, 9);

            var output = new byte[zip.Length + extra.Length];
            zip.AsSpan(0, insertAt).CopyTo(output);
            extra.CopyTo(output, insertAt);
            zip.AsSpan(insertAt).CopyTo(output.AsSpan(insertAt + extra.Length));

            // extra 길이 갱신
            BitConverter.GetBytes((ushort)(extraLength + extra.Length)).CopyTo(output, i + 30);

            patched = output;
            return true;
        }

        return false;
    }
}
