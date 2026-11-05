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

public sealed class EntryRow
{
    public EntryRow(ArchiveEntryInfo info)
    {
        Info = info;
    }

    public ArchiveEntryInfo Info { get; }

    public string Icon => Info.IsDirectory ? "\uE8B7" : Info.IsSymlink ? "\uE71B" : "\uE7C3";
    public string Name => Info.IsDirectory ? Info.Name.TrimEnd('/') : Info.Name;
    public string SizeText => Info.IsDirectory ? string.Empty : Info.SizeText;
    public string CompressedText => Info.IsDirectory ? string.Empty : Info.CompressedText;
    public string RatioText => Info.IsDirectory ? string.Empty : Info.RatioText;
    public string ModifiedText => Info.ModifiedText;
    public string MethodText => Info.IsDirectory ? "폴더" : Info.MethodName;
    public bool IsDirectory => Info.IsDirectory;
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

    private IArchiveSource? _source;
    private string? _archivePath;
    private string _statusText = "압축 파일을 열거나, 파일을 이 창으로 끌어다 놓으세요.";
    private string _encodingDescription = string.Empty;
    private bool _isBusy;

    public ObservableCollection<EntryRow> Entries { get; } = [];

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
        private set => Set(ref _statusText, value);
    }

    public string EncodingDescription
    {
        get => _encodingDescription;
        private set => Set(ref _encodingDescription, value);
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

            Entries.Clear();
            foreach (EntryRow row in loaded.Rows)
            {
                Entries.Add(row);
            }

            ArchivePath = _source.ArchivePath;
            EncodingDescription = "파일명 인코딩: " + _source.EncodingDescription;
            UpdateStatus();
        }
        finally
        {
            IsBusy = false;
        }
    }

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

    public async Task<string?> PreviewAsync(EntryRow row)
    {
        IArchiveSource source = _source ?? throw new InvalidOperationException("먼저 압축 파일을 열어 주세요.");

        string previewRoot = Path.Combine(Path.GetTempPath(), "왕집", "preview");
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
            .EnumerateFiles(temporary, "*", SearchOption.AllDirectories)
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
            $"{_source.Entries.Count:N0}개 항목 · 원본 {Format.Bytes(size)} · " +
            $"압축 {Format.Bytes(compressed)} · {Format.CompressionRate(ratio)}";
    }

    public void Dispose()
    {
        _source?.Dispose();
        _source = null;
    }
}
