using System.IO;
using System.Text;
using System.Text.Json;

namespace WangZip.App;

/// <summary>
/// 응용 프로그램 사용자 설정. %LOCALAPPDATA%\WangZip\settings.json 파일로 유지합니다.
/// </summary>
public sealed class AppSettings
{
    /// <summary>압축 풀기가 성공하면 폴더를 자동으로 엽니다.</summary>
    public bool AutoOpenAfterExtract { get; set; } = true;

    /// <summary>"빠른 압축 풀기" 목적지. 0=자동 폴더, 1=바탕 화면, 2=다운로드, 3=문서, 4=압축 파일이 있던 폴더</summary>
    public int QuickDestination { get; set; } = 0;

    /// <summary>탐색기 우클릭 메뉴 노출 여부(설치 시 기록). 실제 등록은 설치 프로그램이 합니다.</summary>
    public bool ShellMenuEnabled { get; set; } = true;

    public static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WangZip",
            "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
        }
        catch
        {
            // 설정 파일 손상 시 기본값 사용
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }), new UTF8Encoding(false));
        }
        catch
        {
            // 설정 저장 실패는 동작을 막지 않습니다.
        }
    }
}
