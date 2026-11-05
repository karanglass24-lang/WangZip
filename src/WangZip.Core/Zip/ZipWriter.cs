using System.IO.Compression;
using System.Text;
using WangZip.Core.Safety;

namespace WangZip.Core.Zip;

internal sealed record FileItem(string FullPath, string EntryName, long Length, DateTime LastWriteTime);

public static class ZipWriter
{
    static ZipWriter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static CreationResult Create(
        string outputPath,
        IReadOnlyList<string> sources,
        CreationOptions options,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        if (sources.Count == 0)
        {
            throw new ArchiveException("압축할 파일이나 폴더를 선택해 주세요.");
        }

        string fullOutput = Path.GetFullPath(outputPath);
        var warnings = new List<string>();
        List<FileItem> items = CollectItems(sources, options, fullOutput, warnings);

        if (items.Count == 0)
        {
            throw new ArchiveException("압축할 파일이 없습니다.");
        }

        long totalBytes = items.Sum(item => item.Length);
        Directory.CreateDirectory(PathGuard.ToExtended(Path.GetDirectoryName(fullOutput)!));

        Encoding? nameEncoding = options.NameEncoding switch
        {
            NameEncodingMode.Cp949 => Encoding.GetEncoding(949),
            NameEncodingMode.Utf8 => Encoding.UTF8,
            _ => null, // null이면 .NET이 UTF-8 + 플래그(bit 11)로 기록
        };

        var throttle = new ProgressThrottle(progress);
        long processed = 0;

        try
        {
            using (var file = new FileStream(
                       PathGuard.ToExtended(fullOutput), FileMode.Create, FileAccess.ReadWrite,
                       FileShare.None, bufferSize: 128 * 1024))
            using (var archive = new ZipArchive(
                       file, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: nameEncoding))
            {
                if (!string.IsNullOrEmpty(options.Comment))
                {
                    archive.Comment = options.Comment;
                }

                foreach (FileItem item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ZipArchiveEntry entry = archive.CreateEntry(item.EntryName, options.Level);
                    entry.LastWriteTime = ClampTimestamp(item.LastWriteTime);

                    // 폴더 항목은 내용이 없으므로 파일을 열지 않습니다.
                    if (item.EntryName.EndsWith('/'))
                    {
                        continue;
                    }

                    using Stream destination = entry.Open();
                    using var source = new FileStream(
                        PathGuard.ToExtended(item.FullPath), FileMode.Open, FileAccess.Read,
                        FileShare.Read, bufferSize: 128 * 1024, FileOptions.SequentialScan);

                    var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(128 * 1024);
                    try
                    {
                        int read;
                        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            destination.Write(buffer, 0, read);
                            processed += read;
                            throttle.Report(processed, totalBytes, item.EntryName);
                        }
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
        }
        catch
        {
            ExtractionHelpers.TryDelete(fullOutput);
            throw;
        }

        throttle.Report(totalBytes, totalBytes, null, force: true);

        long outputBytes = new FileInfo(fullOutput).Length;
        return new CreationResult
        {
            OutputPath = fullOutput,
            EntryCount = items.Count,
            SourceBytes = totalBytes,
            OutputBytes = outputBytes,
            Warnings = warnings,
        };
    }

    private static List<FileItem> CollectItems(
        IReadOnlyList<string> sources,
        CreationOptions options,
        string outputPath,
        List<string> warnings)
    {
        var items = new List<FileItem>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string source in sources)
        {
            string full = Path.GetFullPath(source);
            if (File.Exists(PathGuard.ToExtended(full)))
            {
                string baseDirectory = Path.GetDirectoryName(full)!;
                AddFile(items, usedNames, full, baseDirectory, outputPath, warnings);
            }
            else if (Directory.Exists(PathGuard.ToExtended(full)))
            {
                string trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string baseDirectory = Path.GetDirectoryName(trimmed)!;

                var enumerationOptions = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                };

                var files = Directory.EnumerateFiles(trimmed, "*", enumerationOptions).ToList();
                if (files.Count == 0 && options.IncludeEmptyDirectories)
                {
                    AddDirectoryEntry(items, usedNames, trimmed, baseDirectory);
                }

                foreach (string file in files)
                {
                    AddFile(items, usedNames, file, baseDirectory, outputPath, warnings);
                }

                if (options.IncludeEmptyDirectories)
                {
                    foreach (string directory in Directory.EnumerateDirectories(trimmed, "*", enumerationOptions))
                    {
                        if (!Directory.EnumerateFileSystemEntries(directory).Any())
                        {
                            AddDirectoryEntry(items, usedNames, directory, baseDirectory);
                        }
                    }
                }
            }
            else
            {
                warnings.Add($"경로를 찾을 수 없어 건너뜁니다: {source}");
            }
        }

        return items;
    }

    private static void AddFile(
        List<FileItem> items,
        HashSet<string> usedNames,
        string fullPath,
        string baseDirectory,
        string outputPath,
        List<string> warnings)
    {
        if (string.Equals(fullPath, outputPath, StringComparison.OrdinalIgnoreCase)) return;

        string entryName = ToEntryName(baseDirectory, fullPath);
        if (entryName.Length == 0) return;

        if (!usedNames.Add(entryName))
        {
            warnings.Add($"이름이 중복되어 건너뜁니다: {entryName}");
            return;
        }

        long length;
        DateTime modified;
        try
        {
            var info = new FileInfo(fullPath);
            length = info.Length;
            modified = info.LastWriteTime;
        }
        catch (IOException)
        {
            warnings.Add($"파일을 읽을 수 없어 건너뜁니다: {fullPath}");
            return;
        }

        items.Add(new FileItem(fullPath, entryName, length, modified));
    }

    private static void AddDirectoryEntry(
        List<FileItem> items,
        HashSet<string> usedNames,
        string directory,
        string baseDirectory)
    {
        string name = ToEntryName(baseDirectory, directory).TrimEnd('/') + "/";
        if (name == "/" || !usedNames.Add(name)) return;
        items.Add(new FileItem(directory, name, 0, Directory.GetLastWriteTime(directory)));
    }

    private static string ToEntryName(string baseDirectory, string fullPath)
    {
        string relative = Path.GetRelativePath(baseDirectory, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            throw new UnsafeArchiveEntryException($"압축 대상이 기준 폴더를 벗어납니다: {fullPath}");
        }

        return relative.Replace('\\', '/');
    }

    private static DateTimeOffset ClampTimestamp(DateTime value)
    {
        var minimum = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var maximum = new DateTime(2107, 12, 31, 23, 59, 58, DateTimeKind.Local);

        if (value < minimum) return new DateTimeOffset(minimum);
        if (value > maximum) return new DateTimeOffset(maximum);
        return new DateTimeOffset(value);
    }
}
