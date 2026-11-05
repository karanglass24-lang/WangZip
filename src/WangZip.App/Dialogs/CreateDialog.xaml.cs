using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WangZip.Core;

namespace WangZip.App.Dialogs;

public partial class CreateDialog : Window
{
    private sealed record LevelOption(string Label, CompressionLevel Level);
    private sealed record EncodingOption(string Label, NameEncodingMode Mode);

    public CreateDialog(IEnumerable<string>? initialSources = null)
    {
        InitializeComponent();

        LevelCombo.ItemsSource = new[]
        {
            new LevelOption("저장 (압축 안 함)", CompressionLevel.NoCompression),
            new LevelOption("빠르게", CompressionLevel.Fastest),
            new LevelOption("보통 (권장)", CompressionLevel.Optimal),
            new LevelOption("최대 압축 (느림)", CompressionLevel.SmallestSize),
        };
        LevelCombo.SelectedIndex = 2;

        EncodingCombo.ItemsSource = new[]
        {
            new EncodingOption("UTF-8 (권장, 현대 도구 호환)", NameEncodingMode.Auto),
            new EncodingOption("CP949 (구형 Windows 호환)", NameEncodingMode.Cp949),
        };
        EncodingCombo.SelectedIndex = 0;

        if (initialSources is not null)
        {
            foreach (string source in initialSources)
            {
                Sources.Add(source);
            }
        }

        UpdateSuggestedOutput();
    }

    public ObservableCollection<string> Sources { get; } = [];

    public string OutputPath => OutputBox.Text.Trim();

    public CreationOptions Options => new()
    {
        Level = (LevelCombo.SelectedItem as LevelOption)?.Level ?? CompressionLevel.Optimal,
        NameEncoding = (EncodingCombo.SelectedItem as EncodingOption)?.Mode ?? NameEncodingMode.Auto,
        IncludeEmptyDirectories = true,
    };

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "압축할 파일 선택",
            Multiselect = true,
            Filter = "모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true) return;

        foreach (string file in dialog.FileNames)
        {
            if (!Sources.Contains(file)) Sources.Add(file);
        }

        UpdateSuggestedOutput();
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "압축할 폴더 선택",
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        foreach (string folder in dialog.FolderNames)
        {
            if (!Sources.Contains(folder)) Sources.Add(folder);
        }

        UpdateSuggestedOutput();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        foreach (string item in SourceList.SelectedItems.Cast<string>().ToList())
        {
            Sources.Remove(item);
        }
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "압축 파일 저장",
            Filter = "ZIP 압축 파일 (*.zip)|*.zip|모든 파일 (*.*)|*.*",
            DefaultExt = ".zip",
            AddExtension = true,
            FileName = Path.GetFileName(OutputBox.Text),
            InitialDirectory = SafeDirectory(Path.GetDirectoryName(OutputBox.Text)),
        };

        if (dialog.ShowDialog(this) == true)
        {
            OutputBox.Text = dialog.FileName;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Sources.Count == 0)
        {
            MessageBox.Show(this, "압축할 파일이나 폴더를 추가해 주세요.", "WangZip", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string output = OutputPath;
        if (string.IsNullOrWhiteSpace(output))
        {
            MessageBox.Show(this, "저장할 파일 경로를 입력해 주세요.", "WangZip", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!Path.HasExtension(output)) output += ".zip";
        OutputBox.Text = output;

        try
        {
            string? directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "저장 경로를 만들 수 없습니다.\n" + exception.Message, "WangZip", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void UpdateSuggestedOutput()
    {
        if (!string.IsNullOrWhiteSpace(OutputBox.Text)) return;
        if (Sources.Count == 0) return;

        string first = Sources[0];
        string directory = (File.Exists(first) ? Path.GetDirectoryName(first) : Path.GetDirectoryName(first.TrimEnd('\\')))
            ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string name = Path.GetFileNameWithoutExtension(first.TrimEnd('\\'));

        OutputBox.Text = Path.Combine(directory, name + ".zip");
    }

    private static string SafeDirectory(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
            ? path
            : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
}
