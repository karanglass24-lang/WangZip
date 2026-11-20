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

    /// <summary>여러 압축 파일을 선택했을 때 나타나는 "모두 풀기" 동사. 항목마다 프로세스가 띄워집니다.</summary>
    private const string ExtractAllVerb = "WangZipExtractAll";

    /// <summary>왕집 서브메뉴 키 이름 (SystemFileAssociations 아래에 생성).</summary>
    private const string SubMenuKey = "WangZip";

    /// <summary>예전 버전이 만든 키·지원하지 않는 형식의 키. 항상 정리합니다.</summary>
    private static readonly string[] LegacyMenuKeys =
    [
        @"Software\Classes\zipfile\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.cab\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.iso\shell\WangZipExtract",
        // 기존 평면 메뉴 키들도 정리
        @"Software\Classes\SystemFileAssociations\.zip\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.rar\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.tar\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.gz\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.tgz\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.bz2\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.xz\shell\WangZipExtract",
        @"Software\Classes\SystemFileAssociations\.zip\shell\WangZipExtractAll",
        @"Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtractAll",
        @"Software\Classes\SystemFileAssociations\.rar\shell\WangZipExtractAll",
        @"Software\Classes\SystemFileAssociations\.tar\shell\WangZipExtractAll",
        @"Software\Classes\SystemFileAssociations\.gz\shell\WangZipExtractAll",
        @"Software\Classes\SystemFileAssociations\.tgz\shell\WangZipExtractAll",
        @"Software\Classes\SystemFileAssociations\.bz2\shell\WangZipExtractAll",
        @"Software\Classes\SystemFileAssociations\.xz\shell\WangZipExtractAll",
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
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\WangZip");
        using RegistryKey? oldKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\WangZipCreate");
        return key is not null || oldKey is not null;
    }

    public static void SetShellMenu(bool enabled, string exePath)
    {
        foreach (string legacy in LegacyMenuKeys) DeleteTree(legacy);

        // 압축 파일 확장자별 서브메뉴 등록
        foreach (string ext in ExtractMenuExtensions)
        {
            string basePath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{SubMenuKey}";
            if (enabled)
            {
                // 부모 메뉴: "왕집"
                WriteSubMenuParent(basePath, exePath);

                // 하위 메뉴: 여기에 풀기
                WriteMenu($@"{basePath}\shell\WangZipExtract", Loc.T("여기에 풀기"), $"\"{exePath}\" here-extract \"%1\"", exePath);

                // 하위 메뉴: 모두 풀기 (다중 선택 지원)
                WriteMenu($@"{basePath}\shell\WangZipExtractAll", Loc.T("모두 풀기"), $"\"{exePath}\" here-extract-all \"%1\"", exePath, multiSelect: true);

                // 하위 메뉴: ZIP으로 압축
                WriteMenu($@"{basePath}\shell\WangZipCompress", Loc.T("ZIP으로 압축"), $"\"{exePath}\" here-create \"%1\"", exePath);
            }
            else
            {
                DeleteTree(basePath);
            }
        }

        // 일반 파일에서도 압축할 수 있게 하되 압축 파일 메뉴와 중복되지 않게 합니다.
        const string files = @"Software\Classes\*\shell\WangZip";
        DeleteTree(@"Software\Classes\*\shell\WangZipCompress");
        if (enabled)
        {
            WriteSubMenuParent(files, exePath);
            SetValue(files, "AppliesTo", "NOT (" + string.Join(" OR ",
                ExtractMenuExtensions.Select(ext => $"System.FileExtension:=\"{ext}\"")) + ")");
            WriteMenu($@"{files}\shell\WangZipCompress", Loc.T("ZIP으로 압축"), $"\"{exePath}\" here-create \"%1\"", exePath);
        }
        else
        {
            DeleteTree(files);
        }

        // 이전 버전의 독립 메뉴를 제거하고 설치 프로그램과 같은 키를 사용합니다.
        DeleteTree(@"Software\Classes\Directory\shell\WangZipCreate");
        DeleteTree(@"Software\Classes\Directory\Background\shell\WangZipCreateBackground");
        const string directory = @"Software\Classes\Directory\shell\WangZip";
        const string background = @"Software\Classes\Directory\Background\shell\WangZip";
        if (enabled)
        {
            WriteSubMenuParent(directory, exePath);
            WriteMenu($@"{directory}\shell\WangZipCreate", Loc.T("이 폴더 압축"), $"\"{exePath}\" here-create \"%1\"", exePath);

            WriteSubMenuParent(background, exePath);
            WriteMenu($@"{background}\shell\WangZipCreateBackground", Loc.T("이 폴더 압축"), $"\"{exePath}\" here-create \"%V\"", exePath);
        }
        else
        {
            DeleteTree(directory);
            DeleteTree(background);
        }

        NotifyChanged();
    }

    /// <summary>캐스케이드(서브메뉴) 부모 키를 생성합니다.</summary>
    private static void WriteSubMenuParent(string path, string exePath)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(path);
        key.DeleteValue("", false);
        key.DeleteValue("SubCommands", false);
        key.SetValue("MUIVerb", Loc.T("왕집"));
        SetValue(path, "Icon", exePath);
        // ExtendedSubCommandsKey는 HKCR 기준이며 그 키의 shell 하위에서 명령을 찾습니다.
        SetValue(path, "ExtendedSubCommandsKey", path[@"Software\Classes\".Length..]);
    }

    private static void WriteMenu(string path, string label, string command, string exePath, bool multiSelect = false)
    {
        SetDefault(path, label);
        SetValue(path, "Icon", exePath);
        // Player: 여러 파일을 선택해도 메뉴가 보이고, 선택한 항목마다 프로세스가 실행됩니다.
        if (multiSelect) SetValue(path, "MultiSelectModel", "Player");
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
