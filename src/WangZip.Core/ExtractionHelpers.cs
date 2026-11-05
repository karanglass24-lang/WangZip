using System.Diagnostics;

namespace WangZip.Core;

internal sealed class ProgressThrottle
{
    private readonly IProgress<ProgressReport>? _progress;
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private long _lastMilliseconds = -100;

    public ProgressThrottle(IProgress<ProgressReport>? progress)
    {
        _progress = progress;
    }

    public void Report(long processed, long total, string? currentName, bool force = false)
    {
        if (_progress is null) return;

        long now = _watch.ElapsedMilliseconds;
        if (!force && now - _lastMilliseconds < 60) return;

        _lastMilliseconds = now;
        _progress.Report(new ProgressReport(processed, total, currentName));
    }
}

internal static class ExtractionHelpers
{
    public static void EnsureFreeSpace(string destinationDirectory, long requiredBytes)
    {
        try
        {
            string root = Path.GetPathRoot(Path.GetFullPath(destinationDirectory)) ?? string.Empty;
            if (root.Length == 0) return;

            var drive = new DriveInfo(root);
            if (!drive.IsReady) return;

            const long margin = 16L * 1024 * 1024;
            if (drive.AvailableFreeSpace < requiredBytes + margin)
            {
                throw new ArchiveException(
                    $"디스크 여유 공간이 부족합니다. 필요: {Format.Bytes(requiredBytes)}, " +
                    $"사용 가능: {Format.Bytes(drive.AvailableFreeSpace)}");
            }
        }
        catch (ArgumentException)
        {
            // UNC 경로 등 DriveInfo로 확인할 수 없는 대상은 건너뜁니다.
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(Safety.PathGuard.ToExtended(path)))
            {
                File.Delete(Safety.PathGuard.ToExtended(path));
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
