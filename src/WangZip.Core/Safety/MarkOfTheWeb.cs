namespace WangZip.Core.Safety;

/// <summary>
/// 인터넷에서 받은 압축 파일의 "인터넷 영역 표시"(Zone.Identifier)를 풀어낸 파일에도 이어 줍니다.
/// 이 표시가 있어야 SmartScreen·Office 보호된 보기가 풀린 파일에도 동작합니다.
/// NTFS가 아닌 곳이나 표시가 없는 파일에서는 조용히 아무 일도 하지 않습니다.
/// </summary>
public static class MarkOfTheWeb
{
    private const string StreamName = ":Zone.Identifier";

    public static byte[]? Read(string archivePath)
    {
        try
        {
            string path = PathGuard.ToExtended(Path.GetFullPath(archivePath)) + StreamName;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.Length is > 0 and < 4096 ? memory.ToArray() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Apply(byte[]? zone, string targetPath)
    {
        if (zone is null) return;

        try
        {
            string path = PathGuard.ToExtended(targetPath) + StreamName;
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(zone);
        }
        catch (Exception)
        {
            // 표시를 못 남겨도 풀기 자체는 성공으로 둡니다.
        }
    }

    /// <summary>압축 파일에 표시가 있으면 target에 복사합니다.</summary>
    public static void Copy(string archivePath, string targetPath) => Apply(Read(archivePath), targetPath);
}
