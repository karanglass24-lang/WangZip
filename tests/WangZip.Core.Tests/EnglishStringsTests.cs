using System.Text.RegularExpressions;

namespace WangZip.Core.Tests;

public class EnglishStringsTests
{
    private static string SourcePath()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "WangZip.slnx")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!, "src", "WangZip.Core", "EnglishStrings.cs");
    }

    /// <summary>Dictionary 초기화(["키"] = ...)는 중복 키가 있어도 컴파일되고 뒤의 값이 조용히 이기므로, 원본 파일에서 직접 검사합니다.</summary>
    [Fact]
    public void Every_key_is_unique()
    {
        var keys = new List<string>();
        foreach (string line in File.ReadAllLines(SourcePath()))
        {
            Match match = Regex.Match(line, "^\\s*\\[\"((?:[^\"\\\\]|\\\\.)*)\"\\]\\s*=");
            if (match.Success) keys.Add(match.Groups[1].Value);
        }

        Assert.True(keys.Count > 100);
        string[] duplicates = keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.True(duplicates.Length == 0, "중복 키: " + string.Join(" | ", duplicates));
    }
}
