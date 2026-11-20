using System.IO;
using System.Text;
using System.Text.Json;

namespace WangZip.App;

/// <summary>
/// 응용 프로그램 사용자 설정. %LOCALAPPDATA%\WangZip\settings.json 파일로 유지합니다.
/// </summary>
public sealed class AppSettings
{
    /// <summary>표시 언어: "auto"(Windows 언어를 따름), "ko", "en". 바꾸면 다시 시작해야 적용됩니다.</summary>
    public string Language { get; set; } = "auto";

    /// <summary>압축 풀기가 성공하면 폴더를 자동으로 엽니다.</summary>
    public bool AutoOpenAfterExtract { get; set; } = true;

    /// <summary>압축 풀기 폴더 만들기: 0=그대로, 1=자동(최상위 폴더가 하나뿐이면 만들지 않음), 2=항상.</summary>
    public int SubfolderMode { get; set; } = 0;

    /// <summary>전체 압축 풀기에 성공하면 원본 압축 파일을 휴지통으로 보냅니다.</summary>
    public bool DeleteSourceAfterExtract { get; set; } = false;

    /// <summary>"빠른 압축 풀기" 목적지. 0=자동 폴더, 1=바탕 화면, 2=다운로드, 3=문서, 4=압축 파일이 있던 폴더</summary>
    public int QuickDestination { get; set; } = 0;

    /// <summary>환경설정 창에서 마지막으로 저장한 우클릭 메뉴 선택(참고용). 실제 상태는 레지스트리를 기준으로 합니다.</summary>
    public bool ShellMenuEnabled { get; set; } = true;

    /// <summary>실행 시 새 버전을 확인하고, 발견하면 자동으로 받아 설치합니다 (설치형만).</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>테마: 0=시스템 따라가기, 1=라이트, 2=다크.</summary>
    public int Theme { get; set; } = 0;

    /// <summary>창 위치·크기 (0이면 저장된 값 없음).</summary>
    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    public double TreeWidth { get; set; }
    /// <summary>목록 열 너비(크기·압축 크기·압축률·수정일·방식 순).</summary>
    public double[]? ColumnWidths { get; set; }

    /// <summary>최근 연 파일/폴더 경로 (최대 10개).</summary>
    public List<string> RecentPaths { get; set; } = [];

    private const int MaxRecentPaths = 10;

    /// <summary>파일/폴더를 최근 목록에 추가합니다.</summary>
    public void AddRecentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        path = Path.GetFullPath(path);

        var list = RecentPaths ?? [];
        list.Remove(path); // 중복 제거
        list.Insert(0, path); // 최신 순으로 앞에 추가
        if (list.Count > MaxRecentPaths) list.RemoveRange(MaxRecentPaths, list.Count - MaxRecentPaths);
        RecentPaths = list;
    }

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
            // 임시 파일에 쓴 뒤 교체해, 쓰는 도중 종료돼도 설정 파일이 깨지지 않게 합니다.
            string temp = FilePath + "." + Environment.ProcessId + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }), new UTF8Encoding(false));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch
        {
            // 설정 저장 실패는 동작을 막지 않습니다.
        }
    }
}
