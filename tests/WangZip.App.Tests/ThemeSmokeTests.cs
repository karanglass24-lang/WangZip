using System.IO;
using System.Windows;
using System.Windows.Media;
using WangZip.App.Dialogs;

namespace WangZip.App.Tests;

/// <summary>
/// 화면에 띄우지 않고 창을 만들어, XAML 로드 오류와 테마별 글자색 회귀(다크에서 글자가 어둡게 나오는 문제)를 잡습니다.
/// 마우스 동작이나 탐색기 연동은 확인하지 못합니다.
/// </summary>
public class ThemeSmokeTests
{
    private static Color Resource(string key) => ((SolidColorBrush)StaHost.Current.Resources[key]).Color;

    private static Window[] CreateWindows() =>
    [
        new ExtractDialog(Path.GetTempPath(), 0, false, new AppSettings()),
        new CreateDialog(),
        new AboutDialog(),
        new PasswordDialog(),
        new PreferencesDialog(new AppSettings()),
        new ProgressDialog("test"),
        new PreviewDialog(PreviewSampleFile(), "sample.txt"),
        new MainWindow(),
    ];

    private static string PreviewSampleFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "wangzip-preview-smoke.txt");
        File.WriteAllText(path, "hello, 안녕하세요");
        return path;
    }

    private static IEnumerable<T> LogicalDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject node) continue;
            if (node is T match) yield return match;
            foreach (T nested in LogicalDescendants<T>(node)) yield return nested;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Every_window_loads_and_uses_theme_text_color(int theme)
    {
        StaHost.Run(() =>
        {
            App.ApplyTheme(theme);
            Color expected = Resource("TextPrimaryBrush");

            foreach (Window window in CreateWindows())
            {
                try
                {
                    var brush = Assert.IsType<SolidColorBrush>(window.Foreground);
                    Assert.True(
                        expected == brush.Color,
                        $"{window.GetType().Name}: 글자색 {brush.Color} (기대 {expected}, 테마 {theme})");
                }
                finally
                {
                    window.Close();
                }
            }
        });
    }

    /// <summary>모달 대화상자는 Esc로 닫을 수 있어야 합니다 (IsCancel 단추). 진행 창은 Esc가 작업 취소입니다.</summary>
    [Fact]
    public void Every_dialog_has_a_cancel_button_for_escape()
    {
        StaHost.Run(() =>
        {
            Window[] dialogs =
            [
                new ExtractDialog(Path.GetTempPath(), 0, false, new AppSettings()),
                new CreateDialog(),
                new AboutDialog(),
                new PasswordDialog(),
                new PreferencesDialog(new AppSettings()),
                new ProgressDialog("test"),
            ];

            foreach (Window dialog in dialogs)
            {
                try
                {
                    Assert.True(
                        LogicalDescendants<System.Windows.Controls.Button>(dialog).Any(b => b.IsCancel),
                        $"{dialog.GetType().Name}: IsCancel 단추가 없습니다");
                }
                finally
                {
                    dialog.Close();
                }
            }
        });
    }

    /// <summary>다크 테마에서 창 배경이 흰색으로 남지 않아야 합니다 (밝은 글자가 흰 배경에 묻히는 문제).</summary>
    [Fact]
    public void Dialog_backgrounds_follow_dark_theme()
    {
        StaHost.Run(() =>
        {
            App.ApplyTheme(2);
            Color surface = Resource("SurfaceBrush");

            foreach (Window window in CreateWindows())
            {
                try
                {
                    if (window is MainWindow) continue;   // 메인 창은 각 영역이 직접 배경을 칠합니다.

                    // 진행 창은 투명 창 안의 테두리가 배경입니다.
                    Brush? background = window is ProgressDialog
                        ? ((System.Windows.Controls.Border)window.Content).Background
                        : window.Background;
                    var brush = Assert.IsType<SolidColorBrush>(background);
                    Assert.True(surface == brush.Color, $"{window.GetType().Name}: 배경 {brush.Color} (기대 {surface})");
                }
                finally
                {
                    window.Close();
                }
            }
        });
    }

    [Fact]
    public void Dark_and_light_text_colors_differ()
    {
        StaHost.Run(() =>
        {
            App.ApplyTheme(1);
            Color light = Resource("TextPrimaryBrush");
            App.ApplyTheme(2);
            Color dark = Resource("TextPrimaryBrush");
            Assert.NotEqual(light, dark);
        });
    }

    [Fact]
    public void Themed_controls_follow_dark_theme()
    {
        StaHost.Run(() =>
        {
            App.ApplyTheme(2);
            Color expected = Resource("TextPrimaryBrush");

            var checkBox = new System.Windows.Controls.CheckBox { Content = "x" };
            var radio = new System.Windows.Controls.RadioButton { Content = "x" };
            var host = new Window { Content = new System.Windows.Controls.StackPanel { Children = { checkBox, radio } } };
            host.Resources.MergedDictionaries.Clear();

            Assert.Equal(expected, ((SolidColorBrush)checkBox.Foreground).Color);
            Assert.Equal(expected, ((SolidColorBrush)radio.Foreground).Color);
            host.Close();
        });
    }
}
