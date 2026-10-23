using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

/// <summary>
/// チュートリアル（練習バトル）の進行（B6）。既存のModel（詠唱・バトル）のイベントを購読して、段階を順に進める。
///
/// バトルの仕組みには手を入れず、「今の段階で教えたい操作が起きたか」を外から見ているだけにしている。
/// そのためチュートリアル用の特別な判定は無く、本番のバトルと同じ操作がそのまま練習になる。
///
/// 段階を達成したら stepClearHold 秒だけ「達成」を見せてから次の段階へ進む。
/// 待機をModelが持つのは、演出の完了をModelが待てないため（SectionProgressManagerと同じ方針）。
/// ゲーム内時間で待つので、ポーズ中は進まない。
/// </summary>
public class TutorialModel : IDisposable
{
    private readonly ReactiveProperty<TutorialStep> _currentStep = new(TutorialStep.Chant);
    /// <summary>今の段階。達成してから次の段階に切り替わるまでは、達成した段階のまま</summary>
    public ReadOnlyReactiveProperty<TutorialStep> CurrentStep => _currentStep;

    private readonly ReactiveProperty<bool> _isStepCleared = new(false);
    /// <summary>今の段階を達成し、次の段階へ切り替わるのを待っている間true</summary>
    public ReadOnlyReactiveProperty<bool> IsStepCleared => _isStepCleared;

    private readonly ReactiveProperty<bool> _isEnemyActive = new(false);
    /// <summary>
    /// 敵に行動させるか。ガードの段階に入るまではfalseで、敵は攻撃してこない。
    /// 最初は敵の攻撃を気にせず、落ち着いて印を試せるようにするため
    /// </summary>
    public ReadOnlyReactiveProperty<bool> IsEnemyActive => _isEnemyActive;

    private readonly Subject<Unit> _onMisfired = new();
    /// <summary>発動の段階で暴発した。並びが違うと暴発することを補足するために使う</summary>
    public Observable<Unit> OnMisfired => _onMisfired;

    private readonly Subject<Unit> _onCompleted = new();
    /// <summary>全段階を達成し、最後の「達成」を見せ終えた</summary>
    public Observable<Unit> OnCompleted => _onCompleted;

    /// <summary>段階の総数（Completedを除く）。進み具合の表示に使う</summary>
    public const int StepCount = (int)TutorialStep.Completed;

    private readonly float _stepClearHold;
    private readonly CompositeDisposable _disposables = new();
    private CancellationTokenSource _advanceCts;
    private bool _isFinished;

    public TutorialModel(SpellSequenceModel spellSequence, BattleModel battle, float stepClearHold)
    {
        _stepClearHold = stepClearHold;

        spellSequence.OnSignAdded
            .Subscribe(_ => TryClear(TutorialStep.Chant))
            .AddTo(_disposables);

        spellSequence.OnSpellCast
            .Subscribe(result =>
            {
                if (result.IsSuccess)
                    TryClear(TutorialStep.Release);
                else if (IsWaitingFor(TutorialStep.Release))
                    _onMisfired.OnNext(Unit.Default);
            })
            .AddTo(_disposables);

        spellSequence.OnSequenceReset
            .Where(reason => reason == SequenceResetReason.Cancel)
            .Subscribe(_ => TryClear(TutorialStep.Cancel))
            .AddTo(_disposables);

        battle.OnPlayerDamaged
            .Where(info => info.WasGuarded && !info.IsMisfire)
            .Subscribe(_ => TryClear(TutorialStep.Guard))
            .AddTo(_disposables);

        battle.OnSpellHit
            .Where(hit => hit.IsWeakness)
            .Subscribe(_ => TryClear(TutorialStep.Element))
            .AddTo(_disposables);

        // 決着が付いたら（木人を倒した・負けた）、その後の進行は止める。行き先はBattleInitializerが決める
        battle.OnBattleEnd
            .Subscribe(_ => Finish())
            .AddTo(_disposables);
    }

    private bool IsWaitingFor(TutorialStep step)
    {
        return !_isFinished && !_isStepCleared.Value && _currentStep.Value == step;
    }

    private void TryClear(TutorialStep step)
    {
        if (!IsWaitingFor(step)) return;

        _isStepCleared.Value = true;
        CancelAdvance();    // 前の段階の待機は終わっているので、破棄するだけ
        _advanceCts = new CancellationTokenSource();
        AdvanceAsync(_advanceCts.Token).Forget();
    }

    private async UniTaskVoid AdvanceAsync(CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_stepClearHold), cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var next = _currentStep.Value + 1;
        _isStepCleared.Value = false;
        _currentStep.Value = next;

        // ガードの段階からは敵が動く。全段階を終えたら、遷移で覆われるまでの間は攻撃させない
        _isEnemyActive.Value = next is TutorialStep.Guard or TutorialStep.Element;

        if (next != TutorialStep.Completed) return;

        // 「修練完了」も同じ尺だけ見せてから抜ける
        _isFinished = true;
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_stepClearHold), cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        _onCompleted.OnNext(Unit.Default);
    }

    private void Finish()
    {
        _isFinished = true;
        CancelAdvance();
        _isEnemyActive.Value = false;
    }

    private void CancelAdvance()
    {
        _advanceCts?.Cancel();
        _advanceCts?.Dispose();
        _advanceCts = null;
    }

    public void Dispose()
    {
        CancelAdvance();
        _disposables.Dispose();
        _currentStep.Dispose();
        _isStepCleared.Dispose();
        _isEnemyActive.Dispose();
        _onMisfired.Dispose();
        _onCompleted.Dispose();
    }
}
