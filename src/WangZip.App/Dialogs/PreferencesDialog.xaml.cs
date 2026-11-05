using System.Windows;
using System.Windows.Controls;

namespace WangZip.App.Dialogs;

public partial class PreferencesDialog : Window
{
    private readonly AppSettings _settings;
    private readonly List<CheckBox> _extensionBoxes = [];

    public PreferencesDialog(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        AutoOpenBox.IsChecked = settings.AutoOpenAfterExtract;
        AutoUpdateBox.IsChecked = settings.AutoUpdate;
        ShellMenuBox.IsChecked = ShellIntegration.IsShellMenuEnabled();

        foreach (string extension in ShellIntegration.AssociationExtensions)
        {
            var box = new CheckBox
            {
                Content = extension,
                Tag = extension,
                IsChecked = ShellIntegration.IsAssociated(extension),
                Width = 90,
                Margin = new Thickness(0, 0, 0, 6),
            };
            _extensionBoxes.Add(box);
            ExtensionPanel.Children.Add(box);
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) =>
        _extensionBoxes.ForEach(box => box.IsChecked = true);

    private void SelectNone_Click(object sender, RoutedEventArgs e) =>
        _extensionBoxes.ForEach(box => box.IsChecked = false);

    private void OpenDefaultApps_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellIntegration.OpenDefaultAppsSettings();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "왕집", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string exe = Environment.ProcessPath
                ?? throw new InvalidOperationException("실행 파일 경로를 알 수 없습니다.");
            if (string.Equals(System.IO.Path.GetFileName(exe), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("개발 중 실행(dotnet) 상태에서는 탐색기 등록을 변경할 수 없습니다.");
            }

            // 레지스트리를 먼저 적용하고, 성공했을 때만 설정 파일에 기록해 둘이 어긋나지 않게 합니다.
            bool shellMenu = ShellMenuBox.IsChecked == true;
            ShellIntegration.SetShellMenu(shellMenu, exe);
            ShellIntegration.SetAssociations(
                _extensionBoxes.Where(box => box.IsChecked == true).Select(box => (string)box.Tag).ToList(),
                exe);

            _settings.AutoOpenAfterExtract = AutoOpenBox.IsChecked == true;
            _settings.AutoUpdate = AutoUpdateBox.IsChecked == true;
            _settings.ShellMenuEnabled = shellMenu;
            _settings.Save();

            DialogResult = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "설정을 저장하지 못했습니다.\n" + exception.Message,
                "왕집", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
