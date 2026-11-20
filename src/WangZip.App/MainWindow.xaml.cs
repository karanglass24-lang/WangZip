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
        Localizer.Apply(this);

        DataContext = _viewModel;

        // 열 때 파일명 인코딩 기본값 (환경설정). 메뉴에서 바꾼 값은 이 창을 닫을 때까지만 유지합니다.
        _encodingMode = Enum.IsDefined(typeof(NameEncodingMode), _settings.DefaultListEncoding)
            ? (NameEncodingMode)_settings.DefaultListEncoding
            : NameEncodingMode.Auto;
        _viewModel.CurrentMode = _encodingMode;
        UpdateEncodingMenuChecks();
        UpdateThemeMenuChecks();

        RestoreWindowState();
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
            ShowInfo(Loc.T("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요."));
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
                    _settings.AddRecentPath(path);
                    _settings.Save();
                    UpdateRecentFilesMenu();
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
            Title = Loc.T("압축 파일 열기"),
            Filter = Loc.T("압축 파일|*.zip;*.7z;*.rar;*.tar;*.gz;*.tgz;*.bz2;*.xz;*.jar;*.apk;*.z01;*.001|모든 파일 (*.*)|*.*"),
        };

        if (dialog.ShowDialog(this) == true)
        {
            await OpenArchiveAsync(dialog.FileName);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e) => _ = ChooseAndOpenAsync();

    /// <summary>압축 풀기 대화상자(대상 폴더 확인)를 띄우고 풉니다.
    /// selectionOnly=true면 선택 항목만을 대상으로 미리 지정됩니다.</summary>
    private async Task RunExtractDialogAsync(bool selectionOnly, IReadOnlyList<EntryRow>? explicitRows = null)
    {
        if (!_viewModel.HasArchive)
        {
            ShowInfo(Loc.T("먼저 압축 파일을 열어 주세요."));
            return;
        }

        if (_viewModel.IsBusy)
        {
            ShowInfo(Loc.T("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요."));
            return;
        }

        // explicitRows: 폴더 트리 우클릭처럼 목록 선택과 무관하게 대상을 직접 지정할 때 씁니다.
        IReadOnlyList<EntryRow> selectedRows = explicitRows ?? EntryGrid.SelectedItems.Cast<EntryRow>().ToList();
        bool selection = selectedRows.Count > 0;
        if (selectionOnly && !selection)
        {
            ShowInfo(Loc.T("목록에서 압축을 풀 항목을 선택해 주세요."));
            return;
        }

        var dialog = new ExtractDialog(_viewModel.DefaultExtractDirectory, selectedRows.Count, selectionOnly, _settings)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true) return;

        _settings.AutoOpenAfterExtract = dialog.AutoOpenAfterExtract;
        _settings.SubfolderMode = (int)dialog.Subfolder;
        _settings.DeleteSourceAfterExtract = dialog.DeleteSourceAfterExtract;
        _settings.QuickDestination = dialog.SelectedQuickDestination;
        _settings.Save();

        List<EntryRow> rows = dialog.ExtractAll
            ? _viewModel.AllRows.Where(row => row.Info.Index >= 0).ToList()
            : selectedRows.ToList();

        if (rows.Count == 0)
        {
            ShowInfo(Loc.T("압축을 풀 항목을 선택해 주세요."));
            return;
        }

        string? password = null;
        if (_viewModel.ExpandRows(rows).Any(row => row.Info.IsEncrypted))
        {
            var passwordDialog = new PasswordDialog { Owner = this };
            if (passwordDialog.ShowDialog() != true) return;
            password = passwordDialog.Password;
        }

        string destination = _viewModel.ResolveExtractDestination(rows, dialog.DestinationDirectory, dialog.Subfolder);
        string? sourcePath = _viewModel.ArchivePath;

        ExtractionResult? result = null;
        bool ok = await ProgressDialog.RunAsync(this, Loc.T("압축을 푸는 중입니다"), async (progress, token) =>
        {
            result = await _viewModel.ExtractAsync(
                rows,
                new ExtractionOptions
                {
                    DestinationDirectory = destination,
                    Overwrite = dialog.Overwrite,
                    Password = password,
                },
                progress,
                token);
        });

        if (!ok || result is null) return;

        string message =
            Loc.F("압축을 풀었습니다.\n\n파일 {0:N0}개 · {1}\n폴더: {2}", result.ExtractedFiles, Format.Bytes(result.TotalBytes), destination);

        if (result.SkippedFiles > 0) message += Loc.F("\n건너뜀: {0:N0}개", result.SkippedFiles);

        if (dialog.DeleteSourceAfterExtract && dialog.ExtractAll && sourcePath is not null)
        {
            message += TryRecycleSource(sourcePath, result);
        }

        // 건너뜀·경고가 있으면 완료 안내를 끄더라도 알려 줍니다.
        bool hasIssues = result.SkippedFiles > 0 || result.Warnings.Count > 0;
        ShowExtractDone(message, destination, dialog.AutoOpenAfterExtract, hasIssues);
    }

    /// <summary>전부 문제없이 풀렸을 때만 원본을 휴지통으로 보냅니다. 결과 안내 문구를 돌려줍니다.</summary>
    private static string TryRecycleSource(string path, ExtractionResult result)
    {
        if (result.SkippedFiles > 0 || result.Warnings.Count > 0)
        {
            return "\n" + Loc.T("건너뛴 항목이나 경고가 있어 원본 압축 파일은 그대로 두었습니다.");
        }

        if (!File.Exists(path)) return "";

        string extension = Path.GetExtension(path);
        if (System.Text.RegularExpressions.Regex.IsMatch(extension, @"^\.(z\d{2,}|\d{3,})$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return "\n" + Loc.T("분할 압축 파일은 원본을 자동으로 삭제하지 않습니다.");
        }

        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            return "\n" + Loc.T("원본 압축 파일을 휴지통으로 보냈습니다.");
        }
        catch (Exception exception)
        {
            return "\n" + Loc.F("원본 압축 파일을 휴지통으로 보내지 못했습니다: {0}", exception.Message);
        }
    }

    /// <summary>완료 안내. 자동 열기가 꺼져 있으면 '폴더 열기'를 물어봅니다.</summary>
    private void ShowExtractDone(string message, string folder, bool autoOpen, bool forceShow)
    {
        bool exists = Directory.Exists(folder);
        bool open = false;
        if (!_settings.ShowCompletionDialog && !forceShow)
        {
            // 완료 안내를 끈 경우: 묻지 않고, 자동 열기가 켜져 있을 때만 폴더를 엽니다.
            open = exists && autoOpen;
        }
        else if (exists && !autoOpen)
        {
            open = MessageBox.Show(this, message + "\n\n" + Loc.T("압축을 푼 폴더를 열까요?"), Loc.T("왕집"),
                MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes;
        }
        else
        {
            MessageBox.Show(this, message, Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);
            open = exists && autoOpen;
        }

        if (open) Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private void QuickExtractAll_Click(object sender, RoutedEventArgs e) => _ = RunExtractDialogAsync(false);

    private void QuickExtractSelected_Click(object sender, RoutedEventArgs e) => _ = RunExtractDialogAsync(true);

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            ShowInfo(Loc.T("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요."));
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

        bool ok = await ProgressDialog.RunAsync(this, Loc.T("압축하는 중입니다"), async (progress, token) =>
        {
            created = await _viewModel.CreateAsync(
                dialog.OutputPath,
                sources,
                dialog.Options,
                progress,
                token);
        });

        if (!ok || created is null) return;

        // 소스 폴더를 최근 경로에 추가
        if (sources.Count > 0 && Directory.Exists(sources[0]))
        {
            _settings.AddRecentPath(sources[0]);
            _settings.Save();
            UpdateRecentFilesMenu();
        }

        bool encrypted = !string.IsNullOrEmpty(dialog.Options.Password);

        MessageBox.Show(
            this,
            Loc.F("압축 파일을 만들었습니다.\n\n{0:N0}개 항목 · {1} → {2} ({3})\n{4}", created.EntryCount, Format.Bytes(created.SourceBytes), Format.Bytes(created.OutputBytes), Format.CompressionRate(created.Ratio), created.OutputPath) +
            (encrypted ? Loc.T("\n\n암호: AES-256으로 보호됨") : string.Empty),
            Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);

        await OpenArchiveAsync(created.OutputPath);
    }

    private async void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasArchive)
        {
            ShowInfo(Loc.T("먼저 압축 파일을 열어 주세요."));
            return;
        }

        if (_viewModel.IsBusy)
        {
            ShowInfo(Loc.T("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요."));
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
        bool ok = await ProgressDialog.RunAsync(this, Loc.T("무결성 검사 중입니다"), async (progress, token) =>
        {
            checkedCount = await _viewModel.VerifyAsync(progress, token, password);
        });

        if (ok)
        {
            MessageBox.Show(
                this,
                Loc.F("정상입니다. {0:N0}개 항목의 CRC를 확인했습니다.", checkedCount),
                Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>항목 추가가 가능한 상태인지 확인하고, 안 되면 이유를 알립니다.</summary>
    private bool CheckCanAddItems()
    {
        if (!_viewModel.HasArchive)
        {
            ShowInfo(Loc.T("먼저 압축 파일을 열어 주세요."));
            return false;
        }

        if (_viewModel.IsBusy)
        {
            ShowInfo(Loc.T("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요."));
            return false;
        }

        if (!_viewModel.CanModify)
        {
            ShowInfo(Loc.T("항목 추가는 ZIP 형식에서만 지원합니다."));
            return false;
        }

        return true;
    }

    /// <summary>열려 있는 ZIP에 파일 추가.</summary>
    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckCanAddItems()) return;

        var fileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.T("추가할 파일 선택 (여러 개 선택 가능)"),
            Multiselect = true,
        };

        if (fileDialog.ShowDialog(this) != true || fileDialog.FileNames.Length == 0) return;

        await AddSourcesToArchiveAsync(fileDialog.FileNames);
    }

    /// <summary>열려 있는 ZIP에 폴더 추가.</summary>
    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckCanAddItems()) return;

        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Loc.T("추가할 폴더 선택"),
            Multiselect = false,
        };

        if (folderDialog.ShowDialog(this) != true || string.IsNullOrEmpty(folderDialog.FolderName)) return;

        await AddSourcesToArchiveAsync([folderDialog.FolderName]);
    }

    private async Task AddSourcesToArchiveAsync(IReadOnlyList<string> sources)
    {
        bool ok = await ProgressDialog.RunAsync(this, Loc.T("압축 파일에 항목 추가"), async (progress, token) =>
        {
            var result = await _viewModel.AddToArchiveAsync(sources, progress, token);
            if (result.Warnings.Count > 0)
            {
                MessageBox.Show(this, string.Join("\n", result.Warnings.Take(8)), Loc.T("왕집"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });

        if (ok && _settings.ShowCompletionDialog)
        {
            MessageBox.Show(this, Loc.T("항목을 추가했습니다."), Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>선택 항목을 ZIP에서 삭제.</summary>
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasArchive)
        {
            ShowInfo(Loc.T("먼저 압축 파일을 열어 주세요."));
            return;
        }

        if (_viewModel.IsBusy)
        {
            ShowInfo(Loc.T("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요."));
            return;
        }

        if (EntryGrid.SelectedItems.Count == 0)
        {
            ShowInfo(Loc.T("삭제할 항목을 목록에서 선택해 주세요."));
            return;
        }

        var rows = EntryGrid.SelectedItems.Cast<EntryRow>().ToList();

        if (!_viewModel.CanModify)
        {
            ShowInfo(Loc.T("항목 삭제는 ZIP 형식에서만 지원합니다."));
            return;
        }

        if (_settings.ConfirmBeforeDelete)
        {
            var confirm = MessageBox.Show(
                this,
                Loc.F("선택한 {0:N0}개 항목을 압축 파일에서 삭제할까요?\n(삭제 후에는 되돌릴 수 없습니다)", rows.Count),
                Loc.T("왕집"), MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.OK) return;
        }

        bool ok = await ProgressDialog.RunAsync(this, Loc.T("압축 파일에서 항목 삭제"), async (_, token) =>
        {
            await _viewModel.DeleteFromArchiveAsync(rows, token);
        });

        if (ok && _settings.ShowCompletionDialog)
        {
            MessageBox.Show(this, Loc.T("항목을 삭제했습니다."), Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void ThemeMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string tag) return;
        if (!int.TryParse(tag, out int theme)) return;

        _settings.Theme = theme;
        _settings.Save();
        App.ApplyTheme(theme);
        UpdateThemeMenuChecks();
    }

    private void UpdateThemeMenuChecks()
    {
        ThemeSystemMenu.IsChecked = _settings.Theme == 0;
        ThemeLightMenu.IsChecked = _settings.Theme == 1;
        ThemeDarkMenu.IsChecked = _settings.Theme == 2;
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

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".ini", ".cfg", ".conf", ".config", ".json", ".xml", ".yaml", ".yml",
        ".csv", ".tsv", ".md", ".markdown", ".html", ".htm", ".css", ".js", ".ts", ".jsx", ".tsx",
        ".py", ".cs", ".vb", ".java", ".cpp", ".c", ".h", ".hpp", ".go", ".rs", ".php", ".rb",
        ".pl", ".sh", ".bat", ".cmd", ".ps1", ".sql", ".ini", ".properties", ".toml",
        ".dockerfile", ".gitignore", ".gitattributes", ".editorconfig", ".license", ".readme",
        ".changelog", ".authors", ".contributors", ".todo", ".note", ".nfo", ".diz"
    };

    private async Task PreviewSelectedAsync(EntryRow row)
    {
        string ext = Path.GetExtension(row.Info.Name.TrimEnd('/'));

        // 위험한 확장자 경고
        if (RiskyExtensions.Contains(ext) &&
            MessageBox.Show(this,
                Loc.F("'{0}'은(는) 실행되는 형식의 파일입니다.\n압축을 풀어 바로 실행하면 위험할 수 있습니다. 계속할까요?", row.Info.Name),
                Loc.T("왕집"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        // 텍스트 파일은 내부 뷰어로 바로 미리보기
        if (TextExtensions.Contains(ext))
        {
            await PreviewTextFileAsync(row);
            return;
        }

        // 그 외는 기존 방식(임시 추출 후 외부 프로그램으로 열기)
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

    /// <summary>텍스트 파일을 임시 추출하여 PreviewDialog로 미리보기합니다.</summary>
    private async Task PreviewTextFileAsync(EntryRow row)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            // 임시 파일에 추출
            string? tempFile = await Task.Run(() => _viewModel.ExtractSingleToTemp(row));

            if (string.IsNullOrEmpty(tempFile) || !File.Exists(tempFile))
            {
                ShowInfo(Loc.T("파일을 추출할 수 없습니다."));
                return;
            }

            Mouse.OverrideCursor = null;

            // PreviewDialog 표시 (모달리스)
            var dialog = new PreviewDialog(tempFile, row.Info.Name) { Owner = this };
            dialog.Show();
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

        // 놓을 수 있는 동안만 안내 오버레이를 보여 줍니다 (화면 표시만 바뀝니다).
        DropOverlay.Visibility = e.Effects == DragDropEffects.Copy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        // 자식 요소 사이를 지날 때도 발생하지만, 창 안이면 곧 DragOver가 다시 보여 줍니다.
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (_draggingOut) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;

        if (_viewModel.IsBusy)
        {
            ShowInfo(Loc.T("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요."));
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
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        bool textInput = Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase;
        // 목록에 포커스가 있고 글 입력 중이 아닐 때만 Del/Enter를 처리합니다.
        bool gridFocus = EntryGrid.IsKeyboardFocusWithin && !textInput;
        bool busy = _viewModel.IsBusy;

        if (control && e.Key == Key.O)
        {
            e.Handled = true;
            await ChooseAndOpenAsync();
        }
        else if (control && e.Key == Key.E)
        {
            e.Handled = true;
            await RunExtractDialogAsync(selectionOnly: shift);
        }
        else if (control && e.Key == Key.N)
        {
            e.Handled = true;
            Create_Click(sender, e);
        }
        else if (control && e.Key == Key.W && !busy)
        {
            if (_viewModel.HasArchive)
            {
                e.Handled = true;
                CloseArchive_Click(sender, e);
            }
        }
        else if (control && e.Key == Key.OemComma && !busy)
        {
            e.Handled = true;
            Preferences_Click(sender, e);
        }
        else if (e.Key == Key.Delete && gridFocus && !busy && _viewModel.CanDelete)
        {
            e.Handled = true;
            Delete_Click(sender, e);
        }
        else if (e.Key == Key.Enter && gridFocus && !busy && EntryGrid.SelectedItems.Count == 1)
        {
            e.Handled = true;
            ContextOpen_Click(sender, e);
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
            FocusSearchBox();
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
        bool saved = dialog.ShowDialog() == true;
        if (saved) UpdateThemeMenuChecks();

        // '최근 목록 지우기'는 저장 여부와 상관없이 바로 적용되므로 항상 갱신합니다.
        UpdateRecentFilesMenu();
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
            if (manual) ShowInfo(Loc.T("업데이트를 확인하지 못했습니다. (네트워크 상태를 확인해 주세요)\n") + exception.Message);
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        bool installed = File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

        if (info is null)
        {
            if (manual) ShowInfo(Loc.T("새 버전 정보를 찾지 못했습니다. (GitHub Releases 확인 필요)"));
            return;
        }

        if (!UpdateService.IsNewer(info))
        {
            if (manual) ShowInfo(Loc.F("최신 버전을 사용하고 있습니다. (현재 v{0})", UpdateService.CurrentVersion));
            return;
        }

        // 설치형이 아니면(포터블) 홈페이지로 안내합니다.
        if (!installed)
        {
            var open = MessageBox.Show(
                this,
                Loc.F("새 버전 v{0}이(가) 있습니다.\n포터블 버전은 홈페이지에서 새 ZIP을 받아 교체하시면 됩니다.\n\n릴리스 페이지를 열까요?", info.LatestVersion),
                Loc.T("왕집 업데이트"), MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
            {
                UpdateService.OpenReleasePage();
            }
            return;
        }

        var answer = MessageBox.Show(
            this,
            Loc.F("새 버전 v{0}이(가) 있습니다. (현재 v{1})\n\n다운로드 후 자동으로 설치하며,\n설치가 끝나면 왕집이 다시 실행됩니다.\n\n지금 업데이트할까요?", info.LatestVersion, UpdateService.CurrentVersion),
            Loc.T("왕집 업데이트"), MessageBoxButton.OKCancel, MessageBoxImage.Information);

        if (answer != MessageBoxResult.OK) return;

        string? setupPath;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var progress = new Progress<double>(percent => _viewModel.StatusText = Loc.F("업데이트 내려받는 중: {0:0}%", percent));
            setupPath = await UpdateService.DownloadAsync(info, progress, System.Threading.CancellationToken.None);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, Loc.T("업데이트 내려받기를 실패했습니다.\n") + exception.Message, Loc.T("왕집"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        MessageBox.Show(
            this,
            Loc.T("다운로드가 끝났습니다. 설치 프로그램을 실행합니다.\n(설치되는 동안 왕집이 잠시 종료되고, 다시 실행됩니다.)"),
            Loc.T("왕집 업데이트"), MessageBoxButton.OK, MessageBoxImage.Information);

        _viewModel.Dispose();   // 파일 잠금 해제
        UpdateService.InstallAndRestart(setupPath);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 작업 중에 닫으면 출력 파일이 손상되거나 임시 파일이 남으므로 막습니다.
        if (_viewModel.IsBusy)
        {
            ShowInfo(Loc.T("작업이 진행 중입니다. 작업이 끝난 뒤 닫아 주세요."));
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

        UpdateRecentFilesMenu();
    }

    private void UpdateRecentFilesMenu()
    {
        RecentFilesMenu.Items.Clear();

        // 없어진 경로는 건너뜁니다. 남은 항목이 없으면 비활성 안내 항목만 보여 줍니다.
        foreach (string path in _settings.RecentPaths ?? [])
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                var item = new MenuItem
                {
                    // 메뉴 머리글에서 '_'는 단축키 표시이므로 '__'로 이스케이프합니다.
                    Header = TruncatePath(path, 60).Replace("_", "__"),
                    ToolTip = path,
                    Tag = path
                };
                item.Click += (_, _) => OpenRecentPath(path);
                RecentFilesMenu.Items.Add(item);
            }
        }

        if (RecentFilesMenu.Items.Count == 0)
        {
            RecentFilesMenu.IsEnabled = false;
            RecentFilesMenu.Items.Add(new MenuItem { Header = Loc.T("(최근 파일 없음)"), IsEnabled = false });
        }
        else
        {
            RecentFilesMenu.IsEnabled = true;
            RecentFilesMenu.Items.Add(new Separator());
            var clear = new MenuItem { Header = Loc.T("목록 지우기") };
            clear.Click += (_, _) =>
            {
                _settings.RecentPaths = [];
                _settings.Save();
                UpdateRecentFilesMenu();
            };
            RecentFilesMenu.Items.Add(clear);
        }
    }

    private static string TruncatePath(string path, int maxLen)
    {
        if (path.Length <= maxLen) return path;
        return "..." + path[^maxLen..];
    }

    private void OpenRecentPath(string path)
    {
        if (File.Exists(path))
        {
            _ = OpenArchiveAsync(path);
        }
        else if (Directory.Exists(path))
        {
            // 폴더인 경우 압축 생성 대화상자로 열기
            _ = RunCreateAsync(new CreateDialog([path]) { Owner = this });
        }
        else
        {
            ShowInfo(Loc.F("'{0}' 경로를 찾을 수 없습니다.", path));
            // 존재하지 않는 경로는 최근 목록에서 제거
            _settings.RecentPaths.Remove(path);
            _settings.Save();
            UpdateRecentFilesMenu();
        }
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
    private EntryRow? _pendingSingleSelection;
    private EntryRow? _sweepAnchor;
    private bool _sweeping;
    private CancellationTokenSource? _dragPreparation;

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

    private void EntryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _viewModel.SelectedCount = EntryGrid.SelectedItems.Count;

    /// <summary>우클릭 메뉴를 열 때 항목별 사용 가능 여부를 맞춥니다.</summary>
    private void EntryContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        // 열기: 파일 또는 폴더 하나를 선택했을 때 (폴더는 안으로 들어갑니다).
        ContextOpenItem.IsEnabled = EntryGrid.SelectedItems.Count == 1;
        ContextExtractItem.IsEnabled = _viewModel.CanExtractSelected;
        ContextAddItem.IsEnabled = _viewModel.CanModify;
        ContextDeleteItem.IsEnabled = _viewModel.CanDelete;
        ContextCopyNameItem.IsEnabled = _viewModel.HasSelection;
        ContextSelectAllItem.IsEnabled = _viewModel.Entries.Count > 0;
        ContextUpItem.IsEnabled = _viewModel.CanGoUp;
    }

    private void SelectAllEntries_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Entries.Count == 0) return;
        EntryGrid.Focus();
        EntryGrid.SelectAll();
    }

    private void Find_Click(object sender, RoutedEventArgs e) => FocusSearchBox();

    private void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    /// <summary>선택한 항목의 압축 파일 안 경로를 줄 단위로 클립보드에 복사합니다.</summary>
    private void CopyNames_Click(object sender, RoutedEventArgs e)
    {
        string[] names = EntryGrid.SelectedItems.Cast<EntryRow>()
            .Select(row => row.Info.Name.TrimEnd('/'))
            .ToArray();
        if (names.Length == 0) return;

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, names));
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void CloseArchive_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasArchive) return;

        if (_viewModel.IsBusy)
        {
            ShowInfo(Loc.T("이미 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요."));
            return;
        }

        _viewModel.CloseArchive();
    }

    // ───────── 폴더 트리 우클릭 메뉴 ─────────

    private FolderNode? _treeContextNode;

    private void FolderTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null && source is not TreeViewItem)
        {
            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        _treeContextNode = (source as TreeViewItem)?.DataContext as FolderNode;
    }

    private void TreeContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        // 루트(압축 파일 전체)는 '압축 풀기' 단추를 쓰므로 하위 폴더에서만 켭니다.
        TreeExtractItem.IsEnabled = _viewModel.HasArchive && _treeContextNode is { IsRoot: false };
    }

    private void TreeExtractFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_treeContextNode is not { IsRoot: false } node) return;

        string name = node.FullPath.TrimEnd('/');
        EntryRow row = _viewModel.AllRows.FirstOrDefault(r =>
                r.IsDirectory && string.Equals(r.Info.Name.TrimEnd('/'), name, StringComparison.OrdinalIgnoreCase))
            ?? new EntryRow(new ArchiveEntryInfo { Index = -1, Name = name + "/", IsDirectory = true });

        _ = RunExtractDialogAsync(true, [row]);
    }

    private void TreeExpandAll_Click(object sender, RoutedEventArgs e) => SetTreeExpanded(FolderTree, true);

    private void TreeCollapseAll_Click(object sender, RoutedEventArgs e)
    {
        SetTreeExpanded(FolderTree, false);

        // 맨 위(압축 파일) 항목은 열어 두어 목록이 통째로 사라지지 않게 합니다.
        if (FolderTree.ItemContainerGenerator.ContainerFromIndex(0) is TreeViewItem root) root.IsExpanded = true;
    }

    private static void SetTreeExpanded(ItemsControl parent, bool expanded)
    {
        foreach (object item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem container) continue;
            container.IsExpanded = expanded;
            if (expanded) container.UpdateLayout();
            SetTreeExpanded(container, expanded);
        }
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
        if (_dragPreparation is not null)
        {
            _dragPreparation.Cancel();
            e.Handled = true;
            return;
        }
        DataGridRow? row = RowAt(e.OriginalSource as DependencyObject);
        _pendingSingleSelection = null;
        _dragCandidate = e.ClickCount == 1 && row?.Item is EntryRow &&
                         Keyboard.Modifiers == ModifierKeys.None;
        _dragStart = e.GetPosition(null);
        _sweeping = false;
        _sweepAnchor = _dragCandidate ? row!.Item as EntryRow : null;
        // 이미 선택된 행을 누를 때는 마우스를 놓을 때까지 선택 축소를 미룹니다.
        if (_dragCandidate && row!.IsSelected && EntryGrid.SelectedItems.Count > 1 && row.Item is EntryRow selected)
        {
            _pendingSingleSelection = selected;
            e.Handled = true;
        }
        if (_dragCandidate && !EntryGrid.CaptureMouse())
        {
            _dragCandidate = false;
            _pendingSingleSelection = null;
        }
    }

    private void EntryGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragPreparation?.Cancel();
        if (!_sweeping && _pendingSingleSelection is EntryRow row && EntryGrid.Items.Contains(row))
        {
            EntryGrid.SelectedItems.Clear();
            EntryGrid.SelectedItems.Add(row);
        }
        _pendingSingleSelection = null;
        _dragCandidate = false;
        _sweepAnchor = null;
        if (Mouse.Captured == EntryGrid) EntryGrid.ReleaseMouseCapture();
    }

    private void EntryGrid_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (Mouse.Captured == EntryGrid) return;
        _dragPreparation?.Cancel();
        _dragCandidate = false;
        _pendingSingleSelection = null;
        _sweepAnchor = null;
        _sweeping = false;
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

        Point position = e.GetPosition(EntryGrid);
        bool inside = position.X >= 0 && position.Y >= 0 &&
                      position.X < EntryGrid.ActualWidth && position.Y < EntryGrid.ActualHeight;
        if (!inside && !_sweeping)
        {
            _dragCandidate = false;
            _pendingSingleSelection = null;
            StartDragOut();
            return;
        }
        if (!inside || _sweepAnchor is null) return;
        var hit = EntryGrid.InputHitTest(position) as DependencyObject;
        if (RowAt(hit)?.Item is not EntryRow end) return;
        int startIndex = EntryGrid.Items.IndexOf(_sweepAnchor);
        int endIndex = EntryGrid.Items.IndexOf(end);
        if (startIndex < 0 || endIndex < 0) return;
        _sweeping = true;
        _pendingSingleSelection = null;
        EntryGrid.SelectedItems.Clear();
        for (int i = Math.Min(startIndex, endIndex); i <= Math.Max(startIndex, endIndex); i++)
            EntryGrid.SelectedItems.Add(EntryGrid.Items[i]);
    }

    /// <summary>선택 항목을 임시 폴더로 풀어 탐색기 등으로 끌어 놓을 수 있게 합니다.</summary>
    private async void StartDragOut()
    {
        if (!_viewModel.HasArchive || _viewModel.IsBusy) return;

        var selected = EntryGrid.SelectedItems.Cast<EntryRow>().ToList();
        if (selected.Count == 0) return;

        List<EntryRow> expanded = _viewModel.ExpandRows(selected);
        if (expanded.Any(row => row.Info.IsEncrypted))
        {
            ShowInfo(Loc.T("암호가 걸린 항목은 끌어서 내보낼 수 없습니다.\n'압축 풀기'를 사용해 주세요."));
            return;
        }

        long total = expanded.Sum(row => row.Info.Size);
        // 2GB 제한 (임시 폴더 공간 확인은 Extract에서 수행)
        const long MaxDragBytes = 2L * 1024 * 1024 * 1024;
        if (total > MaxDragBytes)
        {
            ShowInfo(Loc.F("끌어서 내보내기는 {0} 이하만 지원합니다.\n'압축 풀기'를 사용해 주세요.", Format.Bytes(MaxDragBytes)));
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            using var cancellation = new CancellationTokenSource();
            _dragPreparation = cancellation;
            var result = await Task.Run(() => _viewModel.ExtractToTemp(selected, expanded, token: cancellation.Token));
            using DragTempCache.Lease? lease = result.Lease;
            List<string> paths = result.Paths;

            Mouse.OverrideCursor = null;

            if (cancellation.IsCancellationRequested || Mouse.LeftButton != MouseButtonState.Pressed ||
                Mouse.Captured != EntryGrid)
            {
                return;
            }

            // 파일과 폴더 모두 Explorer의 FileDrop에 전달합니다.
            var validPaths = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToArray();
            if (validPaths.Length == 0)
            {
                ShowInfo(Loc.T("끌어갈 파일이 없습니다."));
                return;
            }

            // DataObject에 SetData로 설정 (생성자보다 안정적)
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, validPaths, true);

            _draggingOut = true;
            lease?.Retain();
            EntryGrid.ReleaseMouseCapture();
            try { DragDrop.DoDragDrop(EntryGrid, data, DragDropEffects.Copy); }
            finally
            {
                lease?.Retain();
                _draggingOut = false;
            }
        }
        catch (OperationCanceledException) { }
        catch (DragCacheFullException)
        {
            ShowInfo(Loc.IsEnglish
                ? "The drag-out temporary cache is full. Wait for older exports to expire, or use Extract instead."
                : "끌어서 내보내기 임시 저장 공간이 가득 찼습니다. 이전 내보내기가 만료될 때까지 기다리거나 '압축 풀기'를 사용해 주세요.");
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            _dragPreparation = null;
        }
    }


    private void ShowInfo(string message) =>
        MessageBox.Show(this, message, Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);

    private void ShowError(Exception exception) =>
        MessageBox.Show(this, exception.Message, Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Warning);
}
