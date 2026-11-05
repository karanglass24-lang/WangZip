using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using WangZip.Core;
using WangZip.Core.Zip;

namespace WangZip.App.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public sealed record QuickDestination(string Label, Func<string>? Resolve, string Hint);

/// <summary>확장자별 아이콘 배지(라벨 + 색).</summary>
public static class FileTypeIcons
{
    public static (string Label, string Color) For(string name)
    {
        string ext = Path.GetExtension(name.TrimEnd('/')).TrimStart('.').ToLowerInvariant();
        string label = ext.Length == 0 ? "" : ext.Length > 4 ? ext[..4].ToUpperInvariant() : ext.ToUpperInvariant();
        string color = ext switch
        {
            "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp" or "tif" or "tiff" or "svg" or "ico" or "heic" => "#1FA67A",
            "mp4" or "mkv" or "avi" or "mov" or "wmv" or "webm" or "flv" => "#D6455D",
            "mp3" or "wav" or "flac" or "aac" or "ogg" or "m4a" or "wma" => "#E0752B",
            "zip" or "7z" or "rar" or "tar" or "gz" or "tgz" or "bz2" or "xz" or "cab" or "iso" or "jar" => "#A36A2B",
            "pdf" => "#D93025",
            "doc" or "docx" or "hwp" or "hwpx" or "rtf" => "#2B579A",
            "xls" or "xlsx" or "csv" => "#217346",
            "ppt" or "pptx" => "#D24726",
            "txt" or "md" or "log" or "ini" => "#6B7280",
            "cs" or "js" or "ts" or "py" or "java" or "c" or "cpp" or "h" or "go" or "rs" or "json" or "xml"
                or "html" or "htm" or "css" or "yml" or "yaml" or "sql" or "xaml" or "sh" or "ps1" => "#5B6ABF",
            "exe" or "msi" or "bat" or "cmd" or "dll" => "#4B5563",
            _ => "#3E7BEA",
        };
        return (label, color);
    }
}

public sealed record PathCrumb(string Label, string Path);

public sealed class EntryRow
{
    public EntryRow(ArchiveEntryInfo info)
    {
        Info = info;
    }

    public ArchiveEntryInfo Info { get; }

    public string IconLabel => Info.IsDirectory ? "" : Info.IsSymlink ? "LNK" : FileTypeIcons.For(Info.Name).Label;
    public string IconColor => Info.IsDirectory ? "#E9A13C" : Info.IsSymlink ? "#7B61C4" : FileTypeIcons.For(Info.Name).Color;

    public System.Windows.Media.Brush IconBrush
    {
        get
        {
            var brush = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(IconColor)!;
            brush.Freeze();
            return brush;
        }
    }
    /// <summary>현재 폴더 기준의 표시 이름 (마지막 경로 조각).</summary>
    public string Name => FullPathMode ? Info.Name.TrimEnd('/') : Segment(Info.Name);
    /// <summary>검색 결과 보기에서는 전체 경로를 이름으로 보여 줍니다.</summary>
    public bool FullPathMode { get; set; }
    public long SortSize => Info.IsDirectory ? -1 : Info.Size;
    public long SortCompressed => Info.IsDirectory ? -1 : Info.CompressedSize;
    public double SortRatio => Info.IsDirectory ? -1 : Info.Ratio;
    public string SizeText => Info.IsDirectory ? string.Empty : Info.SizeText;
    public string CompressedText => Info.IsDirectory ? string.Empty : Info.CompressedText;
    public string RatioText => Info.IsDirectory ? string.Empty : Info.RatioText;
    public string ModifiedText => Info.ModifiedText;
    public string MethodText => Info.IsDirectory ? Loc.T("폴더") : Info.IsEncrypted ? "AES" : Info.MethodName;
    public bool IsDirectory => Info.IsDirectory;
    public string LockText => Info.IsEncrypted ? "\uE72E" : "";

    private static string Segment(string path)
    {
        string trimmed = path.TrimEnd('/');
        int slash = trimmed.LastIndexOf('/');
        return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
    }
}

