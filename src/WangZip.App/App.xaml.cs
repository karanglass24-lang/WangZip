using System.Windows;
using WangZip.Core;

namespace WangZip.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // WANGZIP_LANG 환경 변수(ko/en)는 설정보다 우선합니다 (테스트용).
        Loc.Init(Environment.GetEnvironmentVariable("WANGZIP_LANG") ?? AppSettings.Load().Language);

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

        // 탐색기 우클릭 메뉴 명령 모드 (here-extract / here-create)
        if (e.Args.Length >= 2 &&
            e.Args[0].ToLowerInvariant() is "here-extract" or "here-create")
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
}
