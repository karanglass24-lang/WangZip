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
    internal sealed record LevelOption(string Label, CompressionLevel Level);
    internal sealed record EncodingOption(string Label, NameEncodingMode Mode);
    internal sealed record SplitOption(string Label, long Bytes);
    internal sealed record FormatOption(string Label, CreateFormat Format);

    internal static FormatOption[] FormatOptions() =>
    [
        new FormatOption(Loc.T("ZIP (권장, 암호·분할 지원)"), CreateFormat.Zip),
        new FormatOption(Loc.T("7z (높은 압축률)"), CreateFormat.SevenZip),
        new FormatOption("TAR", CreateFormat.Tar),
        new FormatOption("TAR.GZ", CreateFormat.TarGz),
        new FormatOption("TAR.BZ2", CreateFormat.TarBz2),
    ];

    // 환경설정 창의 기본값 선택에서도 같은 목록(같은 순서·문구)을 씁니다.
    internal static LevelOption[] LevelOptions() =>
    [
        new LevelOption(Loc.T("저장 (압축 안 함)"), CompressionLevel.NoCompression),
        new LevelOption(Loc.T("빠르게"), CompressionLevel.Fastest),
        new LevelOption(Loc.T("보통 (권장)"), CompressionLevel.Optimal),
        new LevelOption(Loc.T("최대 압축 (느림)"), CompressionLevel.SmallestSize),
    ];

    internal static EncodingOption[] EncodingOptions() =>
    [
        new EncodingOption(Loc.T("UTF-8 (권장, 현대 도구 호환)"), NameEncodingMode.Auto),
        new EncodingOption(Loc.T("CP949 (구형 Windows 호환)"), NameEncodingMode.Cp949),
    ];

    internal static SplitOption[] SplitOptions() =>
    [
        new SplitOption(Loc.T("나누지 않음"), 0),
        new SplitOption("10 MB", 10L * 1024 * 1024),
        new SplitOption("100 MB", 100L * 1024 * 1024),
        new SplitOption("650 MB (CD)", 650L * 1024 * 1024),
        new SplitOption("700 MB (CD)", 700L * 1024 * 1024),
        new SplitOption("1 GB", 1024L * 1024 * 1024),
        new SplitOption("4 GB (FAT32)", 4000L * 1024 * 1024),
    ];

    public CreateDialog(IEnumerable<string>? initialSources = null, AppSettings? settings = null)
    {
        InitializeComponent();
        Localizer.Apply(this);
        settings ??= AppSettings.Load();

        LevelOption[] levels = LevelOptions();
        LevelCombo.ItemsSource = levels;
        LevelCombo.SelectedIndex = Math.Clamp(settings.DefaultCompressionLevel, 0, levels.Length - 1);

        EncodingOption[] encodings = EncodingOptions();
        EncodingCombo.ItemsSource = encodings;
        EncodingCombo.SelectedIndex = Math.Clamp(settings.DefaultNameEncoding, 0, encodings.Length - 1);

        SplitOption[] splits = SplitOptions();
        SplitCombo.ItemsSource = splits;
        SplitCombo.SelectedIndex = Math.Clamp(settings.DefaultSplitSizeIndex, 0, splits.Length - 1);

        FormatCombo.ItemsSource = FormatOptions();
        FormatCombo.SelectedIndex = 0;

        if (initialSources is not null)
        {
            foreach (string source in initialSources)
            {
                Sources.Add(source);
            }
        }

        UpdateSuggestedOutput();

        // 처음 포커스: 대상이 있으면 저장 위치, 없으면 '파일 추가' 단추.
        Loaded += (_, _) =>
        {
            if (Sources.Count > 0)
            {
                OutputBox.Focus();
                OutputBox.CaretIndex = OutputBox.Text.Length;
            }
            else
            {
                AddFilesButton.Focus();
            }
        };
    }

    public ObservableCollection<string> Sources { get; } = [];

    public string OutputPath => OutputBox.Text.Trim();

    /// <summary>"암호 걸기"가 켜져 있을 때만 값이 있습니다.</summary>
    public string? Password => UsePasswordCheck.IsChecked == true ? PasswordBox.Password : null;

    public CreateFormat SelectedFormat => (FormatCombo.SelectedItem as FormatOption)?.Format ?? CreateFormat.Zip;

    public CreationOptions Options => new()
    {
        Format = SelectedFormat,
        Level = (LevelCombo.SelectedItem as LevelOption)?.Level ?? CompressionLevel.Optimal,
        NameEncoding = (EncodingCombo.SelectedItem as EncodingOption)?.Mode ?? NameEncodingMode.Auto,
        IncludeEmptyDirectories = true,
        Password = SelectedFormat == CreateFormat.Zip ? Password : null,
        SplitSizeBytes = SelectedFormat == CreateFormat.Zip ? (SplitCombo.SelectedItem as SplitOption)?.Bytes ?? 0 : 0,
    };

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("압축할 파일 선택"),
            Multiselect = true,
            Filter = Loc.T("모든 파일 (*.*)|*.*"),
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
            Title = Loc.T("압축할 폴더 선택"),
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
            Title = Loc.T("압축 파일 저장"),
            Filter = Loc.T("ZIP 압축 파일 (*.zip)|*.zip|7z 압축 파일 (*.7z)|*.7z|TAR (*.tar)|*.tar|TAR.GZ (*.tar.gz)|*.tar.gz|TAR.BZ2 (*.tar.bz2)|*.tar.bz2|모든 파일 (*.*)|*.*"),
            FilterIndex = FormatCombo.SelectedIndex + 1,
            DefaultExt = CreateFormats.Extension(SelectedFormat),
            AddExtension = true,
            FileName = Path.GetFileName(OutputBox.Text),
            InitialDirectory = SafeDirectory(Path.GetDirectoryName(OutputBox.Text)),
        };

        if (dialog.ShowDialog(this) == true)
        {
            OutputBox.Text = dialog.FileName;
            CreateFormat picked = CreateFormats.FromPath(dialog.FileName);
            int index = Array.FindIndex(FormatOptions(), option => option.Format == picked);
            if (index >= 0) FormatCombo.SelectedIndex = index;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Sources.Count == 0)
        {
            MessageBox.Show(this, Loc.T("압축할 파일이나 폴더를 추가해 주세요."), Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string output = OutputPath;
        if (string.IsNullOrWhiteSpace(output))
        {
            MessageBox.Show(this, Loc.T("저장할 파일 경로를 입력해 주세요."), Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!Path.HasExtension(output) || CreateFormats.FromPath(output) != SelectedFormat)
        {
            output = CreateFormats.ChangeExtension(output, SelectedFormat);
        }

        OutputBox.Text = output;

        if (SelectedFormat == CreateFormat.Zip && UsePasswordCheck.IsChecked == true)
        {
            if (string.IsNullOrEmpty(PasswordBox.Password))
            {
                MessageBox.Show(this, Loc.T("비밀번호를 입력해 주세요."), Loc.T("왕집"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                PasswordBox.Focus();
                return;
            }

            if (!string.Equals(PasswordBox.Password, PasswordConfirmBox.Password, StringComparison.Ordinal))
            {
                MessageBox.Show(this, Loc.T("비밀번호가 서로 다릅니다. 다시 확인해 주세요."), Loc.T("왕집"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                PasswordConfirmBox.Focus();
                return;
            }
        }

        try
        {
            string? directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, Loc.T("저장 경로를 만들 수 없습니다.\n") + exception.Message, Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (File.Exists(output) &&
            MessageBox.Show(this, Loc.F("같은 이름의 파일이 이미 있습니다. 덮어쓸까요?\n\n{0}", output), Loc.T("왕집"),
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void UsePassword_Changed(object sender, RoutedEventArgs e)
    {
        if (PasswordPanel is null) return;

        PasswordPanel.IsEnabled = UsePasswordCheck.IsChecked == true;
        if (UsePasswordCheck.IsChecked == true)
        {
            PasswordBox.Focus();
        }
    }

    private void Format_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SplitCombo is null || SecurityPanel is null || OutputBox is null) return;

        bool zip = SelectedFormat == CreateFormat.Zip;
        SplitCombo.IsEnabled = zip;
        EncodingCombo.IsEnabled = zip;
        SecurityPanel.IsEnabled = zip;
        SecurityPanel.Opacity = zip ? 1 : 0.5;
        if (!zip) UsePasswordCheck.IsChecked = false;

        if (!string.IsNullOrWhiteSpace(OutputBox.Text))
        {
            OutputBox.Text = CreateFormats.ChangeExtension(OutputBox.Text.Trim(), SelectedFormat);
        }
    }

    private void UpdateSuggestedOutput()
    {
        if (!string.IsNullOrWhiteSpace(OutputBox.Text)) return;
        if (Sources.Count == 0) return;

        string first = Sources[0];
        string directory = (File.Exists(first) ? Path.GetDirectoryName(first) : Path.GetDirectoryName(first.TrimEnd('\\')))
            ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string name = File.Exists(first)
            ? Path.GetFileNameWithoutExtension(first)
            : Path.GetFileName(first.TrimEnd('\\'));

        OutputBox.Text = Path.Combine(directory, name + CreateFormats.Extension(SelectedFormat));
    }

    private static string SafeDirectory(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
            ? path
            : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
}
