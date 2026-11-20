using System.Windows;
using System.Windows.Controls;
using WangZip.Core;

namespace WangZip.App.Tests;

/// <summary>메인 창 메뉴 구성을 확인합니다 (화면에 띄우지 않고 XAML만 읽습니다).</summary>
public class MainWindowMenuTests
{
    private static readonly string[] ExpectedHeaders =
        ["파일(_F)", "편집(_E)", "도구(_T)", "보기(_V)", "도움말(_H)"];

    private static T WithKorean<T>(Func<T> work)
    {
        string previous = Loc.Language;
        Loc.Init("ko");
        try { return work(); }
        finally { Loc.Init(previous); }
    }

    private static char? AccessKey(string header)
    {
        for (int i = 0; i < header.Length - 1; i++)
        {
            if (header[i] != '_') continue;
            if (header[i + 1] == '_') { i++; continue; }
            return char.ToUpperInvariant(header[i + 1]);
        }

        return null;
    }

    [Fact]
    public void Menu_bar_has_five_top_level_headers_in_order()
    {
        StaHost.Run(() =>
        {
            WithKorean(() =>
            {
                var window = new MainWindow();
                try
                {
                    var menu = Assert.IsType<Menu>(window.FindName("MainMenu"));
                    string[] headers = menu.Items.OfType<MenuItem>().Select(item => (string)item.Header).ToArray();
                    Assert.Equal(ExpectedHeaders, headers);
                }
                finally
                {
                    window.Close();
                }

                return 0;
            });
        });
    }

    [Fact]
    public void Menus_have_no_duplicate_access_keys()
    {
        StaHost.Run(() =>
        {
            WithKorean(() =>
            {
                var window = new MainWindow();
                try
                {
                    var menu = Assert.IsType<Menu>(window.FindName("MainMenu"));
                    CheckScope(menu.Items.OfType<MenuItem>().ToList(), "메뉴 막대");
                }
                finally
                {
                    window.Close();
                }

                return 0;
            });
        });
    }

    /// <summary>영어 모드에서 메뉴·도구 모음·우클릭 메뉴 문구가 한국어로 남지 않아야 합니다 (번역 누락 확인).</summary>
    [Fact]
    public void English_mode_leaves_no_korean_in_main_window_chrome()
    {
        StaHost.Run(() =>
        {
            string previous = Loc.Language;
            Loc.Init("en");
            try
            {
                var window = new MainWindow();
                try
                {
                    var leftovers = new List<string>();
                    Scan(window, leftovers);
                    Assert.True(leftovers.Count == 0, "번역 누락: " + string.Join(" | ", leftovers.Distinct()));
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                Loc.Init(previous);
            }
        });
    }

    [Fact]
    public void English_mode_leaves_no_korean_in_preferences()
    {
        StaHost.Run(() =>
        {
            string previous = Loc.Language;
            Loc.Init("en");
            try
            {
                var dialog = new WangZip.App.Dialogs.PreferencesDialog(new AppSettings());
                try
                {
                    var leftovers = new List<string>();
                    Scan(dialog, leftovers);
                    Assert.True(leftovers.Count == 0, "번역 누락: " + string.Join(" | ", leftovers.Distinct()));
                }
                finally
                {
                    dialog.Close();
                }
            }
            finally
            {
                Loc.Init(previous);
            }
        });
    }

    [Fact]
    public void Old_settings_file_without_new_fields_keeps_previous_behavior()
    {
        AppSettings settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
            "{\"Language\":\"en\",\"SubfolderMode\":1,\"Theme\":2}")!;

        Assert.Equal("en", settings.Language);
        Assert.Equal(1, settings.SubfolderMode);
        Assert.Equal(2, settings.Theme);
        Assert.Equal(0, settings.DefaultOverwrite);
        Assert.Equal(2, settings.DefaultCompressionLevel);
        Assert.Equal(0, settings.DefaultNameEncoding);
        Assert.Equal(0, settings.DefaultSplitSizeIndex);
        Assert.Equal(0, settings.DefaultListEncoding);
        Assert.True(settings.ConfirmBeforeDelete);
        Assert.True(settings.ShowCompletionDialog);
        Assert.False(settings.ExtractShellUsesAppSettings);
    }

    private static bool HasHangul(string? text) =>
        text is not null && text.Any(c => c is >= '가' and <= '힣');

    private static void Scan(DependencyObject node, List<string> leftovers)
    {
        switch (node)
        {
            case MenuItem { Tag: string tag } when System.IO.Path.IsPathRooted(tag):
                return;   // 최근 항목(사용자 경로)은 번역 대상이 아닙니다.
            case MenuItem { Header: string header } when HasHangul(header):
                leftovers.Add(header);
                break;
            case TextBlock { Text: var text } when HasHangul(text) &&
                !System.Windows.Data.BindingOperations.IsDataBound(node, TextBlock.TextProperty):
                leftovers.Add(text);
                break;
            case HeaderedContentControl { Header: string tabHeader } when HasHangul(tabHeader):
                leftovers.Add(tabHeader);
                break;
            case ContentControl { Content: string content } when HasHangul(content) &&
                node is not ComboBoxItem { Tag: "ko" }:   // 언어 목록의 '한국어'는 일부러 한국어로 둡니다.
                leftovers.Add(content);
                break;
        }

        if (node is FrameworkElement element)
        {
            if (element.ToolTip is string tip && HasHangul(tip)) leftovers.Add(tip);
            if (element.ContextMenu is { } menu)
            {
                foreach (object item in menu.Items)
                {
                    if (item is DependencyObject child) Scan(child, leftovers);
                }
            }
        }

        foreach (object child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is DependencyObject dependency) Scan(dependency, leftovers);
        }
    }

    private static void CheckScope(IReadOnlyList<MenuItem> items, string where)
    {
        var seen = new Dictionary<char, string>();
        foreach (MenuItem item in items)
        {
            if (item.Header is string header)
            {
                if (AccessKey(header) is char key)
                {
                    Assert.False(seen.TryGetValue(key, out string? other),
                        $"{where}: 단축키 '{key}' 중복 ('{other}', '{header}')");
                    seen[key] = header;
                }

                CheckScope(item.Items.OfType<MenuItem>().ToList(), header);
            }
        }
    }
}
