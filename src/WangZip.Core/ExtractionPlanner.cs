namespace WangZip.Core;

/// <summary>압축을 풀 때 대상 폴더 아래에 압축 파일 이름의 폴더를 만들지 여부.</summary>
public enum SubfolderMode
{
    /// <summary>선택한 위치에 바로 풉니다.</summary>
    Never = 0,

    /// <summary>항목이 최상위 폴더 하나뿐이면 그대로 풀고, 그렇지 않으면 압축 파일 이름 폴더를 만듭니다.</summary>
    Auto = 1,

    /// <summary>항상 압축 파일 이름 폴더를 만듭니다.</summary>
    Always = 2,
}

public static class ExtractionPlanner
{
    /// <summary>모드에 따라 실제로 풀 폴더를 계산합니다. 폴더는 만들지 않습니다.</summary>
    public static string ResolveDestination(
        string destination, string archivePath, IEnumerable<string> entryNames, SubfolderMode mode)
    {
        if (mode == SubfolderMode.Never) return destination;
        if (mode == SubfolderMode.Auto && HasSingleRootFolder(entryNames)) return destination;

        string name = ArchiveBaseName(archivePath);
        return string.IsNullOrWhiteSpace(name) ? destination : Path.Combine(destination, name);
    }

    /// <summary>모든 항목이 같은 최상위 폴더 하나 안에 들어 있는지 확인합니다.</summary>
    public static bool HasSingleRootFolder(IEnumerable<string> entryNames)
    {
        string? root = null;
        bool any = false;

        foreach (string raw in entryNames)
        {
            string name = raw.Replace('\\', '/').TrimStart('/');
            if (name.Length == 0) continue;

            any = true;
            int slash = name.IndexOf('/');
            if (slash < 0) return false; // 최상위에 파일이 있음

            string first = name[..slash];
            if (root is null) root = first;
            else if (!string.Equals(root, first, StringComparison.Ordinal)) return false;
        }

        return any;
    }

    /// <summary>"a.tar.gz" 는 "a", "b.zip" 은 "b" 로 돌려줍니다.</summary>
    public static string ArchiveBaseName(string archivePath)
    {
        string name = Path.GetFileName(archivePath);
        string lower = name.ToLowerInvariant();

        foreach (string suffix in new[] { ".tar.gz", ".tar.bz2", ".tar.xz" })
        {
            if (lower.EndsWith(suffix)) return name[..^suffix.Length];
        }

        string stem = Path.GetFileNameWithoutExtension(name);
        // 분할 볼륨(a.zip.001 → a.zip → a)
        if (System.Text.RegularExpressions.Regex.IsMatch(Path.GetExtension(name), @"^\.\d{3,}$"))
        {
            stem = Path.GetFileNameWithoutExtension(stem);
        }

        foreach (char invalid in Path.GetInvalidFileNameChars()) stem = stem.Replace(invalid, '_');
        return stem.Trim().TrimEnd('.');
    }
}
