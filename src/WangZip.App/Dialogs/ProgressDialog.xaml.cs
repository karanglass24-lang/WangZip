using System.IO;
using System.Windows;
using WangZip.Core;

namespace WangZip.App.Dialogs;

public partial class ProgressDialog : Window
{
    private readonly CancellationTokenSource _cancellation = new();

    public ProgressDialog(string title)
    {
        InitializeComponent();
        TitleText.Text = title;
        Progress = new Progress<ProgressReport>(UpdateProgress);
    }

    public IProgress<ProgressReport> Progress { get; }

    private void UpdateProgress(ProgressReport report)
    {
        Bar.Value = report.Percent;
        PercentText.Text = $"{report.Percent:0.0}%";
        StatusText.Text = string.IsNullOrEmpty(report.CurrentName)
            ? "마무리하는 중..."
            : report.CurrentName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cancellation.Cancel();
        StatusText.Text = "취소하는 중...";
    }

    public static async Task<bool> RunAsync(
        Window owner,
        string title,
        Func<IProgress<ProgressReport>, CancellationToken, Task> work)
    {
        var dialog = new ProgressDialog(title) { Owner = owner };
        dialog.Show();

        try
        {
            await work(dialog.Progress, dialog._cancellation.Token);
            dialog.Close();
            return true;
        }
        catch (OperationCanceledException)
        {
            dialog.Close();
            MessageBox.Show(owner, "작업이 취소되었습니다.", "왕집", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        catch (ArchiveException exception)
        {
            dialog.Close();
            MessageBox.Show(owner, exception.Message, "왕집", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            dialog.Close();
            MessageBox.Show(owner, "접근 권한이 없습니다.\n" + exception.Message, "왕집", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch (IOException exception)
        {
            dialog.Close();
            MessageBox.Show(owner, "파일을 처리하는 중 오류가 발생했습니다.\n" + exception.Message, "왕집", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch (Exception exception)
        {
            dialog.Close();
            MessageBox.Show(owner, "예상하지 못한 오류가 발생했습니다.\n" + exception.GetType().Name + ": " + exception.Message,
                "왕집", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        finally
        {
            if (dialog.IsVisible) dialog.Close();
        }
    }
}
