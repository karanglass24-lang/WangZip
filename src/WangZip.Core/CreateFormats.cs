namespace WangZip.Core;

public static class CreateFormats
{
    public static string Extension(CreateFormat format) => format switch
    {
        CreateFormat.SevenZip => ".7z",
        CreateFormat.Tar => ".tar",
        CreateFormat.TarGz => ".tar.gz",
        CreateFormat.TarBz2 => ".tar.bz2",
        _ => ".zip",
    };

    public static string DisplayName(CreateFormat format) => format switch
    {
        CreateFormat.SevenZip => "7z",
        CreateFormat.Tar => "TAR",
        CreateFormat.TarGz => "TAR.GZ",
        CreateFormat.TarBz2 => "TAR.BZ2",
        _ => "ZIP",
    };

    /// <summary>출력 파일 이름의 확장자로 형식을 정합니다. 알 수 없으면 ZIP.</summary>
    public static CreateFormat FromPath(string path)
    {
        string lower = path.ToLowerInvariant();
        if (lower.EndsWith(".7z")) return CreateFormat.SevenZip;
        if (lower.EndsWith(".tar.gz") || lower.EndsWith(".tgz")) return CreateFormat.TarGz;
        if (lower.EndsWith(".tar.bz2") || lower.EndsWith(".tbz2")) return CreateFormat.TarBz2;
        if (lower.EndsWith(".tar")) return CreateFormat.Tar;
        return CreateFormat.Zip;
    }

    /// <summary>경로에서 알려진 압축 확장자를 떼어 내고 새 형식의 확장자를 붙입니다.</summary>
    public static string ChangeExtension(string path, CreateFormat format)
    {
        string lower = path.ToLowerInvariant();
        foreach (string known in new[] { ".tar.gz", ".tar.bz2", ".tgz", ".tbz2", ".tar", ".7z", ".zip" })
        {
            if (lower.EndsWith(known)) return path[..^known.Length] + Extension(format);
        }

        return path + Extension(format);
    }
}
