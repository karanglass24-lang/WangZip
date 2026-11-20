using System.IO;
using System.Windows;
using WangZip.Core;

namespace WangZip.App;

public partial class App : Application
{
    private static readonly Dictionary<string, object> LightThemeResources = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // WANGZIP_LANG 환경 변수(ko/en)는 설정보다 우선합니다 (테스트용).
        Loc.Init(Environment.GetEnvironmentVariable("WANGZIP_LANG") ?? AppSettings.Load().Language);

        // 테마 적용
        ApplyTheme(AppSettings.Load().Theme);

        // 예기치 않은 예외가 프로그램을 바로 죽이지 않게 합니다.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                Loc.F("예상하지 못한 오류가 발생했습니다.\n{0}: {1}", args.Exception.GetType().Name, args.Exception.Message),
                Loc.T("왕집"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        // 탐색기 우클릭 메뉴 명령 모드 (here-extract / here-extract-all / here-create)
        if (e.Args.Length >= 2 &&
            e.Args[0].ToLowerInvariant() is "here-extract" or "here-extract-all" or "here-create")
        {
            if (!ShellCommands.TryRun(e.Args))
            {
                Shutdown();
            }
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        if (e.Args.Length > 0)
        {
            window.OpenFromCommandLine(e.Args[0]);
        }
    }

    /// <summary>전역 테마를 적용합니다. 0=시스템, 1=라이트, 2=다크.</summary>
    public static void ApplyTheme(int theme)
    {
        var dict = Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("Theme.xaml") == true);
        if (dict is null) return;

        bool isDark = theme switch
        {
            1 => false,  // 라이트 강제
            2 => true,   // 다크 강제
            _ => IsSystemDarkMode() // 시스템 설정 따라가기
        };

        SwapThemeResources(dict, isDark);
    }

    private static void SwapThemeResources(ResourceDictionary dict, bool isDark)
    {
        // 라이트/다크 브러시 쌍 교체
        var pairs = new (string Light, string Dark)[]
        {
            ("AccentBrush", "DarkAccentBrush"),
            ("AccentHoverBrush", "DarkAccentHoverBrush"),
            ("AccentSoftBrush", "DarkAccentSoftBrush"),
            ("AppBackground", "DarkAppBackground"),
            ("SurfaceBrush", "DarkSurfaceBrush"),
            ("BorderBrush", "DarkBorderBrush"),
            ("TextPrimaryBrush", "DarkTextPrimaryBrush"),
            ("TextSecondaryBrush", "DarkTextSecondaryBrush"),
            ("FolderIconBrush", "DarkFolderIconBrush"),
            ("TreeSelectedBrush", "DarkTreeSelectedBrush"),
            ("TreeHoverBrush", "DarkTreeHoverBrush"),
            ("EditorBackground", "DarkEditorBackground"),
            ("EditorForeground", "DarkEditorForeground"),
            ("MenuBackground", "DarkMenuBackground"),
            ("MenuBorder", "DarkMenuBorder"),
            ("ToolTipBackground", "DarkToolTipBackground"),
            ("ToolTipBorder", "DarkToolTipBorder"),
            ("ScrollBarBackground", "DarkScrollBarBackground"),
            ("ScrollBarForeground", "DarkScrollBarForeground"),
        };

        foreach (var (lightKey, darkKey) in pairs)
        {
            if (dict.Contains(lightKey) && dict.Contains(darkKey))
            {
                if (!LightThemeResources.ContainsKey(lightKey))
                    LightThemeResources[lightKey] = dict[lightKey];
                dict[lightKey] = isDark ? dict[darkKey] : LightThemeResources[lightKey];
            }
        }
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            // Windows 10 1809+ 레지스트리에서 시스템 테마 읽기
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

}
