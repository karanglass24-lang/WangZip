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
        _viewModel.CurrentMode = _encodingMode;
        UpdateEncodingMenuChecks();

        RestoreWindowState();
        AutoUpdateMenu.IsChecked = _settings.AutoUpdate;
        Loaded += (_, _) => _ = RunUpdateCheckAsync(manual: false);
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
            string? password = null;
            while (true)
            {
                try
                {
                    await _viewModel.OpenAsync(path, _encodingMode, password);
                    break;
                }
                catch (EncryptedArchiveException) when (ArchiveService.DetectFormat(path) is ArchiveFormat.SevenZip or ArchiveFormat.Rar)
                {
                    // 파일 이름까지 암호화된 압축 파일은 목록을 읽으려면 비밀번호가 필요합니다.
                    Mouse.OverrideCursor = null;
                    var passwordDialog = new PasswordDialog { Owner = this };
                    if (passwordDialog.ShowDialog() != true) return;
                    password = passwordDialog.Password;
                    Mouse.OverrideCursor = Cursors.Wait;
                }
            }

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
                container.IsSelected = true;
            }
        }
    }

    private async Task ChooseAndOpenAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "압축 파일 열기",
            Filter = "압축 파일|*.zip;*.7z;*.rar;*.tar;*.gz;*.tgz;*.bz2;*.xz;*.jar;*.apk|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) == true)
        {
            await OpenArchiveAsync(dialog.FileName);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e) => _ = ChooseAndOpenAsync();

    /// <summary>압축 풀기 대화상자(대상 폴더 확인)를 띄우고 풉니다.
    /// selectionOnly=true면 선택 항목만을 대상으로 미리 지정됩니다.</summary>
    private async Task RunExtractDialogAsync(bool selectionOnly)
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

        var dialog = new ExtractDialog(_viewModel.DefaultExtractDirectory, EntryGrid.SelectedItems.Count, selectionOnly, _settings)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true) return;

        _settings.AutoOpenAfterExtract = dialog.AutoOpenAfterExtract;
        _settings.QuickDestination = dialog.SelectedQuickDestination;
        _settings.Save();

        List<EntryRow> rows = dialog.ExtractAll
            ? _viewModel.AllRows.Where(row => row.Info.Index >= 0).ToList()
            : EntryGrid.SelectedItems.Cast<EntryRow>().ToList();

        if (rows.Count == 0)
        {
            ShowInfo("압축을 풀 항목을 선택해 주세요.");
            return;
        }

        string? password = null;
        if (_viewModel.ExpandRows(rows).Any(row => row.Info.IsEncrypted))
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

        MessageBox.Show(this, message, "왕집", MessageBoxButton.OK, MessageBoxImage.Information);

        if (dialog.AutoOpenAfterExtract && Directory.Exists(dialog.DestinationDirectory))
        {
            Process.Start(new ProcessStartInfo(dialog.DestinationDirectory) { UseShellExecute = true });
        }
    }

    private void QuickExtractAll_Click(object sender, RoutedEventArgs e) => _ = RunExtractDialogAsync(false);

    private void QuickExtractSelected_Click(object sender, RoutedEventArgs e) => _ = RunExtractDialogAsync(true);

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

        var dialog = new ExtractDialog(_viewModel.DefaultExtractDirectory, EntryGrid.SelectedItems.Count, false, _settings)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true) return;

        List<EntryRow> rows = dialog.ExtractAll
            ? _viewModel.AllRows.Where(row => row.Info.Index >= 0).ToList()
            : EntryGrid.SelectedItems.Cast<EntryRow>().ToList();

        if (rows.Count == 0)
        {
            ShowInfo("압축을 풀 항목을 선택해 주세요.");
            return;
        }

        string? password = null;
        if (_viewModel.ExpandRows(rows).Any(row => row.Info.IsEncrypted))
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

        bool encrypted = !string.IsNullOrEmpty(dialog.Options.Password);

        MessageBox.Show(
            this,
            $"압축 파일을 만들었습니다.\n\n" +
            $"{created.EntryCount:N0}개 항목 · {Format.Bytes(created.SourceBytes)} → {Format.Bytes(created.OutputBytes)} " +
            $"({Format.CompressionRate(created.Ratio)})\n{created.OutputPath}" +
            (encrypted ? "\n\n암호: AES-256으로 보호됨" : string.Empty),
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
        if (_viewModel.ExpandRows(_viewModel.AllRows).Any(row => row.Info.IsEncrypted))
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

    private async void EncodingMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string tag) return;
        if (!Enum.TryParse(tag, out NameEncodingMode mode)) return;

        _encodingMode = mode;
        _viewModel.CurrentMode = mode;
        UpdateEncodingMenuChecks();

        if (_viewModel.HasArchive && _viewModel.ArchivePath is string path)
        {
            await OpenArchiveAsync(path);
        }
    }

    /// <summary>인코딩 메뉴는 항상 하나만 선택된 상태로 유지합니다.</summary>
    private void UpdateEncodingMenuChecks()
    {
        EncodingAutoMenu.IsChecked = _encodingMode == NameEncodingMode.Auto;
        EncodingUtf8Menu.IsChecked = _encodingMode == NameEncodingMode.Utf8;
        EncodingCp949Menu.IsChecked = _encodingMode == NameEncodingMode.Cp949;
        EncodingRawMenu.IsChecked = _encodingMode == NameEncodingMode.Raw;
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

    /// <summary>폴더를 두 번 누르면 그 폴더로 들어가고, 왼쪽 폴더 트리도 함께 펼칩니다.</summary>
    private void EntryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 열 머리글·스크롤바 더블클릭은 무시합니다.
        if (RowAt(e.OriginalSource as DependencyObject) is null) return;
        if (EntryGrid.SelectedItem is not EntryRow row) return;

        if (row.IsDirectory)
        {
            ClearSearch();
            NavigateIntoFolder(row.Info.Name.TrimEnd('/'));
            return;
        }

        _ = PreviewSelectedAsync(row);
    }

    private void GoUp_Click(object sender, RoutedEventArgs e) => NavigateIntoFolder(_viewModel.ParentFolder);

    private void NavigateIntoFolder(string folderPath)
    {
        _viewModel.SelectFolder(folderPath);
        SelectTreeNode(folderPath);
        EntryGrid.SelectedItems.Clear();
        EntryGrid.Focus();
    }

    /// <summary>지정한 경로의 폴더 노드까지 트리를 펼치고 선택합니다.</summary>
    private void SelectTreeNode(string folderPath, bool retried = false)
    {
        if (_viewModel.TreeRoots.Count == 0) return;

        var chain = new List<FolderNode>();
        if (!BuildNodeChain(_viewModel.TreeRoots[0], folderPath, chain)) return;

        ItemsControl parent = FolderTree;
        TreeViewItem? container = null;

        foreach (FolderNode node in chain)
        {
            parent.UpdateLayout();
            container = parent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
            if (container is null)
            {
                parent.UpdateLayout();
                container = parent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
            }

            if (container is null)
            {
                // 컨테이너가 아직 만들어지지 않았으면 레이아웃이 끝난 뒤 한 번만 다시 시도합니다.
                if (!retried)
                {
                    Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Loaded,
                        () => SelectTreeNode(folderPath, retried: true));
                }

                return;
            }

            container.IsExpanded = true;
            container.UpdateLayout();
            parent = container;
        }

        if (container is null) return;

        container.IsSelected = true;
        container.BringIntoView();
    }

    private static bool BuildNodeChain(FolderNode node, string target, List<FolderNode> chain)
    {
        chain.Add(node);

        if (string.Equals(node.FullPath, target, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (FolderNode child in node.Children)
        {
            if (BuildNodeChain(child, target, chain)) return true;
        }

        chain.RemoveAt(chain.Count - 1);
        return false;
    }

    private static readonly HashSet<string> RiskyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".scr", ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta",
        ".msi", ".msp", ".lnk", ".jar", ".reg", ".cpl", ".docm", ".xlsm", ".pptm",
    };

    private async Task PreviewSelectedAsync(EntryRow row)
    {
        if (RiskyExtensions.Contains(Path.GetExtension(row.Info.Name.TrimEnd('/'))) &&
            MessageBox.Show(this,
                $"'{row.Info.Name}'은(는) 실행되는 형식의 파일입니다.\n압축을 풀어 바로 실행하면 위험할 수 있습니다. 계속할까요?",
                "왕집", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

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

    /// <summary>왕집에서 끌어낸 임시 파일을 같은 창에 놓았을 때 무시하기 위한 표시.</summary>
    private bool _draggingOut;

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !_draggingOut && e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (_draggingOut) return;
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
            await RunExtractDialogAsync(selectionOnly: false);
        }
        else if (control && e.Key == Key.N)
        {
            e.Handled = true;
            Create_Click(sender, e);
        }
        else if (_viewModel.HasArchive && _viewModel.CanGoUp &&
                 (e.Key == Key.Back && Keyboard.FocusedElement is not System.Windows.Controls.TextBox ||
                  e.Key == Key.System && e.SystemKey == Key.Up))
        {
            e.Handled = true;
            GoUp_Click(sender, e);
        }
        else if (control && e.Key == Key.F)
        {
            e.Handled = true;
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
        else if (e.Key == Key.Escape && _viewModel.FilterText.Length > 0)
        {
            e.Handled = true;
            ClearSearch();
            EntryGrid.Focus();
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

    /// <summary>업데이트 확인(메뉴) — 직접 눌렀을 때.</summary>
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        await RunUpdateCheckAsync(manual: true);
    }

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PreferencesDialog(_settings) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            AutoUpdateMenu.IsChecked = _settings.AutoUpdate;
        }
    }

    private void AutoUpdateMenu_Click(object sender, RoutedEventArgs e)
    {
        _settings.AutoUpdate = AutoUpdateMenu.IsChecked == true;
        _settings.Save();
    }

    /// <summary>
    /// 최신 출시를 확인해 새 버전이면 안내하고 내려받아 설치합니다.
    /// manual=false(구동 시 자동 확인)는 자동 업데이트 설정이 켜져 있을 때만 동작합니다.
    /// </summary>
    private async Task RunUpdateCheckAsync(bool manual)
    {
        if (!manual && !_settings.AutoUpdate) return;

        UpdateInfo? info;
        try
        {
            // 시작할 때 자동으로 확인하는 경우에는 커서를 바꾸지 않습니다(배경에서 조용히 확인).
            if (manual) Mouse.OverrideCursor = Cursors.Wait;
            info = await UpdateService.CheckLatestAsync();
        }
        catch (Exception exception)
        {
            if (manual) ShowInfo("업데이트를 확인하지 못했습니다. (네트워크 상태를 확인해 주세요)\n" + exception.Message);
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        bool installed = File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

        if (info is null)
        {
            if (manual) ShowInfo("새 버전 정보를 찾지 못했습니다. (GitHub Releases 확인 필요)");
            return;
        }

        if (!UpdateService.IsNewer(info))
        {
            if (manual) ShowInfo($"최신 버전을 사용하고 있습니다. (현재 v{UpdateService.CurrentVersion})");
            return;
        }

        // 설치형이 아니면(포터블) 홈페이지로 안내합니다.
        if (!installed)
        {
            var open = MessageBox.Show(
                this,
                $"새 버전 v{info.LatestVersion}이(가) 있습니다.\n" +
                "포터블 버전은 홈페이지에서 새 ZIP을 받아 교체하시면 됩니다.\n\n" +
                "릴리스 페이지를 열까요?",
                "왕집 업데이트", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
            {
                UpdateService.OpenReleasePage();
            }
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"새 버전 v{info.LatestVersion}이(가) 있습니다. (현재 v{UpdateService.CurrentVersion})\n\n" +
            "다운로드 후 자동으로 설치하며,\n설치가 끝나면 왕집이 다시 실행됩니다.\n\n지금 업데이트할까요?",
            "왕집 업데이트", MessageBoxButton.OKCancel, MessageBoxImage.Information);

        if (answer != MessageBoxResult.OK) return;

        string? setupPath;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var progress = new Progress<double>(percent => _viewModel.StatusText = $"업데이트 내려받는 중: {percent:0}%");
            setupPath = await UpdateService.DownloadAsync(info, progress, System.Threading.CancellationToken.None);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "업데이트 내려받기를 실패했습니다.\n" + exception.Message, "왕집",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        MessageBox.Show(
            this,
            "다운로드가 끝났습니다. 설치 프로그램을 실행합니다.\n" +
            "(설치되는 동안 왕집이 잠시 종료되고, 다시 실행됩니다.)",
            "왕집 업데이트", MessageBoxButton.OK, MessageBoxImage.Information);

        _viewModel.Dispose();   // 파일 잠금 해제
        UpdateService.InstallAndRestart(setupPath);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 작업 중에 닫으면 출력 파일이 손상되거나 임시 파일이 남으므로 막습니다.
        if (_viewModel.IsBusy)
        {
            ShowInfo("작업이 진행 중입니다. 작업이 끝난 뒤 닫아 주세요.");
            e.Cancel = true;
            return;
        }

        SaveWindowState();
        _viewModel.Dispose();
    }

    private void RestoreWindowState()
    {
        if (_settings.TreeWidth >= 120) TreeColumn.Width = new GridLength(_settings.TreeWidth);

        if (_settings.ColumnWidths is { Length: 5 } widths)
        {
            for (int i = 0; i < 5; i++)
            {
                if (widths[i] >= 40) EntryGrid.Columns[i + 1].Width = new DataGridLength(widths[i]);
            }
        }

        if (_settings.WindowWidth >= MinWidth && _settings.WindowHeight >= MinHeight)
        {
            double left = _settings.WindowLeft, top = _settings.WindowTop;
            double width = Math.Min(_settings.WindowWidth, SystemParameters.VirtualScreenWidth);
            double height = Math.Min(_settings.WindowHeight, SystemParameters.VirtualScreenHeight);

            // 화면 밖으로 나간 위치(모니터 구성 변경)는 무시합니다.
            bool visible = left + width > SystemParameters.VirtualScreenLeft + 80 &&
                           left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80 &&
                           top >= SystemParameters.VirtualScreenTop - 10 &&
                           top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 80;
            if (visible)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
            }

            Width = width;
            Height = height;
        }

        if (_settings.WindowMaximized) WindowState = WindowState.Maximized;
    }

    private void SaveWindowState()
    {
        Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        _settings.WindowLeft = bounds.Left;
        _settings.WindowTop = bounds.Top;
        _settings.WindowWidth = bounds.Width;
        _settings.WindowHeight = bounds.Height;
        // 최소화 상태로 닫으면 최대화 여부를 알 수 없으므로 이전 값을 유지합니다.
        if (WindowState != WindowState.Minimized) _settings.WindowMaximized = WindowState == WindowState.Maximized;
        _settings.TreeWidth = TreeColumn.ActualWidth;
        _settings.ColumnWidths = Enumerable.Range(1, 5).Select(i => EntryGrid.Columns[i].ActualWidth).ToArray();
        _settings.Save();
    }

    // ───────── 이동 막대 / 정렬 / 검색 ─────────

    private void Crumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path }) return;
        ClearSearch();
        NavigateIntoFolder(path);
    }

    private void ClearSearch()
    {
        if (_viewModel.FilterText.Length > 0) _viewModel.FilterText = "";
    }

    private void EntryGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        string? key = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(key)) return;

        _viewModel.SortBy(key);
        foreach (DataGridColumn column in EntryGrid.Columns) column.SortDirection = null;
        e.Column.SortDirection = _viewModel.SortAscending
            ? System.ComponentModel.ListSortDirection.Ascending
            : System.ComponentModel.ListSortDirection.Descending;
    }

    // ───────── 우클릭 메뉴 / 끌어서 내보내기 ─────────

    private Point _dragStart;
    private bool _dragCandidate;

    private static DataGridRow? RowAt(DependencyObject? source)
    {
        while (source is not null && source is not DataGridRow)
        {
            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return source as DataGridRow;
    }

    private void EntryGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        DataGridRow? row = RowAt(e.OriginalSource as DependencyObject);
        if (row is not null && !row.IsSelected)
        {
            EntryGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }
    }

    private void ContextOpen_Click(object sender, RoutedEventArgs e)
    {
        if (EntryGrid.SelectedItems.Count != 1 || EntryGrid.SelectedItem is not EntryRow row) return;

        if (row.IsDirectory)
        {
            ClearSearch();
            NavigateIntoFolder(row.Info.Name.TrimEnd('/'));
        }
        else
        {
            _ = PreviewSelectedAsync(row);
        }
    }

    private void EntryGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DataGridRow? row = RowAt(e.OriginalSource as DependencyObject);
        _dragCandidate = row is not null && row.IsSelected && e.ClickCount == 1;
        _dragStart = e.GetPosition(null);
    }

    private void EntryGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragCandidate || e.LeftButton != MouseButtonState.Pressed) return;

        Vector delta = e.GetPosition(null) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragCandidate = false;
        StartDragOut();
    }

    /// <summary>선택 항목을 임시 폴더로 풀어 탐색기 등으로 끌어 놓을 수 있게 합니다.</summary>
    private void StartDragOut()
    {
        if (!_viewModel.HasArchive || _viewModel.IsBusy) return;

        var selected = EntryGrid.SelectedItems.Cast<EntryRow>().ToList();
        if (selected.Count == 0) return;

        List<EntryRow> expanded = _viewModel.ExpandRows(selected);
        if (expanded.Any(row => row.Info.IsEncrypted))
        {
            ShowInfo("암호가 걸린 항목은 끌어서 내보낼 수 없습니다.\n'압축 풀기'를 사용해 주세요.");
            return;
        }

        long total = expanded.Sum(row => row.Info.Size);
        if (total > 500L * 1024 * 1024)
        {
            ShowInfo("끌어서 내보내기는 500MB 이하만 지원합니다.\n'압축 풀기'를 사용해 주세요.");
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            List<string> paths = _viewModel.ExtractToTemp(selected);
            Mouse.OverrideCursor = null;
            if (paths.Count == 0) return;

            var data = new DataObject(DataFormats.FileDrop, paths.ToArray());
            _draggingOut = true;
            try { DragDrop.DoDragDrop(EntryGrid, data, DragDropEffects.Copy); }
            finally { _draggingOut = false; }
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


    private void ShowInfo(string message) =>
        MessageBox.Show(this, message, "왕집", MessageBoxButton.OK, MessageBoxImage.Information);

    private void ShowError(Exception exception) =>
        MessageBox.Show(this, exception.Message, "왕집", MessageBoxButton.OK, MessageBoxImage.Warning);
}
