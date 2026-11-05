using System.Windows;
using WangZip.Core;

namespace WangZip.App.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        Localizer.Apply(this);
        VersionText.Text = Loc.F("왕집 (WangZip) {0}", UpdateService.CurrentVersion);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
