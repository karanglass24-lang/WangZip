using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WangZip.Core;

namespace WangZip.App;

/// <summary>
/// 탐색기 통합(HKCU만 사용, 관리자 권한 불필요): "연결 프로그램" 등록과 우클릭 메뉴.
/// 설치 프로그램(installer\wangzip.iss)이 만드는 키와 같은 구성입니다.
/// </summary>
internal static class ShellIntegration
{
    public const string ProgId = "WangZip.Archive";
    private const string AppKeyName = "WangZip.exe";
    private const string CapabilitiesPath = @"Software\WangZip\Capabilities";

    /// <summary>"연결 프로그램"으로 등록할 수 있는 확장자.</summary>
    public static readonly string[] AssociationExtensions =
        [".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz"];

    /// <summary>
    /// 우클릭 "여기에 풀기"를 붙일 확장자. ProgID와 무관하게 동작하는 SystemFileAssociations 키를 씁니다
    /// (.zip의 ProgID는 PC마다 달라서 zipfile 키는 쓰이지 않는 경우가 많습니다).
    /// </summary>
    private static readonly string[] ExtractMenuExtensions =
        [".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz"];

    /// <summary>예전 버전이 만든 키·지원하지 않는 형식의 키. 항상 정리합니다.</summary>
    private static readonly string[] LegacyMenuKeys =
    [
        @"Software\Classes\zipfile\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.cab\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.iso\shell\WangZipExtract",
    ];

    // ───────── 연결 프로그램 ─────────

    public static bool IsAssociated(string extension)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}\OpenWithProgids");
        return key?.GetValue(ProgId) is not null;
    }

    /// <summary>선택한 확장자만 연결 프로그램으로 등록하고, 나머지는 등록 해제합니다.</summary>
    public static void SetAssociations(IReadOnlyCollection<string> selected, string exePath)
    {
        if (selected.Count == 0)
        {
            foreach (string extension in AssociationExtensions) RemoveAssociation(extension);
            DeleteTree(@"Software\Classes\" + ProgId);
            DeleteTree(@"Software\Classes\Applications\" + AppKeyName);
            DeleteTree(@"Software\WangZip\Capabilities");
            using (RegistryKey? registered = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", writable: true))
            {
                registered?.DeleteValue("WangZip", throwOnMissingValue: false);
            }

            NotifyChanged();
            return;
        }

        string open = $"\"{exePath}\" \"%1\"";
        string icon = $"{exePath},0";

        SetDefault($@"Software\Classes\{ProgId}", Loc.T("왕집 압축 파일"));
        SetDefault($@"Software\Classes\{ProgId}\DefaultIcon", icon);
        SetDefault($@"Software\Classes\{ProgId}\shell\open\command", open);

        string appRoot = $@"Software\Classes\Applications\{AppKeyName}";
        SetValue(appRoot, "FriendlyAppName", Loc.T("왕집"));
        SetDefault($@"{appRoot}\DefaultIcon", icon);
        SetDefault($@"{appRoot}\shell\open\command", open);

        SetValue(CapabilitiesPath, "ApplicationName", Loc.T("왕집"));
        SetValue(CapabilitiesPath, "ApplicationDescription", Loc.T("광고 없는 오픈소스 압축 프로그램"));
        using (RegistryKey registered = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
        {
            registered.SetValue("WangZip", CapabilitiesPath);
        }

        foreach (string extension in AssociationExtensions)
        {
            if (selected.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                SetValue($@"Software\Classes\{extension}\OpenWithProgids", ProgId, "");
                SetValue($@"{appRoot}\SupportedTypes", extension, "");
                SetValue($@"{CapabilitiesPath}\FileAssociations", extension, ProgId);
            }
            else
            {
                RemoveAssociation(extension);
            }
        }

        NotifyChanged();
    }

    private static void RemoveAssociation(string extension)
    {
        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}\OpenWithProgids", writable: true))
        {
            key?.DeleteValue(ProgId, throwOnMissingValue: false);
        }

        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\Applications\{AppKeyName}\SupportedTypes", writable: true))
        {
            key?.DeleteValue(extension, throwOnMissingValue: false);
        }

        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"{CapabilitiesPath}\FileAssociations", writable: true))
        {
            key?.DeleteValue(extension, throwOnMissingValue: false);
        }
    }

    /// <summary>Windows 설정의 기본 앱 화면을 엽니다 (기본 프로그램 지정은 사용자만 할 수 있습니다).</summary>
    public static void OpenDefaultAppsSettings()
    {
        Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
    }

    // ───────── 우클릭 메뉴 ─────────

    public static bool IsShellMenuEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\WangZipCreate");
        return key is not null;
    }

    public static void SetShellMenu(bool enabled, string exePath)
    {
        foreach (string legacy in LegacyMenuKeys) DeleteTree(legacy);

        var extractTargets = ExtractMenuExtensions
            .Select(e => $@"Software\Classes\SystemFileAssociations\{e}\shell\WangZipExtract")
            .ToList();

        string extractLabel = Loc.T("왕집: 여기에 풀기");
        string createLabel = Loc.T("왕집: 이 폴더 압축");

        foreach (string path in extractTargets)
        {
            if (enabled) WriteMenu(path, extractLabel, $"\"{exePath}\" here-extract \"%1\"", exePath);
            else DeleteTree(path);
        }

        const string file = @"Software\Classes\*\shell\WangZipCompress";
        if (enabled) WriteMenu(file, Loc.T("왕집: ZIP으로 압축"), $"\"{exePath}\" here-create \"%1\"", exePath);
        else DeleteTree(file);

        const string directory = @"Software\Classes\Directory\shell\WangZipCreate";
        const string background = @"Software\Classes\Directory\Background\shell\WangZipCreateBackground";
        if (enabled)
        {
            WriteMenu(directory, createLabel, $"\"{exePath}\" here-create \"%1\"", exePath);
            WriteMenu(background, createLabel, $"\"{exePath}\" here-create \"%V\"", exePath);
        }
        else
        {
            DeleteTree(directory);
            DeleteTree(background);
        }

        NotifyChanged();
    }

    private static void WriteMenu(string path, string label, string command, string exePath)
    {
        SetDefault(path, label);
        SetValue(path, "Icon", exePath);
        SetDefault(path + @"\command", command);
    }

    // ───────── 레지스트리 도우미 ─────────

    private static void SetDefault(string path, string value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(path);
        key.SetValue("", value);
    }

    private static void SetValue(string path, string name, string value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(path);
        key.SetValue(name, value);
    }

    private static void DeleteTree(string path)
    {
        Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
    }

    private static void NotifyChanged()
    {
        const int SHCNE_ASSOCCHANGED = 0x08000000;
        SHChangeNotify(SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
