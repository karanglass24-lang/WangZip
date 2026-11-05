using System.Buffers;
using WangZip.Core.Safety;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace WangZip.Core.SharpCompressSupport;

/// <summary>
/// 전진 읽기(forward-only) 전용 백엔드.
/// .tar.gz / .tar.bz2 / .tar.xz 처럼 압축 스트림 안에 TAR이 들어 있는 형식을 처리합니다.
/// (ArchiveFactory는 이런 중첩 구조를 열지 못합니다.)
/// </summary>
public sealed class ReaderArchiveSource : IArchiveSource
{
    private readonly string _archivePath;
    private readonly string? _password;

    private ReaderArchiveSource(
        string archivePath,
        ArchiveFormat format,
        string? password,
        IReadOnlyList<ArchiveEntryInfo> entries,
        IReadOnlyList<string> warnings)
    {
        _archivePath = archivePath;
        _password = password;
        Format = format;
        Entries = entries;
        Warnings = warnings;
    }

    public string ArchivePath => _archivePath;
    public ArchiveFormat Format { get; }
    public IReadOnlyList<ArchiveEntryInfo> Entries { get; }
    public IReadOnlyList<string> Warnings { get; }
    public string EncodingDescription => "라이브러리 자동 감지";

    public static ReaderArchiveSource Open(string archivePath, ArchiveFormat format, string? password = null)
    {
        var entries = new List<ArchiveEntryInfo>();
        var warnings = new List<string>();

        try
        {
            using IReader reader = OpenReader(archivePath, password);
            int index = 0;

            while (reader.MoveToNextEntry())
            {
                IEntry entry = reader.Entry;
                string name = NormalizeKey(entry.Key);
                int current = index++;

                // tar.exe가 만드는 루트 항목("./")은 실제 파일이 아니므로 목록에서 제외합니다.
                // 인덱스는 그대로 소비해 Extract 단계와 번호가 어긋나지 않게 합니다.
                if (name.Length == 0) continue;

                entries.Add(new ArchiveEntryInfo
                {
                    Index = current,
                    Name = name,
                    Size = SafeLong(() => entry.Size),
                    CompressedSize = SafeLong(() => entry.CompressedSize),
                    Crc32 = (uint)SafeLong(() => entry.Crc),
                    Modified = entry.LastModifiedTime is DateTime modified
                        ? new DateTimeOffset(modified)
                        : default,
                    IsDirectory = entry.IsDirectory,
                    IsEncrypted = SafeBool(() => entry.IsEncrypted),
                    EncodingName = "라이브러리 자동",
                });
            }
        }
        catch (Exception exception) when (exception is not ArchiveException && SharpCompressSource.IsPasswordError(exception))
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

        if (entries.Count == 0) warnings.Add("압축 파일 안에 항목이 없습니다.");

        return new ReaderArchiveSource(archivePath, format, password, entries, warnings);
    }

    public ExtractionResult Extract(
        IReadOnlyCollection<int> indices,
        ExtractionOptions options,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        bool all = indices.Count == 0;
        var selected = new HashSet<int>(indices);

        long totalBytes = Entries
            .Where(entry => !entry.IsDirectory && (all || selected.Contains(entry.Index)))
            .Sum(entry => entry.Size);

        if (options.CheckFreeSpace)
        {
            ExtractionHelpers.EnsureFreeSpace(options.DestinationDirectory, totalBytes);
        }

        var guard = new PathGuard(options.DestinationDirectory);
        var throttle = new ProgressThrottle(progress);
        var warnings = new List<string>(Warnings);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        long processed = 0;
        int extractedFiles = 0;
        int skippedFiles = 0;

        try
        {
            using IReader reader = OpenReader(_archivePath, options.Password ?? _password);
            int index = 0;

            while (reader.MoveToNextEntry())
            {
                cancellationToken.ThrowIfCancellationRequested();
                int current = index++;
                IEntry entry = reader.Entry;

                if (!all && !selected.Contains(current)) continue;

                string entryName = NormalizeKey(entry.Key);

                // tar 루트("./") 항목은 건너뜁니다.
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
                    using (Stream source = reader.OpenEntryStream())
                    using (var output = new FileStream(
                               PathGuard.ToExtended(temporary), FileMode.CreateNew, FileAccess.Write,
                               FileShare.None, bufferSize: 128 * 1024, FileOptions.SequentialScan))
                    {
                        byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
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
        catch (Exception exception) when (exception is not ArchiveException and not OperationCanceledException && SharpCompressSource.IsPasswordError(exception))
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

    private static IReader OpenReader(string path, string? password) =>
        ReaderFactory.OpenReader(path, new ReaderOptions
        {
            Password = password,
            LeaveStreamOpen = false,
        });

    /// <summary>"./문서/파일" 형태의 TAR 경로를 정리합니다.</summary>
    internal static string NormalizeKey(string? key)
    {
        string name = (key ?? string.Empty).Replace('\\', '/');

        while (name.StartsWith("./", StringComparison.Ordinal))
        {
            name = name[2..];
        }

        return name == "." ? string.Empty : name;
    }

    private static long SafeLong(Func<long> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return 0;
        }
    }

    private static bool SafeBool(Func<bool> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
    }
}
