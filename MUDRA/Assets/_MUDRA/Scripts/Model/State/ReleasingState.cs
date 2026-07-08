using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 発動中 発動印確定後、固定の硬直時間が経過すると自動でIdleへ戻る
/// </summary>
public class ReleasingState : IPlayerState
{
    // 発動後の硬直時間（ミリ秒） 後々SpellDataやCalibrationDataへ移す想定の仮値
    private const int RecoveryDurationMs = 250;

    private readonly PlayerStateManager _manager;
    private CancellationTokenSource _cts;

    public PlayerPhase Phase => PlayerPhase.Releasing;

    public ReleasingState(PlayerStateManager manager)
    {
        _manager = manager;
    }

    public void Enter()
    {
        _cts = new CancellationTokenSource();
        WaitAndReturnToIdleAsync(_cts.Token).Forget();
    }

    public void Exit()
    {
        // 硬直時間が経過する前に別状態へ遷移した場合に備えてタイマーを止める
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async UniTaskVoid WaitAndReturnToIdleAsync(CancellationToken token)
    {
        await UniTask.Delay(RecoveryDurationMs, cancellationToken: token);
        _manager.TransitionToIdle();
    }
}