public sealed class MainViewModel : ObservableObject, IDisposable
{
    public static IReadOnlyList<QuickDestination> QuickDestinations { get; } =
    [
        new(Loc.T("압축 파일 연 폴더 (권장)"), null, Loc.T("압축 파일과 같은 이름의 폴더를 새로 만들어 풉니다.")),
        new(Loc.T("바탕 화면"), () => Environment.GetFolderPath(Environment.SpecialFolder.Desktop), ""),
        new(Loc.T("다운로드"), () => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), ""),
        new(Loc.T("문서"), () => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ""),
    ];

    private IArchiveSource? _source;
    private string? _archivePath;
    private string _statusText = Loc.T("압축 파일을 열거나, 파일을 이 창으로 끌어다 놓으세요.");
    private string _encodingDescription = string.Empty;
    private string _pathText = "";
    private bool _isBusy;
    private string? _currentPath = "";
    private List<EntryRow> _allRows = [];

    public ObservableCollection<EntryRow> Entries { get; } = [];
    public ObservableCollection<PathCrumb> Crumbs { get; } = [];

    private string _filterText = "";
    private string _sortKey = "Name";
    private bool _sortAscending = true;

    /// <summary>이름 검색어. 비어 있지 않으면 압축 파일 전체에서 찾습니다.</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (!Set(ref _filterText, value ?? "")) return;
            if (HasArchive) ApplyFilter();
        }
    }

    public string SortKey => _sortKey;
    public bool SortAscending => _sortAscending;

    /// <summary>같은 열을 다시 누르면 방향을 바꿉니다.</summary>
    public void SortBy(string key)
    {
        if (_sortKey == key) _sortAscending = !_sortAscending;
        else { _sortKey = key; _sortAscending = true; }
        if (HasArchive) ApplyFilter();
    }
    public ObservableCollection<FolderNode> TreeRoots { get; } = [];

    public string? ArchivePath
    {
        get => _archivePath;
        private set
        {
            if (!Set(ref _archivePath, value)) return;
            OnPropertyChanged(nameof(HasArchive));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(DefaultExtractDirectory));
        }
    }

    public string Title => ArchivePath is null
        ? Loc.T("왕집")
        : Loc.F("{0} — 왕집", Path.GetFileName(ArchivePath));

    public bool HasArchive => _source is not null;

    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    public string EncodingDescription
    {
        get => _encodingDescription;
        private set => Set(ref _encodingDescription, value);
    }

    /// <summary>현재 폴더 경로 ("" = 루트, 끝 슬래시 없음).</summary>
    public string CurrentFolder => (_currentPath ?? "").TrimEnd('/');

    public bool CanGoUp => CurrentFolder.Length > 0;

    /// <summary>상위 폴더 경로 ("" = 루트).</summary>
    public string ParentFolder
    {
        get
        {
            string current = CurrentFolder;
            int slash = current.LastIndexOf('/');
            return slash <= 0 ? "" : current[..slash];
        }
    }

    /// <summary>현재 보고 있는 폴더 경로 (탐색 바).</summary>
    public string PathText
    {
        get => _pathText;
        private set => Set(ref _pathText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    public string DefaultExtractDirectory
    {
        get
        {
            if (ArchivePath is null) return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            string? directory = Path.GetDirectoryName(ArchivePath);
            string name = Path.GetFileNameWithoutExtension(ArchivePath);
            return Path.Combine(directory ?? Environment.CurrentDirectory, name);
        }
    }

    public async Task OpenAsync(string archivePath, NameEncodingMode encodingMode, string? password = null)
    {
        IsBusy = true;
        try
        {
            (IArchiveSource Source, List<EntryRow> Rows) loaded = await Task.Run(() =>
            {
                IArchiveSource opened = ArchiveService.Open(archivePath, encodingMode, password);
                List<EntryRow> rows = opened.Entries.Select(entry => new EntryRow(entry)).ToList();
                return (opened, rows);
            });

            _source?.Dispose();
            _source = loaded.Source;
            _allRows = loaded.Rows;

            ArchivePath = _source.ArchivePath;
            EncodingDescription = Loc.T("파일명 인코딩: ") + _source.EncodingDescription;

            _filterText = "";
            OnPropertyChanged(nameof(FilterText));
            RebuildTree();
            SelectFolder("");
            UpdateStatus();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>폴더 선택/변경. null이면 루트(전체 항목 중 현재 폴더 아래).</summary>
    public void SelectFolder(string? path)
    {
        _currentPath = path ?? "";

        // 폴더를 직접 고르면 검색을 끝내고 그 폴더를 보여 줍니다.
        if (_filterText.Length > 0)
        {
            _filterText = "";
            OnPropertyChanged(nameof(FilterText));
        }

        ApplyFilter();
    }

    private void RebuildTree()
    {
        TreeRoots.Clear();
        string rootName = ArchivePath is null ? Loc.T("압축 파일") : Path.GetFileName(ArchivePath);
        TreeRoots.Add(FolderTreeBuilder.Build(
            rootName, _allRows.Select(r => (r.Info.Name, r.Info.IsDirectory))));
    }

    private void ApplyFilter()
    {
        string current = _currentPath ?? "";
        if (current.Length > 0 && !current.EndsWith('/')) current += '/';

        foreach (EntryRow all in _allRows) all.FullPathMode = false;

        string needle = _filterText.Trim();
        if (needle.Length > 0)
        {
            // 검색: 압축 파일 전체에서 이름에 검색어가 들어간 항목
            var found = new List<EntryRow>();
            foreach (EntryRow row in _allRows)
            {
                // 상위 폴더 이름만 맞는 항목까지 걸리지 않도록 마지막 이름 부분만 비교합니다.
                string trimmed = row.Info.Name.TrimEnd('/');
                string leaf = trimmed[(trimmed.LastIndexOf('/') + 1)..];
                if (leaf.Contains(needle, StringComparison.CurrentCultureIgnoreCase))
                {
                    row.FullPathMode = true;
                    found.Add(row);
                }
            }

            ReplaceEntries(found);
            PathText = Loc.F("검색: {0}", needle);
            RebuildCrumbs("");
            OnPropertyChanged(nameof(CanGoUp));
            OnPropertyChanged(nameof(EntriesCount));
            OnPropertyChanged(nameof(TotalCount));
            return;
        }

        var rows = new List<EntryRow>();
        var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var implicitFolders = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        foreach (EntryRow row in _allRows)
        {
            string name = row.Info.Name;
            if (!name.StartsWith(current, StringComparison.OrdinalIgnoreCase)) continue;
            string relative = name[current.Length..];
            if (relative.Length == 0) continue; // 현재 폴더 자신

            int slash = relative.IndexOf('/');
            if (row.Info.IsDirectory)
            {
                // 폴더: 현재 폴더의 직하위 폴더만
                if (slash == -1 || slash == relative.Length - 1)
                {
                    rows.Add(row);
                    seenFolders.Add(relative.TrimEnd('/'));
                }
                else
                {
                    TrackImplicit(implicitFolders, relative[..slash], row.Info.Modified);
                }
            }
            else if (slash == -1)
            {
                rows.Add(row);
            }
            else
            {
                // 폴더 항목 없이 파일 경로로만 존재하는 하위 폴더도 목록에 보여 줍니다.
                TrackImplicit(implicitFolders, relative[..slash], row.Info.Modified);
            }
        }

        foreach (var pair in implicitFolders)
        {
            if (seenFolders.Contains(pair.Key)) continue;
            rows.Add(new EntryRow(new ArchiveEntryInfo
            {
                Index = -1,
                Name = current + pair.Key + "/",
                IsDirectory = true,
                Modified = pair.Value,
            }));
        }

        ReplaceEntries(rows);
        RebuildCrumbs(current.TrimEnd('/'));

        PathText = current.Length == 0 ? "/" : current;
        OnPropertyChanged(nameof(CanGoUp));

        OnPropertyChanged(nameof(EntriesCount));
        OnPropertyChanged(nameof(TotalCount));
    }

    private void ReplaceEntries(List<EntryRow> rows)
    {
        Func<EntryRow, IComparable> key = _sortKey switch
        {
            "Size" => r => r.SortSize,
            "Compressed" => r => r.SortCompressed,
            "Ratio" => r => r.SortRatio,
            "Modified" => r => r.Info.Modified,
            "Method" => r => r.MethodText,
            _ => r => r.Name,
        };

        IOrderedEnumerable<EntryRow> ordered = rows.OrderBy(r => r.Info.IsDirectory ? 0 : 1);
        ordered = _sortAscending
            ? ordered.ThenBy(key, SortComparer.Instance).ThenBy(r => r.Name, StringComparer.CurrentCulture)
            : ordered.ThenByDescending(key, SortComparer.Instance).ThenBy(r => r.Name, StringComparer.CurrentCulture);

        Entries.Clear();
        foreach (EntryRow row in ordered) Entries.Add(row);
    }

    private sealed class SortComparer : IComparer<IComparable>
    {
        public static readonly SortComparer Instance = new();

        public int Compare(IComparable? x, IComparable? y) =>
            x is string a && y is string b
                ? string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase)
                : x?.CompareTo(y) ?? (y is null ? 0 : -1);
    }

    private void RebuildCrumbs(string folder)
    {
        Crumbs.Clear();
        string rootLabel = ArchivePath is null ? "/" : Path.GetFileName(ArchivePath);
        Crumbs.Add(new PathCrumb(rootLabel, ""));

        string accumulated = "";
        foreach (string part in folder.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            accumulated = accumulated.Length == 0 ? part : accumulated + "/" + part;
            Crumbs.Add(new PathCrumb(part, accumulated));
        }
    }

    /// <summary>끌어서 내보내기용: 선택 항목을 임시 폴더로 풀고 각 항목의 실제 경로를 돌려줍니다.</summary>
    public List<string> ExtractToTemp(IReadOnlyList<EntryRow> selected)
    {
        IArchiveSource source = _source ?? throw new InvalidOperationException(Loc.T("먼저 압축 파일을 열어 주세요."));

        string root = Path.Combine(Path.GetTempPath(), "WangZip", "drag");
        try
        {
            if (Directory.Exists(root))
            {
                foreach (string stale in Directory.EnumerateDirectories(root))
                {
                    try { Directory.Delete(stale, recursive: true); } catch (IOException) { }
                }
            }
        }
        catch (IOException)
        {
        }

        string temporary = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);

        int[] indices = ExpandRows(selected).Select(row => row.Info.Index).ToArray();
        if (indices.Length == 0) return [];

        source.Extract(
            indices,
            new ExtractionOptions
            {
                DestinationDirectory = temporary,
                Overwrite = OverwritePolicy.Overwrite,
                CheckFreeSpace = false,
            },
            null,
            CancellationToken.None);

        var paths = new List<string>();
        foreach (EntryRow row in selected)
        {
            string path = Path.Combine(temporary, row.Info.Name.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path) || Directory.Exists(path)) paths.Add(path);
        }

        return paths;
    }

    private static void TrackImplicit(Dictionary<string, DateTimeOffset> map, string name, DateTimeOffset modified)
    {
        if (!map.TryGetValue(name, out DateTimeOffset existing) || modified > existing) map[name] = modified;
    }

    /// <summary>폴더 행은 그 안의 모든 실제 항목으로 펼쳐 중복 없이 돌려줍니다.</summary>
    public List<EntryRow> ExpandRows(IEnumerable<EntryRow> rows)
    {
        var result = new List<EntryRow>();
        var seen = new HashSet<int>();
        foreach (EntryRow row in rows)
        {
            if (!row.IsDirectory)
            {
                if (row.Info.Index >= 0 && seen.Add(row.Info.Index)) result.Add(row);
                continue;
            }

            // 폴더 항목 자신도 포함해야 빈 폴더 선택이 "전체"로 오해되지 않고, 삭제 시 폴더 항목이 남지 않습니다.
            if (row.Info.Index >= 0 && seen.Add(row.Info.Index)) result.Add(row);

            string prefix = row.Info.Name.TrimEnd('/') + "/";
            foreach (EntryRow candidate in _allRows)
            {
                if (candidate.Info.Index >= 0 &&
                    candidate.Info.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    seen.Add(candidate.Info.Index))
                {
                    result.Add(candidate);
                }
            }
        }

        return result;
    }

    /// <summary>현재 폴더·검색 결과와 무관하게 압축 파일 안의 모든 항목.</summary>
    public IReadOnlyList<EntryRow> AllRows => _allRows;

    public int EntriesCount => Entries.Count;
    public int TotalCount => _allRows.Count;

    public async Task<ExtractionResult> ExtractAsync(
        IReadOnlyList<EntryRow> rows,
        ExtractionOptions options,
        IProgress<ProgressReport> progress,
        CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            IArchiveSource source = _source ?? throw new InvalidOperationException(Loc.T("먼저 압축 파일을 열어 주세요."));
            int[] indices = ExpandRows(rows).Select(row => row.Info.Index).ToArray();

            // 빈 배열은 소스가 "전체"로 해석하므로, 풀 것이 없으면 아예 호출하지 않습니다.
            if (indices.Length == 0) return new ExtractionResult();

            return await Task.Run(
                () => source.Extract(indices, options, progress, cancellationToken),
                cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<CreationResult> CreateAsync(
        string outputPath,
        IReadOnlyList<string> sources,
        CreationOptions options,
        IProgress<ProgressReport> progress,
        CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            return await Task.Run(
                () => ZipWriter.Create(outputPath, sources, options, progress, cancellationToken),
                cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<int> VerifyAsync(
        IProgress<ProgressReport> progress,
        CancellationToken cancellationToken,
        string? password = null)
    {
        IsBusy = true;
        try
        {
            if (_source is not ZipArchiveSource zip)
            {
                throw new ArchiveException(Loc.T("무결성 검사는 ZIP 형식에서만 지원합니다."));
            }

            return await Task.Run(() => zip.Verify(progress, cancellationToken, password), cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>열려 있는 ZIP에 파일/폴더 추가.</summary>
    public async Task<ZipModResult> AddToArchiveAsync(
        IReadOnlyList<string> sources,
        IProgress<ProgressReport> progress,
        CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            if (_source is not { Format: ArchiveFormat.Zip } || _archivePath is null)
            {
                throw new ArchiveException(Loc.T("항목 추가는 ZIP 형식에서만 지원합니다."));
            }

            string path = _archivePath;
            var modified = await Task.Run(
                () => ZipRewriter.AddEntries(path, sources, NameEncodingMode.Auto, progress, cancellationToken),
                cancellationToken);

            // 새 항목 반영을 위해 다시 엽니다.
            await OpenAsync(path, CurrentMode);
            return modified;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public NameEncodingMode CurrentMode { get; set; } = NameEncodingMode.Auto;

    /// <summary>열려 있는 ZIP에서 선택 항목 삭제.</summary>
    public async Task<ZipModResult> DeleteFromArchiveAsync(
        IReadOnlyList<EntryRow> rows,
        CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            if (_source is not { Format: ArchiveFormat.Zip } || _archivePath is null)
            {
                throw new ArchiveException(Loc.T("항목 삭제는 ZIP 형식에서만 지원합니다."));
            }

            int[] indices = ExpandRows(rows).Select(row => row.Info.Index).ToArray();
            if (indices.Length == 0) throw new ArchiveException(Loc.T("삭제할 항목을 선택해 주세요."));
            string path = _archivePath;
            var modified = await Task.Run(
                () => ZipRewriter.DeleteEntries(path, indices, cancellationToken),
                cancellationToken);

            await OpenAsync(path, CurrentMode);
            return modified;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<string?> PreviewAsync(EntryRow row)
    {
        IArchiveSource source = _source ?? throw new InvalidOperationException(Loc.T("먼저 압축 파일을 열어 주세요."));

        string previewRoot = Path.Combine(Path.GetTempPath(), "WangZip", "preview");
        string temporary = Path.Combine(previewRoot, Guid.NewGuid().ToString("N"));

        // 이전 미리보기 잔여물 정리
        try
        {
            if (Directory.Exists(previewRoot))
            {
                foreach (string stale in Directory.EnumerateDirectories(previewRoot))
                {
                    try { Directory.Delete(stale, recursive: true); } catch (IOException) { }
                }
            }
        }
        catch (IOException)
        {
        }

        Directory.CreateDirectory(temporary);

        var options = new ExtractionOptions
        {
            DestinationDirectory = temporary,
            Overwrite = OverwritePolicy.Overwrite,
            CheckFreeSpace = false,
            PreserveTimestamps = false,
        };

        await Task.Run(() => source.Extract([row.Info.Index], options, null, CancellationToken.None));

        string? file = Directory
            .EnumerateFiles(temporary, "*", System.IO.SearchOption.AllDirectories)
            .FirstOrDefault();

        if (file is null) return null;

        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
        return file;
    }

    private void UpdateStatus()
    {
        if (_source is null)
        {
            StatusText = Loc.T("압축 파일을 열거나, 파일을 이 창으로 끌어다 놓으세요.");
            return;
        }

        long size = _source.Entries.Sum(entry => entry.Size);
        long compressed = _source.Entries.Sum(entry => entry.CompressedSize);
        double ratio = size <= 0 ? 0 : 1.0 - (double)compressed / size;

        StatusText =
            Loc.F("전체 {0:N0}개 항목 · 원본 {1} · 압축 {2} · {3}", _source.Entries.Count, Format.Bytes(size), Format.Bytes(compressed), Format.CompressionRate(ratio));
    }

    public void Dispose()
    {
        _source?.Dispose();
        _source = null;
    }
}
