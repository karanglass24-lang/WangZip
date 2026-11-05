using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;

namespace WangZip.App;

/// <summary>확인된 새 버전 정보.</summary>
public sealed record UpdateInfo(
    string CurrentVersion,
    string LatestVersion,
    string VersionTag,
    string AssetName,
    string AssetUrl,
    long AssetSize,
    string? Sha256);

/// <summary>
/// GitHub Releases를 한 번 확인해 새 버전을 찾고, 설치형 배포본을 받아 끝냅니다.
/// 요청은 최초 확인 시 1회이며 다른 정보를 전송하지 않습니다.
/// </summary>
public static class UpdateService
{
    private const string ReleasesApi = "https://api.github.com/repos/karanglass24-lang/WangZip/releases/latest";
    public const string ReleasePage = "https://github.com/karanglass24-lang/WangZip/releases/latest";

    public static string CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";

    /// <summary>설치형(제거 도구 존재) 여부.</summary>
    public static bool IsInstalled() =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    public static async Task<UpdateInfo?> CheckLatestAsync()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"WangZip/{CurrentVersion}");
        http.Timeout = TimeSpan.FromSeconds(30);
        using var response = await http.GetAsync(ReleasesApi);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return Parse(document.RootElement);
    }

    internal static UpdateInfo? Parse(System.Text.Json.JsonElement root)
    {
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        string versionText = tag.TrimStart('v', 'V');
        if (!Version.TryParse(versionText, out _))
        {
            return null;
        }

        if (!root.TryGetProperty("assets", out var assets)) return null;

        foreach (var asset in assets.EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? "";
            if (!name.StartsWith($"WangZip-{versionText}-setup") || !name.EndsWith(".exe")) continue;

            string? digest = asset.TryGetProperty("digest", out var digestEl)
                ? digestEl.GetString()
                : null;
            int separatorAt = digest?.IndexOf(':') ?? -1;
            digest = digest is null || separatorAt < 0
                ? null
                : digest![(separatorAt + 1)..];

            return new UpdateInfo(
                CurrentVersion,
                versionText,
                tag,
                name,
                asset.GetProperty("browser_download_url").GetString() ?? "",
                asset.GetProperty("size").GetInt64(),
                digest);
        }

        return null;
    }

    public static bool IsNewer(UpdateInfo info) =>
        Version.Parse(info.LatestVersion) > Version.Parse(info.CurrentVersion);

    /// <summary>새 설치본을 내려받아 SHA-256을 대조하고 마칩니다.</summary>
    public static async Task<string> DownloadAsync(
        UpdateInfo info,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        string directory = Path.Combine(Path.GetTempPath(), "WangZip", "update");
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, info.AssetName);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"WangZip/{CurrentVersion}");
        http.Timeout = TimeSpan.FromMinutes(10);

        using (var response = await http.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024);

            var buffer = new byte[256 * 1024];
            long done = 0;
            long total = response.Content.Headers.ContentLength ?? info.AssetSize;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                done += read;
                progress?.Report(total > 0 ? done * 100.0 / total : 0);
            }
        }

        if (info.Sha256 is { } expected)
        {
            byte[] hash;
            await using (var stream = File.OpenRead(target))
            {
                hash = await SHA256.HashDataAsync(stream, cancellationToken);
            }

            string actual = Convert.ToHexString(hash).ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(target);
                throw new InvalidDataException("다운로드한 업데이트 파일이 손상되었습니다. 잠시 후 다시 시도해 주세요.");
            }
        }

        return target;
    }

    /// <summary>설치 프로그램을 조용히 실행하고, 파일 교체를 위해 지금 프로그램을 종료합니다.
    /// /RESTARTAPPLICATIONS로 설치기가 종료 후 전 프로그램을 다시 실행합니다.</summary>
    public static void InstallAndRestart(string setupPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = setupPath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /RESTARTAPPLICATIONS",
            UseShellExecute = true,
        });

        Application.Current?.Shutdown();
    }

    public static void OpenReleasePage() =>
        Process.Start(new ProcessStartInfo(ReleasePage) { UseShellExecute = true });
}
