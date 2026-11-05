using System.Buffers;
using WangZip.Core.Safety;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace WangZip.Core.SharpCompressSupport;

/// <summary>
/// ZIP 이외 포맷(7z, TAR, GZ, BZ2, XZ, RAR 읽기)을 SharpCompress로 읽는 백엔드.
/// 쓰기는 지원하지 않습니다.
/// </summary>
public sealed class SharpCompressSource : IArchiveSource
{
    private readonly string _archivePath;
    private readonly string? _password;
    private readonly List<string> _keys;

    private SharpCompressSource(
        string archivePath,
        ArchiveFormat format,
        string? password,
        IReadOnlyList<ArchiveEntryInfo> entries,
        IReadOnlyList<string> keys,
        IReadOnlyList<string> warnings,
        string encodingDescription)
    {
        _archivePath = archivePath;
        _password = password;
        _keys = keys.ToList();
        Format = format;
        Entries = entries;
        Warnings = warnings;
        EncodingDescription = encodingDescription;
    }

    public string ArchivePath => _archivePath;
    public ArchiveFormat Format { get; }
    public IReadOnlyList<ArchiveEntryInfo> Entries { get; }
    public IReadOnlyList<string> Warnings { get; }
    public string EncodingDescription { get; }

    public static SharpCompressSource Open(
        string archivePath,
        ArchiveFormat format,
        string? password = null)
    {
        var warnings = new List<string>();
        var entries = new List<ArchiveEntryInfo>();
        var keys = new List<string>();

        try
        {
            using IArchive archive = OpenArchive(archivePath, password);
            int index = 0;

            foreach (IArchiveEntry entry in archive.Entries)
            {
                string name = ReaderArchiveSource.NormalizeKey(entry.Key);
                keys.Add(name);

                // tar의 루트 항목("./")은 실제 파일이 아니므로 목록에서 뺍니다(번호는 원래 위치 그대로 유지).
                if (name.Length == 0)
                {
                    index++;
                    continue;
                }

                long size = SafeSize(entry);
                long compressed = SafeCompressedSize(entry);

                entries.Add(new ArchiveEntryInfo
                {
                    Index = index,
                    Name = name,
                    Size = size,
                    CompressedSize = compressed,
                    Modified = entry.LastModifiedTime is DateTime modified
                        ? new DateTimeOffset(modified)
                        : default,
                    IsDirectory = entry.IsDirectory,
                    IsEncrypted = IsEncrypted(entry),
                    EncodingName = "라이브러리 자동",
                });

                index++;
            }
        }
        catch (Exception exception) when (exception is not ArchiveException && IsPasswordError(exception))
        {
            throw new EncryptedArchiveException(
                $"비밀번호가 필요하거나 올바르지 않습니다: {System.IO.Path.GetFileName(archivePath)}\n" +
                "압축 풀기 화면이나 --password 옵션에 올바른 비밀번호를 입력해 주세요.");
        }
        catch (Exception exception) when (exception is not ArchiveException)
        {
            throw new UnsupportedArchiveException(
                $"'{System.IO.Path.GetFileName(archivePath)}' 파일을 읽을 수 없습니다. " +
                "손상되었거나 아직 지원하지 않는 형식일 수 있습니다.\n" + exception.Message,
                exception);
        }

        if (entries.Count == 0)
        {
            warnings.Add("압축 파일 안에 항목이 없습니다.");
        }

        return new SharpCompressSource(
            archivePath,
            format,
            password,
            entries,
            keys,
            warnings,
            "라이브러리 자동 감지");
    }

