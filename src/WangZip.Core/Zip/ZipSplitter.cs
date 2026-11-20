namespace WangZip.Core.Zip;

/// <summary>
/// 완성된 ZIP을 일정 크기의 볼륨(name.zip.001, name.zip.002 …)으로 나눕니다.
/// 단순 바이트 분할이라 7-Zip 등 대부분의 도구가 첫 볼륨으로 열 수 있고, 왕집은 이를 이어 붙여 읽습니다.
/// </summary>
public static class ZipSplitter
{
    public const long MinimumVolumeSize = 64 * 1024;

    /// <summary>zipPath 를 나누고 원본은 삭제합니다. 만든 볼륨 경로를 순서대로 돌려줍니다.</summary>
    public static IReadOnlyList<string> Split(
        string zipPath, long volumeSize, CancellationToken cancellationToken = default)
    {
        if (volumeSize < MinimumVolumeSize)
        {
            throw new ArchiveException(Loc.F("분할 크기는 최소 {0}KB 이상이어야 합니다.", MinimumVolumeSize / 1024));
        }

        var volumes = new List<string>();
        try
        {
            using (var input = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.SequentialScan))
            {
                long remaining = input.Length;
                var buffer = new byte[128 * 1024];
                int index = 1;

                while (remaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string volume = $"{zipPath}.{index:D3}";
                    volumes.Add(volume);
                    long toWrite = Math.Min(volumeSize, remaining);

                    using (var output = new FileStream(volume, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024))
                    {
                        long written = 0;
                        while (written < toWrite)
                        {
                            int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, toWrite - written));
                            if (read <= 0) throw new EndOfStreamException();
                            output.Write(buffer, 0, read);
                            written += read;
                        }
                    }

                    remaining -= toWrite;
                    index++;
                }
            }

            File.Delete(zipPath);
            return volumes;
        }
        catch
        {
            foreach (string volume in volumes) ExtractionHelpers.TryDelete(volume);
            throw;
        }
    }
}
