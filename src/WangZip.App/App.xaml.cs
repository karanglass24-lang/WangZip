using System.Windows;

namespace WangZip.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        if (e.Args.Length > 0)
        {
            window.OpenFromCommandLine(e.Args[0]);
        }
    }
}
