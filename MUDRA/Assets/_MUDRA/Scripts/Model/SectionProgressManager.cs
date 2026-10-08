using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MUDRA.Data;
using R3;

/// <summary>
/// ステージ内のセクション進行を管理する。
///
/// BattleModelは「現在交戦中の敵1体との決着」までしか知らない。
/// このクラスがその決着（OnBattleEnd）を購読し、
/// 次のセクションへ進むのか・ステージクリアなのか・敗北なのかを判断する。
/// セクションという概念を持つのはこのクラスだけで、
/// BattleModelやEnemyStateManagerはあくまで「今の敵」しか見ていない。
///
/// セクション遷移ではModelを作り直さず、BattleModelとEnemyStateManagerの
/// 中身（敵データ）を差し替える。そのため他クラスの購読を張り直す必要がない。
/// </summary>
public class SectionProgressManager : IDisposable
{
    // --- R3通知 ---
    private readonly ReactiveProperty<int> _currentSectionIndex = new(0);
    /// <summary>現在のセクション番号（0始まり）</summary>
    public ReadOnlyReactiveProperty<int> CurrentSectionIndex => _currentSectionIndex;

    private readonly ReactiveProperty<SectionPhase> _currentPhase = new(SectionPhase.InBattle);
    public ReadOnlyReactiveProperty<SectionPhase> CurrentPhase => _currentPhase;

    private readonly Subject<StageData> _onStageStarted = new();
    /// <summary>ステージ開始時に発火。ステージ名表示のトリガ（B4）。直後に先頭セクションのOnSectionStartedが続く</summary>
    public Observable<StageData> OnStageStarted => _onStageStarted;

    private readonly Subject<StageSection> _onSectionStarted = new();
    /// <summary>セクション開始時に発火。敵の出現演出・背景切替のトリガ（B4で使用）</summary>
    public Observable<StageSection> OnSectionStarted => _onSectionStarted;

    private readonly Subject<int> _onSectionCleared = new();
    /// <summary>セクション撃破時に発火。引数は撃破したセクションのindex。遷移演出の開始トリガ</summary>
    public Observable<int> OnSectionCleared => _onSectionCleared;

    private readonly Subject<Unit> _onStageCleared = new();
    /// <summary>最終セクションを撃破した時に発火</summary>
    public Observable<Unit> OnStageCleared => _onStageCleared;

    private readonly Subject<Unit> _onGameOver = new();
    /// <summary>プレイヤーが敗北した時に発火</summary>
    public Observable<Unit> OnGameOver => _onGameOver;

    // --- 依存 ---
    private readonly BattleModel _battleModel;
    private readonly EnemyStateManager _enemyStateManager;
    private readonly StatusEffectManager _statusEffectManager;

    /// <summary>
    /// 演出の時間表。Modelは演出の完了を待てない（Viewを知らない）ため、
    /// 撃破→次の敵の出現、出現→1手目の予告の間は、ここから計算した「演出の尺＋余白」だけ待つ（B4）。
    /// 待機時間は尺から計算されるので、Viewの尺を変えても食い違わない。
    /// </summary>
    private readonly PresentationTimingData _timing;

    // --- 進行状態 ---
    private StageData _stageData;

    /// <summary>現在のステージのセクション総数。StartStage前は0</summary>
    public int SectionCount => _stageData != null ? _stageData.sections.Length : 0;

    /// <summary>現在進行中のステージ。StartStage前はnull</summary>
    public StageData CurrentStage => _stageData;

    /// <summary>現在のセクション定義</summary>
    public StageSection CurrentSection => _stageData.sections[_currentSectionIndex.CurrentValue];

    private readonly CompositeDisposable _disposables = new();
    private CancellationTokenSource _transitionCts;

    public SectionProgressManager(
        BattleModel battleModel,
        EnemyStateManager enemyStateManager,
        StatusEffectManager statusEffectManager,
        PresentationTimingData timing)
    {
        _battleModel = battleModel;
        _enemyStateManager = enemyStateManager;
        _statusEffectManager = statusEffectManager;
        _timing = timing;

        // StartStageではなくここで購読する。
        // StartStageはステージ切替で複数回呼ばれうるため、
        // あちらに置くと呼ぶたびに購読が積み上がり、1回の決着でHandleBattleEndが
        // 複数回走ってセクションが飛ばされてしまう。
        // StartStage前は_stageDataがnullで、HandleBattleEnd側で弾く。
        _battleModel.OnBattleEnd
            .Subscribe(HandleBattleEnd)
            .AddTo(_disposables);
    }

    /// <summary>
    /// ステージを開始する。既に別のステージが進行中でも安全に切り替えられる。
    /// </summary>
    /// <param name="startIndex">開始セクション。通常は0。デバッグのステージジャンプで途中を指定する</param>
    public void StartStage(StageData stageData, int startIndex = 0)
    {
        if (stageData == null)
        {
            UnityEngine.Debug.LogError("[SectionProgressManager] StageDataが未設定です");
            return;
        }

        if (stageData.sections == null || stageData.sections.Length == 0)
        {
            UnityEngine.Debug.LogError(
                $"[SectionProgressManager] {stageData.stageName} のsectionsが空です");
            return;
        }

        if (startIndex < 0 || startIndex >= stageData.sections.Length)
        {
            UnityEngine.Debug.LogError(
                $"[SectionProgressManager] 開始セクション{startIndex} が範囲外です" +
                $"（{stageData.stageName} のセクション数: {stageData.sections.Length}）");
            return;
        }

        // 前のステージの遷移待機が生きたままだと、待機明けに前ステージのセクションへ
        // 勝手に進んでしまう
        CancelTransition();

        _stageData = stageData;

        _onStageStarted.OnNext(stageData);
        EnterSection(startIndex);
    }

