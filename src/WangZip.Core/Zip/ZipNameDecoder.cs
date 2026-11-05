using System.Text;

namespace WangZip.Core.Zip;

/// <summary>
/// ZIP 파일명 인코딩 판별기.
///
/// 판별 순서:
///   1) Unicode Path 추가 필드(0x7075)  — 가장 신뢰도 높음
///   2) 일반 목적 플래그 bit 11 (UTF-8) — ZIP 스펙 표준
///   3) ASCII
///   4) 엄격한 UTF-8 해석 + 한글 포함 → UTF-8
///   5) 엄격한 CP949 해석 + 한글 포함 → CP949
///   6) 엄격한 UTF-8 → 7) CP949 → 8) CP437
///
/// 사용자가 Auto 대신 UTF-8/CP949를 직접 지정할 수 있습니다.
/// </summary>
internal static class ZipNameDecoder
{
    static ZipNameDecoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding Utf8Loose = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    private static readonly Encoding Cp949Strict = Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    private static readonly Encoding Cp949Loose = Encoding.GetEncoding(949);
    private static readonly Encoding Cp437 = Encoding.GetEncoding(437);

    public static string Decode(byte[] raw, bool flaggedUtf8, byte[]? unicodePath, NameEncodingMode mode, out string encodingName)
    {
        if (mode == NameEncodingMode.Raw)
        {
            encodingName = "원본 바이트";
            return Cp437.GetString(raw);
        }

        if (mode == NameEncodingMode.Utf8)
        {
            encodingName = "UTF-8(지정)";
            return Normalize(Utf8Loose.GetString(raw));
        }

        if (mode == NameEncodingMode.Cp949)
        {
            encodingName = "CP949(지정)";
            return Normalize(Cp949Loose.GetString(raw));
        }

        if (unicodePath is not null)
        {
            encodingName = "UTF-8(유니코드 필드)";
            return Normalize(Utf8Loose.GetString(unicodePath));
        }

        if (flaggedUtf8)
        {
            encodingName = "UTF-8";
            return Normalize(Utf8Loose.GetString(raw));
        }

        if (IsAscii(raw))
        {
            encodingName = "ASCII";
            return Normalize(Encoding.ASCII.GetString(raw));
        }

        if (TryDecode(Utf8Strict, raw, out string strictUtf8) && ContainsHangul(strictUtf8))
        {
            encodingName = "UTF-8(자동)";
            return Normalize(strictUtf8);
        }

        if (TryDecode(Cp949Strict, raw, out string strictCp949) && ContainsHangul(strictCp949))
        {
            encodingName = "CP949(자동)";
            return Normalize(strictCp949);
        }

        if (TryDecode(Utf8Strict, raw, out strictUtf8))
        {
            encodingName = "UTF-8(자동)";
            return Normalize(strictUtf8);
        }

        if (TryDecode(Cp949Strict, raw, out strictCp949))
        {
            encodingName = "CP949(자동)";
            return Normalize(strictCp949);
        }

        encodingName = "CP437(대체)";
        return Normalize(Cp437.GetString(raw));
    }

    public static string DecodeLegacy(byte[] raw) => Cp949Loose.GetString(raw);

    private static bool TryDecode(Encoding encoding, byte[] raw, out string text)
    {
        try
        {
            text = encoding.GetString(raw);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    private static bool IsAscii(byte[] raw)
    {
        foreach (byte b in raw)
        {
            if (b >= 0x80) return false;
        }

        return true;
    }

    private static bool ContainsHangul(string text)
    {
        foreach (char c in text)
        {
            if ((c >= 0xAC00 && c <= 0xD7A3) ||
                (c >= 0x1100 && c <= 0x11FF) ||
                (c >= 0x3130 && c <= 0x318F))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>macOS에서 만든 ZIP의 NFD(자모 분리) 한글을 NFC로 정규화하고 구분자를 통일합니다.</summary>
    private static string Normalize(string name)
    {
        string replaced = name.Replace('\\', '/');
        try
        {
            return replaced.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return replaced;
        }
    }
}
