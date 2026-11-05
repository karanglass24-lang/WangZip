namespace WangZip.Core.Safety;

/// <summary>
/// 압축 파일에서 나온 경로를 안전하게 정규화합니다.
/// zip-slip(상위 경로 탈출), 예약 장치명, ADS(:), 후행 점/공백, 대소문자 충돌을 처리합니다.
/// </summary>
public sealed class PathGuard
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '|', '?', '*'];

    private readonly string _root;
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _warnings = new();

    public PathGuard(string destinationRoot)
    {
        _root = Path.GetFullPath(destinationRoot);
        Directory.CreateDirectory(ToExtended(_root));
    }

    public string Root => _root;
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>압축 항목 이름을 실제 저장 경로로 변환합니다. Skip 정책으로 제외되면 null입니다.</summary>
    public string? Resolve(string entryName, CollisionPolicy collision = CollisionPolicy.Rename)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            throw new UnsafeArchiveEntryException(Loc.T("이름이 비어 있는 항목은 압축을 풀 수 없습니다."));
        }

        string relative = Normalize(entryName);
        if (relative.Length == 0)
        {
            throw new UnsafeArchiveEntryException(Loc.F("안전하지 않은 경로입니다: '{0}'", entryName));
        }

        string combined = Path.GetFullPath(Path.Combine(_root, relative));
        if (!IsUnderRoot(combined))
        {
            throw new UnsafeArchiveEntryException(Loc.F("압축 파일 밖으로 벗어나는 경로입니다: '{0}'", entryName));
        }

        string? unique = EnsureUnique(combined, collision);
        return unique;
    }

    /// <summary>이미 디스크에 존재하는 파일과 충돌할 때 새 이름을 찾습니다.</summary>
    public string FindFreeOnDisk(string target)
    {
        if (!File.Exists(ToExtended(target)) && !Directory.Exists(ToExtended(target))) return target;

        string dir = Path.GetDirectoryName(target)!;
        string ext = Path.GetExtension(target);
        string stem = Path.GetFileNameWithoutExtension(target);

        for (int i = 2; i < 10_000; i++)
        {
            string candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (_used.Contains(candidate)) continue;
            if (!File.Exists(ToExtended(candidate)) && !Directory.Exists(ToExtended(candidate)))
            {
                _used.Add(candidate);
                return candidate;
            }
        }

        throw new ArchiveException(Loc.F("사용 가능한 파일 이름을 찾지 못했습니다: '{0}'", target));
    }

    private string Normalize(string raw)
    {
        string text = raw.Replace('\\', '/');
        string[] rawParts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();

        foreach (string part in rawParts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (stack.Count == 0)
                {
                    throw new UnsafeArchiveEntryException(
                        Loc.F("상위 폴더로 벗어나는 항목(..)은 압축을 풀지 않습니다: '{0}'", raw));
                }

                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            string clean = part;
            if (clean.Length >= 2 && clean[1] == ':')
            {
                clean = clean[2..];
                _warnings.Add(Loc.F("드라이브 문자를 제거했습니다: '{0}'", part));
                if (clean.Length == 0) continue;
            }

            clean = SanitizeSegment(clean);
            if (clean.Length > 0) stack.Add(clean);
        }

        return string.Join(Path.DirectorySeparatorChar, stack);
    }

    private string SanitizeSegment(string segment)
    {
        string original = segment;

        // Windows는 후행 점/공백을 허용하지 않습니다.
        segment = segment.TrimEnd(' ', '.');

        // 예약 장치 이름 (CON, NUL, COM1 ...)은 확장자가 붙어도 예약됩니다.
        foreach (char c in InvalidChars)
        {
            segment = segment.Replace(c, '_');
        }

        var builder = new System.Text.StringBuilder(segment.Length);
        foreach (char c in segment)
        {
            builder.Append(char.IsControl(c) ? '_' : c);
        }

        segment = builder.ToString();

        if (segment.Length == 0) segment = "_";

        string stem = segment.Split('.')[0];
        if (ReservedNames.Contains(stem)) segment = "_" + segment;

        if (segment.Length > 255)
        {
            string ext = Path.GetExtension(segment);
            string stemPart = Path.GetFileNameWithoutExtension(segment);
            int keep = Math.Max(1, 255 - ext.Length);
            segment = stemPart[..Math.Min(stemPart.Length, keep)] + ext;
            _warnings.Add(Loc.F("파일 이름이 너무 길어 줄였습니다: '{0}'", original));
        }

        if (!string.Equals(original, segment, StringComparison.Ordinal))
        {
            _warnings.Add(Loc.F("파일 이름을 안전하게 바꿨습니다: '{0}' → '{1}'", original, segment));
        }

        return segment;
    }

    private string? EnsureUnique(string fullPath, CollisionPolicy policy)
    {
        if (_used.Add(fullPath)) return fullPath;

        if (policy == CollisionPolicy.Skip) return null;
        if (policy == CollisionPolicy.Throw)
        {
            throw new UnsafeArchiveEntryException(Loc.F("압축 안에 같은 경로가 중복됩니다: '{0}'", fullPath));
        }

        string dir = Path.GetDirectoryName(fullPath)!;
        string ext = Path.GetExtension(fullPath);
        string stem = Path.GetFileNameWithoutExtension(fullPath);

        for (int i = 2; i < 10_000; i++)
        {
            string candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (_used.Add(candidate)) return candidate;
        }

        throw new UnsafeArchiveEntryException(Loc.F("중복 이름을 피할 수 없습니다: '{0}'", fullPath));
    }

    private bool IsUnderRoot(string candidate)
    {
        string rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        return candidate.Equals(_root, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>긴 경로에서 Win32 API 한계를 넘지 않도록 확장 경로 접두사를 붙입니다.</summary>
    public static string ToExtended(string path)
    {
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
        if (path.Length < 248) return path;
        // UNC(\\server\share)는 \\?\UNC\server\share 형태여야 합니다.
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return @"\\?\UNC\" + path[2..];
        return @"\\?\" + path;
    }
}