    /// <summary>
    /// 進行中の遷移待機を打ち切る。ステージ切替・ジャンプ・破棄で共有する。
    /// </summary>
    private void CancelTransition()
    {
        _transitionCts?.Cancel();
        _transitionCts?.Dispose();
        _transitionCts = null;
    }

    /// <summary>
    /// 指定セクションの敵を各Modelに流し込み、戦闘を開始する。
    /// </summary>
    private void EnterSection(int index)
    {
        var section = _stageData.sections[index];

        // 敵データ不備で抜ける場合も先にindexを進めておく。
        // ここで戻ると_stageDataだけ新しいステージに差し替わったまま
        // indexが前のステージの値で取り残され、CurrentSectionが範囲外になる。
        _currentSectionIndex.Value = index;

        if (section.enemyData == null)
        {
            UnityEngine.Debug.LogError(
                $"[SectionProgressManager] セクション{index} のenemyDataが未設定です");
            return;
        }

        // 前の敵に付いていた効果を落としてから差し替える
        _statusEffectManager.ClearEnemyEffects();

        _battleModel.SetEnemy(section.enemyData);
        _enemyStateManager.SetEnemy(
            section.enemyData,
            section.isBoss ? _timing.BossEntryDelay : _timing.NormalEntryDelay);

        _currentPhase.Value = SectionPhase.InBattle;

        _onSectionStarted.OnNext(section);
    }

    private void HandleBattleEnd(bool isWin)
    {
        // StartStage前の決着通知は進行に関係しないので無視する
        if (_stageData == null) return;

        if (!isWin)
        {
            _currentPhase.Value = SectionPhase.GameOver;
            _onGameOver.OnNext(Unit.Default);
            return;
        }

        AdvanceAsync().Forget();
    }

    /// <summary>
    /// 次セクションへ進む。最終セクションだった場合はステージクリア。
    ///
    /// 非同期にしているのは演出の尺のためだけではない。
    /// OnBattleEndはCheckBattleEndから同期的に発火し、その呼び出し元のひとつである
    /// ApplyDotDamageはStatusEffectManager.Tickがリストを走査している最中に呼ばれる。
    /// その通知スタック上でClearEnemyEffectsや敵の差し替えを同期実行すると
    /// 走査中のリストを壊してしまうため、awaitを1回挟んでスタックから抜けてから状態を変える。
    /// </summary>
    private async UniTaskVoid AdvanceAsync()
    {
        _currentPhase.Value = SectionPhase.Transitioning;

        int clearedIndex = _currentSectionIndex.CurrentValue;
        _onSectionCleared.OnNext(clearedIndex);

        // 最終セクションならここで終わり
        if (clearedIndex >= _stageData.sections.Length - 1)
        {
            _currentPhase.Value = SectionPhase.StageCleared;
            _onStageCleared.OnNext(Unit.Default);
            return;
        }

        CancelTransition();
        _transitionCts = new CancellationTokenSource();

        try
        {
            // 通知スタックから抜けてから、倒した敵を止める。
            // 止めないと遷移待機の間も倒した敵の行動ループが回り続け、
            // （ダメージはBattleModel側で弾かれるものの）予告・攻撃の演出が死んだ敵から出てしまう（B4で顕在化）。
            // 効果のクリアを先にする: Stunの解除（EndStun）がループを再開させても、後のStopLoopで確実に止まる
            await UniTask.Yield(_transitionCts.Token);
            _statusEffectManager.ClearEnemyEffects();
            _enemyStateManager.StopLoop();

            await UniTask.Delay(
                TimeSpan.FromSeconds(_timing.TransitionDuration),
                cancellationToken: _transitionCts.Token
            );
        }
        catch (OperationCanceledException)
        {
            // シーン破棄によるキャンセルは正常終了として扱う
            return;
        }

        EnterSection(clearedIndex + 1);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // デバッグ専用

    /// <summary>
    /// 現在のステージ内で指定セクションへ即座に飛ぶ。
    ///
    /// EnterSectionがBattleModel.SetEnemyで_isBattleActiveをtrueに戻すため、
    /// StageCleared・GameOverに到達した後でもここから戦闘を再開できる。
    ///
    /// 通常のセクション遷移と違いClearAllを使う。
    /// 遷移ではHoTを意図的に残しているが、ジャンプは「そのセクションを素の状態で見る」のが
    /// 目的なので、プレイヤーに付いている効果も落とす。
    /// </summary>
    public void DebugJumpToSection(int index)
    {
        if (_stageData == null)
        {
            UnityEngine.Debug.LogError("[SectionProgressManager] ステージが開始されていません");
            return;
        }

        if (index < 0 || index >= _stageData.sections.Length)
        {
            UnityEngine.Debug.LogError(
                $"[SectionProgressManager] セクション{index} が範囲外です" +
                $"（セクション数: {_stageData.sections.Length}）");
            return;
        }

        CancelTransition();
        _statusEffectManager.ClearAll();

        EnterSection(index);
    }
#endif

    public void Dispose()
    {
        CancelTransition();

        _disposables.Dispose();
        _currentSectionIndex.Dispose();
        _currentPhase.Dispose();
        _onStageStarted.Dispose();
        _onSectionStarted.Dispose();
        _onSectionCleared.Dispose();
        _onStageCleared.Dispose();
        _onGameOver.Dispose();
    }
}
