using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WangZip.App.Dialogs;
using WangZip.App.ViewModels;
using WangZip.Core;

namespace WangZip.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private NameEncodingMode _encodingMode = NameEncodingMode.Auto;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = _viewModel;
        EncodingCombo.ItemsSource = MainViewModel.EncodingOptions;
        EncodingCombo.SelectedIndex = 0;
    }

    public void OpenFromCommandLine(string path)
    {
        if (File.Exists(path))
        {
            _ = OpenArchiveAsync(path);
        }
    }

    private async Task OpenArchiveAsync(string path)
    {
        if (_viewModel.IsBusy)
        {
            ShowInfo("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요.");
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            await _viewModel.OpenAsync(path, _encodingMode);
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async Task ChooseAndOpenAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "압축 파일 열기",
            Filter = "압축 파일|*.zip;*.7z;*.rar;*.tar;*.gz;*.tgz;*.bz2;*.xz;*.cab;*.iso;*.lzh;*.jar;*.apk|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) == true)
        {
            await OpenArchiveAsync(dialog.FileName);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e) => _ = ChooseAndOpenAsync();

    private async void Extract_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasArchive)
        {
            ShowInfo("먼저 압축 파일을 열어 주세요.");
            return;
        }

        if (_viewModel.IsBusy)
        {
            ShowInfo("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요.");
            return;
        }

        var dialog = new ExtractDialog(_viewModel.DefaultExtractDirectory, EntryGrid.SelectedItems.Count)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true) return;

        List<EntryRow> rows = dialog.ExtractAll
            ? _viewModel.Entries.ToList()
            : EntryGrid.SelectedItems.Cast<EntryRow>().ToList();

        if (rows.Count == 0)
        {
            ShowInfo("압축을 풀 항목을 선택해 주세요.");
            return;
        }

        string? password = null;
        if (rows.Any(row => row.Info.IsEncrypted))
        {
            var passwordDialog = new PasswordDialog { Owner = this };
            if (passwordDialog.ShowDialog() != true) return;
            password = passwordDialog.Password;
        }

        ExtractionResult? result = null;
        bool ok = await ProgressDialog.RunAsync(this, "압축을 푸는 중입니다", async (progress, token) =>
        {
            result = await _viewModel.ExtractAsync(
                rows,
                new ExtractionOptions
                {
                    DestinationDirectory = dialog.DestinationDirectory,
                    Overwrite = dialog.Overwrite,
                    Password = password,
                },
                progress,
                token);
        });

        if (!ok || result is null) return;

        string message =
            $"압축을 풀었습니다.\n\n" +
            $"파일 {result.ExtractedFiles:N0}개 · {Format.Bytes(result.TotalBytes)}\n" +
            $"폴더: {dialog.DestinationDirectory}";

        if (result.SkippedFiles > 0) message += $"\n건너뜀: {result.SkippedFiles:N0}개";
        if (result.Warnings.Count > 0) message += $"\n\n참고: 경고 {result.Warnings.Count}건";

        MessageBox.Show(this, message, "왕집", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            ShowInfo("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요.");
            return;
        }

        var dialog = new CreateDialog { Owner = this };
        await RunCreateAsync(dialog);
    }

    private async Task RunCreateAsync(CreateDialog dialog)
    {
        if (dialog.ShowDialog() != true) return;

        List<string> sources = dialog.Sources.ToList();
        CreationResult? created = null;

        bool ok = await ProgressDialog.RunAsync(this, "압축하는 중입니다", async (progress, token) =>
        {
            created = await _viewModel.CreateAsync(
                dialog.OutputPath,
                sources,
                dialog.Options,
                progress,
                token);
        });

        if (!ok || created is null) return;

        string message =
            $"압축 파일을 만들었습니다.\n\n" +
            $"{created.EntryCount:N0}개 항목 · {Format.Bytes(created.SourceBytes)} → {Format.Bytes(created.OutputBytes)} " +
            $"({Format.CompressionRate(created.Ratio)})\n{created.OutputPath}";

        MessageBox.Show(this, message, "왕집", MessageBoxButton.OK, MessageBoxImage.Information);

        await OpenArchiveAsync(created.OutputPath);
    }

    private async void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasArchive)
        {
            ShowInfo("먼저 압축 파일을 열어 주세요.");
            return;
        }

        if (_viewModel.IsBusy)
        {
            ShowInfo("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요.");
            return;
        }

        string? password = null;
        if (_viewModel.Entries.Any(row => row.Info.IsEncrypted))
        {
            var passwordDialog = new PasswordDialog { Owner = this };
            if (passwordDialog.ShowDialog() != true) return;
            password = passwordDialog.Password;
        }

        int checkedCount = 0;
        bool ok = await ProgressDialog.RunAsync(this, "무결성 검사 중입니다", async (progress, token) =>
        {
            checkedCount = await _viewModel.VerifyAsync(progress, token, password);
        });

        if (ok)
        {
            MessageBox.Show(
                this,
                $"정상입니다. {checkedCount:N0}개 항목의 CRC를 확인했습니다.",
                "WangZip",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ArchivePath is string path)
        {
            await OpenArchiveAsync(path);
        }
    }

    private async void Encoding_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (EncodingCombo.SelectedItem is not EncodingOption option) return;

        _encodingMode = option.Mode;

        if (_viewModel.HasArchive && _viewModel.ArchivePath is string path)
        {
            await OpenArchiveAsync(path);
        }
    }

    private async void EntryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntryGrid.SelectedItem is not EntryRow row || row.IsDirectory) return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            await _viewModel.PreviewAsync(row);
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async Task RunCreateFromPathsAsync(IReadOnlyList<string> paths)
    {
        var dialog = new CreateDialog(paths) { Owner = this };
        await RunCreateAsync(dialog);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;

        if (_viewModel.IsBusy)
        {
            ShowInfo("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요.");
            return;
        }

        if (paths.Length == 1 &&
            File.Exists(paths[0]) &&
            ArchiveService.DetectFormat(paths[0]) != ArchiveFormat.Unknown)
        {
            await OpenArchiveAsync(paths[0]);
            return;
        }

        await RunCreateFromPathsAsync(paths);
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool control = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (control && e.Key == Key.O)
        {
            e.Handled = true;
            await ChooseAndOpenAsync();
        }
        else if (control && e.Key == Key.E)
        {
            e.Handled = true;
            Extract_Click(sender, new RoutedEventArgs());
        }
        else if (control && e.Key == Key.N)
        {
            e.Handled = true;
            Create_Click(sender, new RoutedEventArgs());
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            Refresh_Click(sender, new RoutedEventArgs());
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutDialog { Owner = this }.ShowDialog();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.Dispose();
    }

    private void ShowInfo(string message) =>
        MessageBox.Show(this, message, "왕집", MessageBoxButton.OK, MessageBoxImage.Information);

    private void ShowError(Exception exception) =>
        MessageBox.Show(this, exception.Message, "왕집", MessageBoxButton.OK, MessageBoxImage.Warning);
}
