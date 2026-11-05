using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WangZip.Core;
using WangZip.App.ViewModels;

namespace WangZip.App.Dialogs;

public partial class ExtractDialog : Window
{
    private sealed record OverwriteOption(string Label, OverwritePolicy Policy);

    public ExtractDialog(string defaultDestination, int selectedCount, bool preferSelection = false)
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

        // 빠른 위치 콤보: 설정에 저장된 기본 위치를 반영합니다.
        var quickOptions = MainViewModel.QuickDestinations
            .Concat([new QuickDestination("직접 입력 (경로 지정)", null, "")])
            .ToList();
        QuickDestCombo.ItemsSource = quickOptions;
        int remembered = new AppSettings().QuickDestination;
        QuickDestCombo.SelectedIndex = Math.Clamp(remembered, 0, quickOptions.Count - 1);
        _selectedQuick = QuickDestCombo.SelectedIndex;

        bool hasSelection = selectedCount > 0;
        SelectedOnlyRadio.IsEnabled = hasSelection;
        AllRadio.IsChecked = true;

        if (preferSelection && hasSelection)
        {
            SelectedOnlyRadio.IsChecked = true;
            AllRadio.IsChecked = false;
        }

        SelectedCountText.Text = hasSelection
            ? $"선택한 {selectedCount}개 항목만 압축 풀기"
            : "선택한 항목이 없습니다 (전체 압축 풀기만 가능)";

        AutoOpenBox.IsChecked = new AppSettings().AutoOpenAfterExtract;
    }

    private int _selectedQuick;

    public string DestinationDirectory => DestinationBox.Text.Trim();

    public OverwritePolicy Overwrite =>
        (OverwriteCombo.SelectedItem as OverwriteOption)?.Policy ?? OverwritePolicy.Overwrite;

    public bool ExtractAll => AllRadio.IsChecked == true;

    /// <summary>성공 후 대상 폴더를 열지 여부 (설정에 저장됩니다).</summary>
    public bool AutoOpenAfterExtract => AutoOpenBox.IsChecked == true;

    /// <summary>"빠른 위치" 콤보 선택 (설정에 저장됩니다). 마지막 항목은 직접 입력.</summary>
    public int SelectedQuickDestination => _selectedQuick;

    private void QuickDest_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (QuickDestCombo.SelectedItem is not QuickDestination option) return;

        _selectedQuick = QuickDestCombo.SelectedIndex;

        if (option.Resolve is { } resolver)
        {
            string path = resolver();
            if (Directory.Exists(path))
            {
                DestinationBox.Text = path;
            }
        }
    }

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
