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
    private readonly AppSettings _settings = AppSettings.Load();
    private NameEncodingMode _encodingMode = NameEncodingMode.Auto;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = _viewModel;
        EncodingCombo.ItemsSource = MainViewModel.EncodingOptions;
        EncodingCombo.SelectedIndex = 0;
        _viewModel.CurrentMode = _encodingMode;

        QuickDestCombo.ItemsSource = MainViewModel.QuickDestinations;
        QuickDestCombo.SelectedIndex = Math.Clamp(_settings.QuickDestination, 0, MainViewModel.QuickDestinations.Count - 1);
        _viewModel.QuickDestinationIndex = QuickDestCombo.SelectedIndex;
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
            _viewModel.CurrentMode = _encodingMode;
            await _viewModel.OpenAsync(path, _encodingMode);
            ExpandTreeRoot();
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

    private void ExpandTreeRoot()
    {
        if (FolderTree.ItemsSource is System.Collections.ObjectModel.ObservableCollection<FolderNode> roots &&
            roots.Count > 0)
        {
            var container = FolderTree.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
            container?.UpdateLayout();
            container = FolderTree.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
            if (container is not null)
            {
                container.IsExpanded = true;
            }
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

    /// <summary>빠른 압축 풀기 (툴바 위치 설정 사용). 선택이 있으면 선택 항목만, 없으면 전체.</summary>
    private async Task QuickExtractInternalAsync(bool selectionOnly)
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

        bool selection = EntryGrid.SelectedItems.Count > 0;
        if (selectionOnly && !selection)
        {
            ShowInfo("목록에서 압축을 풀 항목을 선택해 주세요.");
            return;
        }

        List<EntryRow> rows = selection || selectionOnly
            ? EntryGrid.SelectedItems.Cast<EntryRow>().ToList()
            : _viewModel.Entries.ToList();

        string destination = _viewModel.ResolveQuickDestination();

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
                    DestinationDirectory = destination,
                    Overwrite = OverwritePolicy.Rename,
                    Password = password,
                },
                progress,
                token);
        });

        if (!ok || result is null) return;

        _viewModel.StatusText =
            $"압축을 풀었습니다: 파일 {result.ExtractedFiles:N0}개 → {destination}";

        MessageBox.Show(
            this,
            $"압축을 풀었습니다.\n\n파일 {result.ExtractedFiles:N0}개 · {Format.Bytes(result.TotalBytes)}\n{destination}",
            "왕집", MessageBoxButton.OK, MessageBoxImage.Information);

        if (_settings.AutoOpenAfterExtract && Directory.Exists(destination))
        {
            Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true });
        }
    }

    private void QuickExtractAll_Click(object sender, RoutedEventArgs e) => _ = QuickExtractInternalAsync(selectionOnly: false);

    private void QuickExtractSelected_Click(object sender, RoutedEventArgs e) => _ = QuickExtractInternalAsync(selectionOnly: true);

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

        if (ok)
        {
            _settings.AutoOpenAfterExtract = dialog.AutoOpenAfterExtract;
            _settings.Save();
        }

        if (!ok || result is null) return;

        string message =
            $"압축을 풀었습니다.\n\n" +
            $"파일 {result.ExtractedFiles:N0}개 · {Format.Bytes(result.TotalBytes)}\n" +
            $"폴더: {dialog.DestinationDirectory}";

        if (result.SkippedFiles > 0) message += $"\n건너뜀: {result.SkippedFiles:N0}개";

        MessageBox.Show(this, message, "왕집", MessageBoxButton.OK, MessageBoxImage.Information);

        if (dialog.AutoOpenAfterExtract && Directory.Exists(dialog.DestinationDirectory))
        {
            Process.Start(new ProcessStartInfo(dialog.DestinationDirectory) { UseShellExecute = true });
        }
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

        MessageBox.Show(
            this,
            $"압축 파일을 만들었습니다.\n\n" +
            $"{created.EntryCount:N0}개 항목 · {Format.Bytes(created.SourceBytes)} → {Format.Bytes(created.OutputBytes)} " +
            $"({Format.CompressionRate(created.Ratio)})\n{created.OutputPath}",
            "왕집", MessageBoxButton.OK, MessageBoxImage.Information);

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
                "왕집", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>열려 있는 ZIP에 파일/폴더 추가.</summary>
    private async void AddFiles_Click(object sender, RoutedEventArgs e)
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

        if (_viewModel.Entries.Count is int count && count > 0 &&
            ArchiveService.DetectFormat(_viewModel.ArchivePath!) != ArchiveFormat.Zip)
        {
            ShowInfo("항목 추가는 ZIP 형식에서만 지원합니다.");
            return;
        }

        var fileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "추가할 파일 선택 (여러 개 선택 가능)",
            Multiselect = true,
        };
        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "추가할 폴더 선택 (선택하지 않으려면 취소)",
            Multiselect = false,
        };

        var sources = new List<string>();
        if (fileDialog.ShowDialog(this) == true)
        {
            sources.AddRange(fileDialog.FileNames);
        }

        if (MessageBox.Show(this, "폴더도 추가하시겠습니까?", "왕집",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes &&
            folderDialog.ShowDialog(this) == true)
        {
            sources.Add(folderDialog.FolderName);
        }

        if (sources.Count == 0) return;

        bool ok = await ProgressDialog.RunAsync(this, "압축 파일에 항목 추가", async (progress, token) =>
        {
            var result = await _viewModel.AddToArchiveAsync(sources, progress, token);
            if (result.Warnings.Count > 0)
            {
                MessageBox.Show(this, string.Join("\n", result.Warnings.Take(8)), "왕집",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });

        if (ok)
        {
            MessageBox.Show(this, "항목을 추가했습니다.", "왕집", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>선택 항목을 ZIP에서 삭제.</summary>
    private async void Delete_Click(object sender, RoutedEventArgs e)
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

        if (EntryGrid.SelectedItems.Count == 0)
        {
            ShowInfo("삭제할 항목을 목록에서 선택해 주세요.");
            return;
        }

        var rows = EntryGrid.SelectedItems.Cast<EntryRow>().ToList();

        if (ArchiveService.DetectFormat(_viewModel.ArchivePath!) != ArchiveFormat.Zip)
        {
            ShowInfo("항목 삭제는 ZIP 형식에서만 지원합니다.");
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"선택한 {rows.Count:N0}개 항목을 압축 파일에서 삭제할까요?\n(삭제 후에는 되돌릴 수 없습니다)",
            "왕집", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.OK) return;

        bool ok = await ProgressDialog.RunAsync(this, "압축 파일에서 항목 삭제", async (_, token) =>
        {
            await _viewModel.DeleteFromArchiveAsync(rows, token);
        });

        if (ok)
        {
            MessageBox.Show(this, "항목을 삭제했습니다.", "왕집", MessageBoxButton.OK, MessageBoxImage.Information);
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
        _viewModel.CurrentMode = _encodingMode;

        if (_viewModel.HasArchive && _viewModel.ArchivePath is string path)
        {
            await OpenArchiveAsync(path);
        }
    }

    private void QuickDest_Changed(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.QuickDestinationIndex = QuickDestCombo.SelectedIndex;
        _settings.QuickDestination = QuickDestCombo.SelectedIndex;
        _settings.Save();
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderNode node)
        {
            _viewModel.SelectFolder(node.FullPath);
        }
    }

    private void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectFolder("");

        var container = FolderTree.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
        container?.SetValue(System.Windows.Controls.Primitives.Selector.IsSelectedProperty, true);
    }

    /// <summary>현재 폴더 대신 전체 항목목록 보기.</summary>
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
            await QuickExtractInternalAsync(selectionOnly: false);
        }
        else if (control && e.Key == Key.N)
        {
            e.Handled = true;
            Create_Click(sender, e);
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            Refresh_Click(sender, e);
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
