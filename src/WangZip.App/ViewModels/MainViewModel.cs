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

public sealed record EncodingOption(string Label, NameEncodingMode Mode);

/// <summary>빠른 압축 풀기 목적지. Resolve가 null이면 압축 파일 옆 폴더입니다.</summary>
public sealed record QuickDestination(string Label, Func<string>? Resolve, string Hint);

/// <summary>나무형 폴더 트리 노드.</summary>
public sealed class FolderNode
{
    public string Name { get; init; } = "";
    /// <summary>루트부터 "/" 구분자로 이어진 전체 경로 (루트는 빈 문자열).</summary>
    public string FullPath { get; init; } = "";
    public ObservableCollection<FolderNode> Children { get; } = [];
    public bool IsRoot { get; init; }

    public string Icon => IsRoot ? "\uE8A5" : "\uE8B7";
}

public sealed class EntryRow
{
    public EntryRow(ArchiveEntryInfo info)
    {
        Info = info;
    }

    public ArchiveEntryInfo Info { get; }

    public string Icon => Info.IsDirectory ? "\uE8B7" : Info.IsSymlink ? "\uE71B" : "\uE7C3";
    /// <summary>현재 폴더 기준의 표시 이름 (마지막 경로 조각).</summary>
    public string Name => Segment(Info.Name);
    public string SizeText => Info.IsDirectory ? string.Empty : Info.SizeText;
    public string CompressedText => Info.IsDirectory ? string.Empty : Info.CompressedText;
    public string RatioText => Info.IsDirectory ? string.Empty : Info.RatioText;
    public string ModifiedText => Info.ModifiedText;
    public string MethodText => Info.IsDirectory ? "폴더" : Info.IsEncrypted ? "AES" : Info.MethodName;
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
    public static IReadOnlyList<EncodingOption> EncodingOptions { get; } =
    [
        new("자동 감지 (권장)", NameEncodingMode.Auto),
        new("UTF-8", NameEncodingMode.Utf8),
        new("CP949 / EUC-KR (구형)", NameEncodingMode.Cp949),
        new("원본 바이트", NameEncodingMode.Raw),
    ];

