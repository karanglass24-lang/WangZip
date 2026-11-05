using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WangZip.Core;

namespace WangZip.App.Dialogs;

public partial class ExtractDialog : Window
{
    private sealed record OverwriteOption(string Label, OverwritePolicy Policy);

    public ExtractDialog(string defaultDestination, int selectedCount)
    {
        InitializeComponent();

        DestinationBox.Text = defaultDestination;

        OverwriteCombo.ItemsSource = new[]
        {
            new OverwriteOption("덮어쓰기", OverwritePolicy.Overwrite),
            new OverwriteOption("건너뛰기", OverwritePolicy.Skip),
            new OverwriteOption("이름 바꾸기", OverwritePolicy.Rename),
        };
        OverwriteCombo.SelectedIndex = 0;

        bool hasSelection = selectedCount > 0;
        SelectedOnlyRadio.IsEnabled = hasSelection;
        AllRadio.IsChecked = true;

        SelectedCountText.Text = hasSelection
            ? $"선택한 {selectedCount}개 항목만 압축 풀기"
            : "선택한 항목이 없습니다 (전체 압축 풀기만 가능)";

        AutoOpenBox.IsChecked = new AppSettings().AutoOpenAfterExtract;
    }

    public string DestinationDirectory => DestinationBox.Text.Trim();

    public OverwritePolicy Overwrite =>
        (OverwriteCombo.SelectedItem as OverwriteOption)?.Policy ?? OverwritePolicy.Overwrite;

    public bool ExtractAll => AllRadio.IsChecked == true;

    /// <summary>성공 후 대상 폴더를 열지 여부 (설정에 저장됩니다).</summary>
    public bool AutoOpenAfterExtract => AutoOpenBox.IsChecked == true;

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "압축을 풀 폴더 선택",
        };

        if (dialog.ShowDialog(this) == true)
        {
            DestinationBox.Text = dialog.FolderName;
        }
    }

    private void QuickLocation_Click(object sender, RoutedEventArgs e)
    {
        string folder = ((sender as Button)?.Tag as string) switch
        {
            "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "Downloads" => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            _ => string.Empty,
        };

        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            DestinationBox.Text = folder;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string destination = DestinationDirectory;
        if (string.IsNullOrWhiteSpace(destination))
        {
            MessageBox.Show(this, "압축을 풀 폴더를 입력해 주세요.", "왕집", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Directory.CreateDirectory(destination);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "폴더를 만들 수 없습니다.\n" + exception.Message, "왕집", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
