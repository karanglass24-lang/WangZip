using System.Globalization;

namespace WangZip.Core;

/// <summary>
/// 간단한 한국어/영어 번역. 코드의 한국어 원문이 곧 키입니다.
///   Loc.T("한국어")                       → 영어 모드에서는 EnglishStrings의 번역을, 아니면 원문을 돌려줍니다.
///   Loc.F("파일 {0}개", count)            → 번역한 형식 문자열에 값을 채웁니다.
/// 번역이 없는 문구는 한국어 그대로 표시됩니다.
/// </summary>
public static class Loc
{
    public static string Language { get; private set; } = "ko";

    public static bool IsEnglish => Language == "en";

    /// <summary>"auto"는 Windows 표시 언어(한국어면 한국어, 그 외 영어)를 따릅니다.</summary>
    public static void Init(string? setting)
    {
        string choice = (setting ?? "auto").Trim().ToLowerInvariant();
        if (choice is "ko" or "en")
        {
            Language = choice;
            return;
        }

        Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? "ko" : "en";
    }

    public static string T(string korean) =>
        IsEnglish && EnglishStrings.Map.TryGetValue(korean, out string? english) ? english : korean;

    public static string F(string korean, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(korean), args);
}
