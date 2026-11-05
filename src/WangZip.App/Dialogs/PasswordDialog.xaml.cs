using System.Windows;
using System.Windows.Controls;
using WangZip.Core;

namespace WangZip.App.Dialogs;

public partial class PasswordDialog : Window
{
    public PasswordDialog()
    {
        InitializeComponent();
        Localizer.Apply(this);
        Loaded += (_, _) => PasswordInput.Focus();
    }

    public string Password => RevealBox.IsVisible ? RevealBox.Text : PasswordInput.Password;

    private void ShowPassword_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (ShowPasswordBox.IsChecked == true)
        {
            RevealBox.Text = PasswordInput.Password;
            PasswordInput.Visibility = Visibility.Collapsed;
            RevealBox.Visibility = Visibility.Visible;
        }
        else
        {
            PasswordInput.Password = RevealBox.Text;
            RevealBox.Visibility = Visibility.Collapsed;
            PasswordInput.Visibility = Visibility.Visible;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Password))
        {
            MessageBox.Show(this, Loc.T("비밀번호를 입력해 주세요."), Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
