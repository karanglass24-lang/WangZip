using System.Windows;

namespace WangZip.App.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        VersionText.Text = $"왕집 (WangZip) {UpdateService.CurrentVersion}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