    public ExtractionResult Extract(
        IReadOnlyCollection<int> indices,
        ExtractionOptions options,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        bool all = indices.Count == 0;
        var selected = new HashSet<int>(indices);

        long totalBytes;
        string? password = options.Password ?? _password;
        using (IArchive probe = OpenArchive(_archivePath, password))
        {
            totalBytes = probe.Entries
                .Where((entry, index) => !entry.IsDirectory && (all || selected.Contains(index)))
                .Sum(SafeSize);
        }

        if (options.CheckFreeSpace)
        {
            ExtractionHelpers.EnsureFreeSpace(options.DestinationDirectory, totalBytes);
        }

        var guard = new PathGuard(options.DestinationDirectory);
        var throttle = new ProgressThrottle(progress);
        var warnings = new List<string>(Warnings);
        long processed = 0;
        int extractedFiles = 0;
        int skippedFiles = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using IArchive archive = OpenArchive(_archivePath, password);
            int index = 0;

            foreach (IArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int current = index++;

                if (!all && !selected.Contains(current)) continue;

                string entryName = ReaderArchiveSource.NormalizeKey(entry.Key ?? $"entry-{current}");
                if (entryName.Length == 0) continue;
                string? target = guard.Resolve(entryName, options.Collision);
                if (target is null)
                {
                    skippedFiles++;
                    continue;
                }

                if (entry.IsDirectory)
                {
                    if (options.CreateDirectoryEntries)
                    {
                        Directory.CreateDirectory(PathGuard.ToExtended(target));
                    }

                    continue;
                }

                if (File.Exists(PathGuard.ToExtended(target)) || Directory.Exists(PathGuard.ToExtended(target)))
                {
                    if (options.Overwrite == OverwritePolicy.Skip)
                    {
                        skippedFiles++;
                        continue;
                    }

                    if (options.Overwrite == OverwritePolicy.Rename)
                    {
                        target = guard.FindFreeOnDisk(target);
                    }
                }

                string? directory = System.IO.Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(PathGuard.ToExtended(directory));
                }

                string temporary = System.IO.Path.Combine(
                    directory ?? options.DestinationDirectory,
                    "." + Guid.NewGuid().ToString("N") + ".wang-tmp");

                try
                {
                    using (Stream source = entry.OpenEntryStream())
                    using (var output = new FileStream(
                               PathGuard.ToExtended(temporary), FileMode.CreateNew, FileAccess.Write,
                               FileShare.None, bufferSize: 128 * 1024, FileOptions.SequentialScan))
                    {
                        var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
                        try
                        {
                            int read;
                            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                output.Write(buffer, 0, read);
                                processed += read;
                                throttle.Report(processed, totalBytes, entryName);
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }

                    if (options.PreserveTimestamps && entry.LastModifiedTime is DateTime modified)
                    {
                        File.SetLastWriteTime(PathGuard.ToExtended(temporary), modified);
                    }

                    MarkOfTheWeb.Copy(_archivePath, temporary);
                    File.Move(PathGuard.ToExtended(temporary), PathGuard.ToExtended(target), overwrite: true);
                    extractedFiles++;
                }
                catch
                {
                    ExtractionHelpers.TryDelete(temporary);
                    throw;
                }
            }
        }
        catch (Exception exception) when (exception is not ArchiveException and not OperationCanceledException && IsPasswordError(exception))
        {
            throw new EncryptedArchiveException(
                "비밀번호가 필요하거나 올바르지 않습니다.\n압축 풀기 화면에서 올바른 비밀번호를 입력해 주세요.");
        }
        catch (Exception exception) when (exception is not ArchiveException and not OperationCanceledException)
        {
            throw new ArchiveException(
                "압축을 푸는 중 오류가 발생했습니다. 분할 압축이거나 손상된 파일일 수 있습니다.\n" +
                exception.Message,
                exception);
        }

        throttle.Report(processed, totalBytes, null, force: true);
        warnings.AddRange(guard.Warnings);

        return new ExtractionResult
        {
            ExtractedFiles = extractedFiles,
            SkippedFiles = skippedFiles,
            TotalBytes = processed,
            Warnings = warnings,
            Elapsed = stopwatch.Elapsed,
        };
    }

    private static IArchive OpenArchive(string path, string? password)
    {
        var options = new ReaderOptions
        {
            Password = password,
            LeaveStreamOpen = false,
        };

        return ArchiveFactory.OpenArchive(path, options);
    }

    private static long SafeSize(IArchiveEntry entry)
    {
        try
        {
            return entry.Size;
        }
        catch
        {
            return 0;
        }
    }

    private static long SafeCompressedSize(IArchiveEntry entry)
    {
        try
        {
            return entry.CompressedSize;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsEncrypted(IArchiveEntry entry)
    {
        try
        {
            return entry.IsEncrypted;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>예외가 "비밀번호 문제"인지 대략 판별합니다 (7z/RAR/ZIP 암호화 관련).</summary>
    internal static bool IsPasswordError(Exception exception)
    {
        string message = exception.Message;
        return
            message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("decrypt", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("cryptographic", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("hmac", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
    }
}
