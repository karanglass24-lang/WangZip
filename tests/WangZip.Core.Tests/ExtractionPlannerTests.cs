namespace WangZip.Core.Tests;

public class ExtractionPlannerTests
{
    [Fact]
    public void Never_keeps_destination() =>
        Assert.Equal(@"C:\out", ExtractionPlanner.ResolveDestination(@"C:\out", @"D:\a\x.zip", ["x/a.txt"], SubfolderMode.Never));

    [Fact]
    public void Auto_skips_folder_when_single_root()
    {
        string result = ExtractionPlanner.ResolveDestination(@"C:\out", @"D:\a\x.zip", ["proj/", "proj/a.txt", "proj/sub/b.txt"], SubfolderMode.Auto);
        Assert.Equal(@"C:\out", result);
    }

    [Fact]
    public void Auto_creates_folder_when_multiple_roots_or_loose_files()
    {
        Assert.Equal(Path.Combine(@"C:\out", "x"),
            ExtractionPlanner.ResolveDestination(@"C:\out", @"D:\a\x.zip", ["a.txt", "b/c.txt"], SubfolderMode.Auto));
        Assert.Equal(Path.Combine(@"C:\out", "x"),
            ExtractionPlanner.ResolveDestination(@"C:\out", @"D:\a\x.zip", ["p/a.txt", "q/b.txt"], SubfolderMode.Auto));
    }

    [Fact]
    public void Always_creates_folder_even_with_single_root() =>
        Assert.Equal(Path.Combine(@"C:\out", "x"),
            ExtractionPlanner.ResolveDestination(@"C:\out", @"D:\a\x.zip", ["p/a.txt"], SubfolderMode.Always));

    [Theory]
    [InlineData("a.tar.gz", "a")]
    [InlineData("b.zip", "b")]
    [InlineData("c.zip.001", "c")]
    [InlineData("d.z01", "d")]
    public void ArchiveBaseName_strips_known_suffixes(string file, string expected) =>
        Assert.Equal(expected, ExtractionPlanner.ArchiveBaseName(@"D:\x\" + file));
}
