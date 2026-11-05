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

        if (!hasSelection)
        {
            AllRadio.IsChecked = true;
        }
    }

    public string DestinationDirectory => DestinationBox.Text.Trim();

    public OverwritePolicy Overwrite =>
        (OverwriteCombo.SelectedItem as OverwriteOption)?.Policy ?? OverwritePolicy.Overwrite;

    public bool ExtractAll => AllRadio.IsChecked == true;

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

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string destination = DestinationDirectory;
        if (string.IsNullOrWhiteSpace(destination))
        {
            MessageBox.Show(this, "압축을 풀 폴더를 입력해 주세요.", "WangZip", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Directory.CreateDirectory(destination);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "폴더를 만들 수 없습니다.\n" + exception.Message, "WangZip", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
