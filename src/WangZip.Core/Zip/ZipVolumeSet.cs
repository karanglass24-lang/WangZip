using System.Text.RegularExpressions;

namespace WangZip.Core.Zip;

/// <summary>
/// 분할 ZIP의 볼륨 목록. 볼륨을 이어 붙인 하나의 가상 스트림으로 읽습니다.
/// 지원 형식: base.z01 … base.zip (PKZIP/Info-ZIP), base.zip.001 … / base.001 … (단순 바이트 분할).
/// </summary>
internal sealed class ZipVolumeSet
{
    private static readonly Regex ZNumbered = new(@"^\.z(\d{2,})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Numbered = new(@"^\.(\d{3,})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private ZipVolumeSet(string[] paths, long[] starts, long totalLength)
    {
        Paths = paths;
        Starts = starts;
        TotalLength = totalLength;
    }

    public string[] Paths { get; }

    /// <summary>각 볼륨의 가상 스트림 내 시작 위치.</summary>
    public long[] Starts { get; }

    public long TotalLength { get; }
    public bool IsSplit => Paths.Length > 1;

    public static ZipVolumeSet Single(string path) => Create([path]);

    public static ZipVolumeSet Create(IReadOnlyList<string> paths)
    {
        var starts = new long[paths.Count];
        long total = 0;
        for (int i = 0; i < paths.Count; i++)
        {
            starts[i] = total;
            total += new FileInfo(paths[i]).Length;
        }

        return new ZipVolumeSet(paths.ToArray(), starts, total);
    }

    /// <summary>경로가 .zNN / .NNN 형태일 때 전체 볼륨 목록을 찾습니다. 그 밖에는 null(단일 파일).</summary>
    public static ZipVolumeSet? DiscoverFromName(string path)
    {
        string extension = Path.GetExtension(path);
        string stem = path[..^extension.Length];

        if (ZNumbered.IsMatch(extension))
        {
            int width = extension.Length - 2;
            var paths = new List<string>();
            for (int i = 1; ; i++)
            {
                string candidate = $"{stem}.z{i.ToString().PadLeft(width, '0')}";
                if (!File.Exists(candidate)) break;
                paths.Add(candidate);
            }

            string last = stem + ".zip";
            if (!File.Exists(last))
            {
                throw new ArchiveException(Loc.F("분할 ZIP의 마지막 볼륨(.zip)을 찾을 수 없습니다: {0}", Path.GetFileName(last)));
            }

            paths.Add(last);
            return Create(paths);
        }

        if (Numbered.IsMatch(extension))
        {
            int width = extension.Length - 1;
            var paths = new List<string>();
            for (int i = 1; ; i++)
            {
                string candidate = $"{stem}.{i.ToString().PadLeft(width, '0')}";
                if (!File.Exists(candidate)) break;
                paths.Add(candidate);
            }

            return paths.Count == 0 ? null : Create(paths);
        }

        return null;
    }

    /// <summary>.zip 이 분할 ZIP의 마지막 볼륨일 때(EOCD 디스크 번호 &gt; 0) 앞 볼륨(.z01…)을 찾습니다.</summary>
    public static ZipVolumeSet DiscoverForLastVolume(string zipPath, int lastDiskNumber)
    {
        string stem = zipPath[..^Path.GetExtension(zipPath).Length];
        var paths = new List<string>();
        for (int i = 1; i <= lastDiskNumber; i++)
        {
            string candidate = $"{stem}.z{i:D2}";
            if (!File.Exists(candidate))
            {
                throw new ArchiveException(Loc.F("분할 ZIP의 볼륨을 찾을 수 없습니다: {0}", Path.GetFileName(candidate)));
            }

            paths.Add(candidate);
        }

        paths.Add(zipPath);
        return Create(paths);
    }

    public Stream Open() => Paths.Length == 1
        ? new FileStream(Paths[0], FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.RandomAccess)
        : new MultiVolumeStream(this);

    /// <summary>디스크 번호와 디스크 내 오프셋을 가상 스트림 절대 위치로 바꿉니다.</summary>
    public long ToAbsolute(long disk, long offset)
    {
        if (disk < 0 || disk >= Paths.Length)
        {
            throw new ArchiveException(Loc.F("분할 ZIP의 디스크 번호가 올바르지 않습니다: {0}", disk));
        }

        return Starts[disk] + offset;
    }
}

/// <summary>여러 볼륨 파일을 이어 붙여 하나처럼 읽는 읽기 전용 탐색 가능 스트림.</summary>
internal sealed class MultiVolumeStream : Stream
{
    private readonly ZipVolumeSet _set;
    private FileStream? _current;
    private int _currentIndex = -1;
    private long _position;

    public MultiVolumeStream(ZipVolumeSet set) => _set = set;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _set.TotalLength;

    public override long Position
    {
        get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int total = 0;
        while (!buffer.IsEmpty && _position < _set.TotalLength)
        {
            int index = FindVolume(_position);
            FileStream stream = Use(index);
            long local = _position - _set.Starts[index];
            if (stream.Position != local) stream.Position = local;

            int read = stream.Read(buffer);
            if (read <= 0) break;

            buffer = buffer[read..];
            _position += read;
            total += read;
        }

        return total;
    }

    private int FindVolume(long position)
    {
        int index = Array.BinarySearch(_set.Starts, position);
        return index >= 0 ? index : ~index - 1;
    }

    private FileStream Use(int index)
    {
        if (_currentIndex != index)
        {
            _current?.Dispose();
            _current = new FileStream(
                _set.Paths[index], FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.RandomAccess);
            _currentIndex = index;
        }

        return _current!;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            _ => _set.TotalLength + offset,
        };

        Position = target;
        return _position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _current?.Dispose();
            _current = null;
        }

        base.Dispose(disposing);
    }
}
