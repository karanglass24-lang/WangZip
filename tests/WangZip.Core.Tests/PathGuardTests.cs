using WangZip.Core.Safety;

namespace WangZip.Core.Tests;

public class PathGuardTests
{
    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("a/../../evil.txt")]
    [InlineData("..\\evil.txt")]
    public void Traversal_entries_are_rejected(string entryName)
    {
        using var dir = new TempDir();
        var guard = new PathGuard(dir.Path);

        Assert.Throws<UnsafeArchiveEntryException>(() => guard.Resolve(entryName));
    }

    [Fact]
    public void Absolute_posix_path_is_confined_to_root()
    {
        using var dir = new TempDir();
        var guard = new PathGuard(dir.Path);

        string resolved = guard.Resolve("/etc/passwd")!;

        Assert.StartsWith(dir.Path, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("etc", "passwd"), resolved);
    }

    [Fact]
    public void Drive_letter_is_removed()
    {
        using var dir = new TempDir();
        var guard = new PathGuard(dir.Path);

        string resolved = guard.Resolve("C:\\Windows\\System32\\evil.dll")!;

        Assert.StartsWith(dir.Path, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("Windows", "System32", "evil.dll"), resolved);
    }

    [Theory]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("nul", "_nul")]
    [InlineData("COM1.log", "_COM1.log")]
    public void Reserved_device_names_are_prefixed(string entryName, string expected)
    {
        using var dir = new TempDir();
        var guard = new PathGuard(dir.Path);

        string resolved = guard.Resolve(entryName)!;

        Assert.Equal(expected, Path.GetFileName(resolved));
    }

    [Fact]
    public void Alternate_data_stream_separator_is_sanitized()
    {
        using var dir = new TempDir();
        var guard = new PathGuard(dir.Path);

        string resolved = guard.Resolve("file.txt:evil")!;

        Assert.Equal("file.txt_evil", Path.GetFileName(resolved));
    }

    [Fact]
    public void Trailing_dots_and_spaces_are_removed()
    {
        using var dir = new TempDir();
        var guard = new PathGuard(dir.Path);

        string resolved = guard.Resolve("name. ") !;

        Assert.Equal("name", Path.GetFileName(resolved));
    }

    [Fact]
    public void Case_insensitive_collisions_are_renamed()
    {
        using var dir = new TempDir();
        var guard = new PathGuard(dir.Path);

        string first = guard.Resolve("Report.txt")!;
        string second = guard.Resolve("report.txt")!;

        Assert.NotEqual(first, second, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("(2)", second);
    }

    [Fact]
    public void Long_paths_get_extended_prefix()
    {
        string longPath = @"C:\" + new string('a', 300);

        string extended = PathGuard.ToExtended(longPath);

        Assert.StartsWith(@"\\?\", extended);
    }
}
