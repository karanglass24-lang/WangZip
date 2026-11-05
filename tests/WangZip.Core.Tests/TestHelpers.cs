using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Initialize() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
}

internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "WangZip-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Write(string relativePath, string text)
    {
        string full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, text, new UTF8Encoding(false));
    }

    public void WriteBytes(string relativePath, byte[] bytes)
    {
        string full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllBytes(full, bytes);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

internal static class TestArchives
{
    static TestArchives()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static void CreateZip(string path, IReadOnlyList<string> entryNames, Encoding? nameEncoding)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: nameEncoding);

        foreach (string name in entryNames)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using Stream stream = entry.Open();
            byte[] payload = Encoding.UTF8.GetBytes("내용:" + name);
            stream.Write(payload);
        }
    }
}
