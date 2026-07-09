using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

/// <summary>
/// 敵の行動ループを管理する。
/// EnemyDataのactionPatternを順番に実行し、末尾で先頭に戻る。
/// </summary>
public class EnemyStateManager : IDisposable
{
    // --- R3通知 ---
    private readonly ReactiveProperty<EnemyPhase> _currentPhase = new(EnemyPhase.Idle);
    public ReadOnlyReactiveProperty<EnemyPhase> CurrentPhase => _currentPhase;

    private readonly ReactiveProperty<EnemyAction?> _currentAction = new(null);
    public ReadOnlyReactiveProperty<EnemyAction?> CurrentAction => _currentAction;

    // 攻撃着弾の瞬間通知（BattleModelがダメージ適用に使う）
    private readonly Subject<EnemyAction> _onAttackExecuted = new();
    public Observable<EnemyAction> OnAttackExecuted => _onAttackExecuted;

    // --- 行動データ ---
    private readonly EnemyData _enemyData;
    private int _patternIndex;

    // --- ループ制御 ---
    private CancellationTokenSource _loopCts;

    public EnemyStateManager(EnemyData enemyData)
    {
        _enemyData = enemyData;
    }

    /// <summary>
    /// 行動ループを開始する。バトル開始時に呼ぶ。
    /// </summary>
    public void StartLoop()
    {
        _loopCts = new CancellationTokenSource();
        _patternIndex = 0;
        RunLoopAsync(_loopCts.Token).Forget();
    }

    /// <summary>
    /// 行動ループを停止する。バトル終了時に呼ぶ。
    /// </summary>
    public void StopLoop()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;
        _currentPhase.Value = EnemyPhase.Idle;
        _currentAction.Value = null;
    }

    private async UniTaskVoid RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var action = _enemyData.actionPattern[_patternIndex];

            // --- Charging ---
            _currentPhase.Value = EnemyPhase.Charging;
            _currentAction.Value = action;
            await UniTask.Delay(
                TimeSpan.FromSeconds(action.attackData.chargeTime),
                cancellationToken: ct
            );

            // --- Attacking ---
            _currentPhase.Value = EnemyPhase.Attacking;
            _onAttackExecuted.OnNext(action);

            // 攻撃演出用の短い待機（仮値）
            await UniTask.Delay(
                TimeSpan.FromSeconds(0.3f),
                cancellationToken: ct
            );

            // --- Idle（次の行動までの待機）---
            _currentPhase.Value = EnemyPhase.Idle;
            _currentAction.Value = null;
            await UniTask.Delay(
                TimeSpan.FromSeconds(action.intervalAfter),
                cancellationToken: ct
            );

            // パターンを進める
            _patternIndex = (_patternIndex + 1) % _enemyData.actionPattern.Length;
        }
    }

    public void Dispose()
    {
        StopLoop();
        _currentPhase.Dispose();
        _currentAction.Dispose();
        _onAttackExecuted.Dispose();
    }
}