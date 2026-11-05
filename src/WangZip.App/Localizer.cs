using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WangZip.Core;

namespace WangZip.App;

/// <summary>
/// XAML에 한국어로 적힌 문구를 영어 모드에서 번역합니다 (창을 만든 직후 한 번 호출).
/// 한국어 모드에서는 아무 일도 하지 않습니다.
/// </summary>
internal static class Localizer
{
    public static void Apply(DependencyObject root)
    {
        if (!Loc.IsEnglish) return;
        Walk(root);
    }

    private static void Walk(DependencyObject node)
    {
        // 번역하면서 컬렉션이 바뀔 수 있으므로 자식 목록을 먼저 복사해 둡니다.
        List<object> children = LogicalTreeHelper.GetChildren(node).Cast<object>().ToList();

        switch (node)
        {
            case Window window:
                window.Title = Loc.T(window.Title);
                break;
            case TextBlock textBlock when textBlock.Inlines.Count <= 1:
                // Run이 여러 개인 TextBlock은 아래 Run 처리에서 번역합니다. 바인딩된 값은 건드리지 않습니다.
                bool bound = System.Windows.Data.BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty) ||
                             (textBlock.Inlines.FirstInline is Run first &&
                              System.Windows.Data.BindingOperations.IsDataBound(first, Run.TextProperty));
                if (!bound && !string.IsNullOrEmpty(textBlock.Text))
                {
                    textBlock.Text = Loc.T(textBlock.Text);
                }

                break;
            case Run run:
                // TextBlock.Text로 지정한 문구도 내부적으로 Run 하나로 만들어집니다. 바인딩된 값은 건드리지 않습니다.
                if (!System.Windows.Data.BindingOperations.IsDataBound(run, Run.TextProperty))
                {
                    run.Text = Loc.T(run.Text);
                }

                break;
            case MenuItem menuItem:
                if (menuItem.Header is string header) menuItem.Header = Loc.T(header);
                break;
            case ContentControl content:
                if (content.Content is string text) content.Content = Loc.T(text);
                break;
            case DataGrid grid:
                foreach (DataGridColumn column in grid.Columns)
                {
                    if (column.Header is string columnHeader) column.Header = Loc.T(columnHeader);
                }

                break;
        }

        if (node is FrameworkElement element)
        {
            if (element.ToolTip is string tip) element.ToolTip = Loc.T(tip);

            if (element.ContextMenu is { } menu)
            {
                foreach (object item in menu.Items.Cast<object>().ToList())
                {
                    if (item is DependencyObject dependency) Walk(dependency);
                }
            }
        }

        foreach (object child in children)
        {
            if (child is DependencyObject dependency) Walk(dependency);
        }
    }
}
