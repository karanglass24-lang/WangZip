namespace WangZip.Core;

/// <summary>
/// 진행 중인 작업을 일시정지/재개하는 문. 작업 스레드는 진행률을 보고할 때마다 문을 확인하고,
/// 일시정지 상태면 재개될 때까지 멈춥니다. 현재 비동기 흐름(<see cref="Current"/>)에 걸어 두면
/// 압축/해제 코드를 고치지 않고도 모든 작업에 적용됩니다.
/// </summary>
public sealed class PauseGate
{
    private static readonly AsyncLocal<PauseGate?> CurrentGate = new();
    private readonly ManualResetEventSlim _open = new(initialState: true);

    /// <summary>현재 비동기 흐름에 연결된 문(없으면 null). Task.Run으로 시작한 작업에도 전달됩니다.</summary>
    public static PauseGate? Current
    {
        get => CurrentGate.Value;
        set => CurrentGate.Value = value;
    }

    public bool IsPaused => !_open.IsSet;

    public void Pause() => _open.Reset();

    public void Resume() => _open.Set();

    /// <summary>일시정지 중이면 재개될 때까지 기다립니다.</summary>
    public void WaitIfPaused(CancellationToken cancellationToken = default)
    {
        if (_open.IsSet) return;
        _open.Wait(cancellationToken);
    }

    internal static void CheckCurrent()
    {
        PauseGate? gate = CurrentGate.Value;
        if (gate is not null && gate.IsPaused) gate.WaitIfPaused();
    }
}
