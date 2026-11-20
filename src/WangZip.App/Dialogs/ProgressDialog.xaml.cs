using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WangZip.Core;

namespace WangZip.App.Dialogs;

public partial class ProgressDialog : Window
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly PauseGate _gate = new();
    private double _lastPercent;
    private bool _waiting;

    // 한 번에 하나의 작업만 실행하고 나머지는 대기열에서 차례를 기다립니다.
    private static readonly SemaphoreSlim QueueLock = new(1, 1);
    private static int _queued;

    public ProgressDialog(string title)
    {
        InitializeComponent();
        Localizer.Apply(this);
        TitleText.Text = title;
        Progress = new Progress<ProgressReport>(UpdateProgress);

        // 진행 보고가 잠시 멈춰도 경과 시간은 계속 흐르도록 1초마다 갱신합니다.
        _clock.Tick += (_, _) => UpdateTimeText();
        Loaded += (_, _) => _clock.Start();
        Closed += (_, _) => _clock.Stop();
    }

    public IProgress<ProgressReport> Progress { get; }

    private void UpdateProgress(ProgressReport report)
    {
        Bar.Value = report.Percent;
        PercentText.Text = $"{report.Percent:0.0}%";
        _lastPercent = report.Percent;

        // 취소를 누른 뒤에는 "취소하는 중..." 안내를 덮어쓰지 않습니다.
        if (!_cancellation.IsCancellationRequested && !_waiting)
        {
            StatusText.Text = string.IsNullOrEmpty(report.CurrentName)
                ? Loc.T("마무리하는 중...")
                : report.CurrentName;
        }

        UpdateTimeText();
    }

    /// <summary>경과 시간과, 충분히 진행됐을 때만 대략적인 남은 시간을 보여 줍니다.</summary>
    private void UpdateTimeText()
    {
        if (_waiting) return;

        TimeSpan elapsed = _elapsed.Elapsed;
        double percent = _lastPercent;
        if (percent >= 1 && percent < 100 && elapsed.TotalSeconds >= 3)
        {
            var remaining = TimeSpan.FromSeconds(elapsed.TotalSeconds * (100 - percent) / percent);
            TimeText.Text = Loc.F("경과 {0} · 남은 시간 약 {1}", FormatDuration(elapsed), FormatDuration(remaining));
        }
        else
        {
            TimeText.Text = Loc.F("경과 {0}", FormatDuration(elapsed));
        }
    }

    private static string FormatDuration(TimeSpan time) =>
        time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_gate.IsPaused)
        {
            _gate.Resume();
            _elapsed.Start();
            PauseButton.Content = Loc.T("일시정지");
            StatusText.Text = Loc.T("계속하는 중...");
        }
        else
        {
            _gate.Pause();
            _elapsed.Stop();
            PauseButton.Content = Loc.T("계속");
            StatusText.Text = Loc.T("일시정지됨");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // 일시정지 중이어도 취소가 바로 먹도록 문을 엽니다.
        _gate.Resume();
        _cancellation.Cancel();
        StatusText.Text = Loc.T("취소하는 중...");
        // 두 번 누를 필요가 없음을 보여 줍니다.
        CancelButton.IsEnabled = false;
    }

    public static async Task<bool> RunAsync(
        Window owner,
        string title,
        Func<IProgress<ProgressReport>, CancellationToken, Task> work)
    {
        var dialog = new ProgressDialog(title) { Owner = owner };
        dialog.Show();

        bool holdsTurn = false;
        try
        {
            await dialog.WaitForTurnAsync();
            holdsTurn = true;

            PauseGate.Current = dialog._gate;
            await work(dialog.Progress, dialog._cancellation.Token);
            dialog.Close();
            return true;
        }
        catch (OperationCanceledException)
        {
            dialog.Close();
            MessageBox.Show(owner, Loc.T("작업이 취소되었습니다."), Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        catch (ArchiveException exception)
        {
            dialog.Close();
            MessageBox.Show(owner, exception.Message, Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            dialog.Close();
            MessageBox.Show(owner, Loc.T("접근 권한이 없습니다.\n") + exception.Message, Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch (IOException exception)
        {
            dialog.Close();
            MessageBox.Show(owner, Loc.T("파일을 처리하는 중 오류가 발생했습니다.\n") + exception.Message, Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch (Exception exception)
        {
            dialog.Close();
            MessageBox.Show(owner, Loc.T("예상하지 못한 오류가 발생했습니다.\n") + exception.GetType().Name + ": " + exception.Message,
                Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        finally
        {
            PauseGate.Current = null;
            if (holdsTurn) QueueLock.Release();
            if (dialog.IsVisible) dialog.Close();
        }
    }

    /// <summary>앞선 작업이 끝날 때까지 기다립니다. 기다리는 동안에도 취소할 수 있습니다.</summary>
    private async Task WaitForTurnAsync()
    {
        if (QueueLock.CurrentCount == 0)
        {
            _waiting = true;
            int ahead = Interlocked.Increment(ref _queued);
            StatusText.Text = Loc.F("대기 중 (앞에 {0}개 작업)", ahead);
            try
            {
                await QueueLock.WaitAsync(_cancellation.Token);
            }
            finally
            {
                Interlocked.Decrement(ref _queued);
                _waiting = false;
            }

            _elapsed.Restart();
            StatusText.Text = Loc.T("준비 중...");
        }
        else
        {
            await QueueLock.WaitAsync(_cancellation.Token);
        }

        PauseButton.IsEnabled = true;
    }
}
