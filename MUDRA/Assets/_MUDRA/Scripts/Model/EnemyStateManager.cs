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
    // セクション遷移で差し替わるためreadonlyにできない
    private EnemyData _enemyData;
    private int _patternIndex;

    // --- ループ制御 ---
    private CancellationTokenSource _loopCts;

    public EnemyStateManager(EnemyData enemyData)
    {
        _enemyData = enemyData;
    }

    /// <summary>
    /// 交戦相手を差し替えて行動ループを開始し直す。
    /// セクション遷移時にSectionProgressManagerから呼ぶ。
    ///
    /// インスタンスを作り直さないのは、OnAttackExecuted等の購読と
    /// StatusEffectFactoryが握っているApplyStun/EndStunのdelegateを
    /// 生かしたままにするため。保持している状態は
    /// _enemyData / _patternIndex / _loopCts の3つだけなので、
    /// ここでリセットすれば新規生成と同じ状態になる。
    /// </summary>
    /// <param name="firstActionDelay">1手目の予告に入るまでの待機（秒）。StartLoopを参照</param>
    public void SetEnemy(EnemyData enemyData, float firstActionDelay)
    {
        StopLoop();
        _enemyData = enemyData;
        StartLoop(firstActionDelay);    // _patternIndexはStartLoop内で0に戻る
    }

    /// <summary>
    /// 行動ループを開始する。バトル開始時に呼ぶ。
    /// </summary>
    /// <param name="firstActionDelay">
    /// 1手目のChargingに入るまでの待機（秒）。敵の登場演出の間に予告が始まると、
    /// 登場と予告が重なってGuardのタイミングが読めなくなるため（B4）。
    /// 長さは演出の尺で決まり、ボスとそれ以外で違うので呼び出し側（SectionProgressManager）が決める。
    /// Stun解除からの再開には入れない（行動を遅らせるとStunの効果が実質延びてしまう）。
    /// </param>
    public void StartLoop(float firstActionDelay)
    {
        StopLoop();

        if (_enemyData.actionPattern == null || _enemyData.actionPattern.Length == 0)
        {
            UnityEngine.Debug.LogError($"[EnemyStateManager] {_enemyData.enemyName} の actionPattern が未設定です");
            return;
        }
        
        _loopCts = new CancellationTokenSource();
        _patternIndex = 0;
        RunLoopAsync(_loopCts.Token, firstActionDelay).Forget();
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

    private async UniTaskVoid RunLoopAsync(CancellationToken ct, float initialDelay = 0f)
    {
        try
        {
            if (initialDelay > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(initialDelay),
                    cancellationToken: ct
                );
            }

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
                
                // パターンを進める
                _patternIndex = (_patternIndex + 1) % _enemyData.actionPattern.Length;

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
            }
        }
        catch (OperationCanceledException)
        {
            // バトル終了・シーン破棄によるキャンセルは正常終了として扱う
        }
    }
    
    /// <summary>
    /// 外部からStunを付与する。現在の行動ループを中断しStunned状態に遷移する。
    /// _patternIndexは保持するため、解除後は中断されたアクションから再開する。
    /// </summary>
    public void ApplyStun()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;

        _currentPhase.Value = EnemyPhase.Stunned;
        _currentAction.Value = null;
    }

    /// <summary>
    /// Stunを解除しIdle復帰→行動ループを再開する。
    /// StatusEffectManagerのStunEffect.OnExpireから呼ばれる。
    /// </summary>
    public void EndStun()
    {
        _currentPhase.Value = EnemyPhase.Idle;
        _loopCts = new CancellationTokenSource();
        RunLoopAsync(_loopCts.Token).Forget();
    }

    public void Dispose()
    {
        StopLoop();
        _currentPhase.Dispose();
        _currentAction.Dispose();
        _onAttackExecuted.Dispose();
    }
}