    public static IReadOnlyList<QuickDestination> QuickDestinations { get; } =
    [
        new("압축 파일 옆 폴더 (권장)", null, "압축 파일과 같은 이름의 폴더를 새로 만들어 풉니다."),
        new("바탕 화면", () => Environment.GetFolderPath(Environment.SpecialFolder.Desktop), ""),
        new("다운로드", () => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), ""),
        new("문서", () => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ""),
    ];

    private IArchiveSource? _source;
    private string? _archivePath;
    private string _statusText = "압축 파일을 열거나, 파일을 이 창으로 끌어다 놓으세요.";
    private string _encodingDescription = string.Empty;
    private string _pathText = "";
    private bool _isBusy;
    private string? _currentPath = "";
    private int _quickDestinationIndex;
    private List<EntryRow> _allRows = [];

    public ObservableCollection<EntryRow> Entries { get; } = [];
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
        ? "왕집"
        : $"{Path.GetFileName(ArchivePath)} — 왕집";

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

    /// <summary>빠른 압축 풀기 목적지 (AppSettings에 유지).</summary>
    public int QuickDestinationIndex
    {
        get => _quickDestinationIndex;
        set
        {
            if (Set(ref _quickDestinationIndex, value))
            {
                var destinations = QuickDestinations;
                string label = value >= 0 && value < destinations.Count ? destinations[value].Label : "";
                StatusText = HasArchive ? $"압축 풀기 위치: {label}" : _statusText;
                OnPropertyChanged(nameof(SelectedQuickHint));
            }
        }
    }

    public string SelectedQuickHint =>
        QuickDestinationIndex >= 0 && QuickDestinationIndex < QuickDestinations.Count
            ? QuickDestinations[QuickDestinationIndex].Hint
            : "";

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

    public async Task OpenAsync(string archivePath, NameEncodingMode encodingMode)
    {
        IsBusy = true;
        try
        {
            (IArchiveSource Source, List<EntryRow> Rows) loaded = await Task.Run(() =>
            {
                IArchiveSource opened = ArchiveService.Open(archivePath, encodingMode);
                List<EntryRow> rows = opened.Entries.Select(entry => new EntryRow(entry)).ToList();
                return (opened, rows);
            });

            _source?.Dispose();
            _source = loaded.Source;
            _allRows = loaded.Rows;

            ArchivePath = _source.ArchivePath;
            EncodingDescription = "파일명 인코딩: " + _source.EncodingDescription;

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
        ApplyFilter();
    }

    private void RebuildTree()
    {
        TreeRoots.Clear();

        var root = new FolderNode
        {
            IsRoot = true,
            Name = ArchivePath is null ? "압축 파일" : Path.GetFileName(ArchivePath),
            FullPath = "",
        };

        var map = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = root,
        };

        // 디렉터리 항목 먼저 등록 (파일 경로로도 폴더 조상을 자동 생성)
        void Ensures(string path)
        {
            while (!string.IsNullOrEmpty(path))
            {
                if (map.ContainsKey(path)) break;
                int slash = path.LastIndexOf('/');
                string parent = slash <= 0 ? "" : path[..slash];
                EnsureFolder(map, path, parent);
                path = parent;
            }
        }

        foreach (var row in _allRows)
        {
            string path = row.Info.Name.TrimEnd('/');
            int slash = path.LastIndexOf('/');
            string parent = slash <= 0 ? "" : path[..slash];
            Ensures(parent);
            if (row.Info.IsDirectory)
            {
                EnsureFolder(map, path, parent);
            }
        }

        // 부모-자식 연결 후 폴더順 정렬 (가나다 순)
        foreach (var pair in map)
        {
            if (pair.Key == "") continue;
            string path = pair.Key;
            int slash = path.LastIndexOf('/');
            string parent = slash <= 0 ? "" : path[..slash];
            if (map.TryGetValue(parent, out var parentNode) && parentNode.Children.All(c => !c.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                parentNode.Children.Add(pair.Value);
            }
        }

        foreach (var pair in map)
        {
            var sorted = pair.Value.Children.OrderBy(c => c.Name, StringComparer.CurrentCulture).ToList();
            pair.Value.Children.Clear();
            foreach (var child in sorted) pair.Value.Children.Add(child);
        }

        TreeRoots.Add(root);
    }

    private static void EnsureFolder(
        Dictionary<string, FolderNode> map, string path, string parent)
    {
        if (map.ContainsKey(path)) return;

        string name = path;
        int slash = path.LastIndexOf('/');
        if (slash >= 0) name = path[(slash + 1)..];

        var node = new FolderNode { Name = name, FullPath = path };
        map[path] = node;
    }

    private void ApplyFilter()
    {
        string current = _currentPath ?? "";
        if (current.Length > 0 && !current.EndsWith('/')) current += '/';

        IEnumerable<EntryRow> rows = _allRows.Where(row =>
        {
            string name = row.Info.Name;
            if (!name.StartsWith(current, StringComparison.OrdinalIgnoreCase)) return false;
            string relative = name[current.Length..];
            int slash = relative.IndexOf('/');
            if (row.Info.IsDirectory)
            {
                // 폴더: 현재 폴더의 직하위 폴더만
                return slash == -1 || slash == relative.Length - 1;
            }

            return slash == -1;
        });

        Entries.Clear();
        foreach (var row in rows.OrderBy(r => r.Info.IsDirectory ? 0 : 1)
                     .ThenBy(r => r.Name, StringComparer.CurrentCulture))
        {
            Entries.Add(row);
        }

        PathText = current.Length == 0 ? "/" : current;

        OnPropertyChanged(nameof(EntriesCount));
        OnPropertyChanged(nameof(TotalCount));
    }

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
            IArchiveSource source = _source ?? throw new InvalidOperationException("먼저 압축 파일을 열어 주세요.");
            int[] indices = rows.Select(row => row.Info.Index).ToArray();

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
                throw new ArchiveException("무결성 검사는 ZIP 형식에서만 지원합니다.");
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
                throw new ArchiveException("항목 추가는 ZIP 형식에서만 지원합니다.");
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
                throw new ArchiveException("항목 삭제는 ZIP 형식에서만 지원합니다.");
            }

            int[] indices = rows.Select(row => row.Info.Index).Distinct().ToArray();
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

    /// <summary>빠른 압축 풀기 목적지 결정.</summary>
    public string ResolveQuickDestination()
    {
        var destination = QuickDestinationIndex >= 0 && QuickDestinationIndex < QuickDestinations.Count
            ? QuickDestinations[QuickDestinationIndex]
            : QuickDestinations[0];

        if (destination.Resolve is { } resolver)
        {
            string path = resolver();
            return Directory.Exists(path) ? path : DefaultExtractDirectory;
        }

        return DefaultExtractDirectory;
    }

    public async Task<string?> PreviewAsync(EntryRow row)
    {
        IArchiveSource source = _source ?? throw new InvalidOperationException("먼저 압축 파일을 열어 주세요.");

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
            StatusText = "압축 파일을 열거나, 파일을 이 창으로 끌어다 놓으세요.";
            return;
        }

        long size = _source.Entries.Sum(entry => entry.Size);
        long compressed = _source.Entries.Sum(entry => entry.CompressedSize);
        double ratio = size <= 0 ? 0 : 1.0 - (double)compressed / size;

        StatusText =
            $"전체 {_source.Entries.Count:N0}개 항목 · 원본 {Format.Bytes(size)} · " +
            $"압축 {Format.Bytes(compressed)} · {Format.CompressionRate(ratio)}";
    }

    public void Dispose()
    {
        _source?.Dispose();
        _source = null;
    }
}
