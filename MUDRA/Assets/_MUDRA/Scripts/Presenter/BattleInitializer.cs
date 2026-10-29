using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using MUDRA.Data;
using MUDRA.HandTracking;
using UnityEngine;
using R3;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// 全Modelの生成と各Presenterへの注入を行うエントリーポイント。
/// シーン上に1つ配置し、SerializeFieldで素材を集約する。
///
/// ここで生成するModelは全てステージ全体で生存する。
/// セクション遷移で作り直すインスタンスは無く、
/// SectionProgressManagerがBattleModelとEnemyStateManagerの中身だけを差し替える。
/// そのため一度張った購読は最後まで有効で、再配線は発生しない。
///
/// B5からはInGameを常駐シーン（App）に加算ロードする構成になり、自分からは組み立てを始めない。
/// AppのGameFlowPresenterがシーンを読み込んだ後に Initialize（組み立て）→ 遷移演出 → BeginBattle（開始）の順に呼ぶ。
/// ステージ・カメラ（Provider）・GameStateManagerはApp側にあるため、BattleContextで受け取る。
/// リトライはシーンごと読み直すので、Modelは必ず新しく作り直される。
/// </summary>
public class BattleInitializer : MonoBehaviour
{
    [Header("Presenter参照")]
    [SerializeField] private HandSignPresenter _handSignPresenter;
    [SerializeField] private BattlePresenter _battlePresenter;
    [SerializeField] private EnemyPresenter _enemyPresenter;
    [SerializeField] private PausePresenter _pausePresenter;
    [Tooltip("チュートリアルの指示パネル（B6）。チュートリアルの時だけ使う")]
    [SerializeField] private TutorialPresenter _tutorialPresenter;

    [Header("Data")]
    [SerializeField] private int _playerMaxHp = 100;
    [Tooltip("演出の時間表。Modelの待機時間とViewの尺を同じ表から決める")]
    [SerializeField] private PresentationTimingData _presentationTiming;

    [Header("View")]
    [SerializeField] private HpBarView _playerHpBarView;
    [SerializeField] private HpBarView _bossHpBarView;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("デバッグ")]
    [SerializeField, Tooltip("エディタでInGameだけを再生した時に始めるステージ。未設定ならAppの全ステージの先頭")]
    private StageData _debugStartStage;

    /// <summary>エディタでInGameだけを再生した時に始めるステージ</summary>
    public StageData DebugStartStage => _debugStartStage;
#endif

    // Model群（Dispose管理のため保持）
    private HandTrackingService _handTrackingService;
    private SpellSequenceModel _spellSequenceModel;
    private PlayerStateManager _playerStateManager;
    private EnemyStateManager _enemyStateManager;
    private BattleModel _battleModel;
    private StatusEffectManager _statusEffectManager;
    private GuardWindowManager _guardWindowManager;
    private SectionProgressManager _sectionProgressManager;
    private BattleEndModel _battleEndModel;
    private PauseModel _pauseModel;
    private BattleStatsModel _battleStatsModel;
    private TutorialModel _tutorialModel;    // チュートリアルの時だけ（B6）

    private BattleContext _context;
    private bool _isInitialized;
    private bool _hasBegun;

    private readonly CompositeDisposable _disposables = new();

    /// <summary>
    /// Modelを組み立て、各Presenterへ注入する。まだバトルは始めない（敵は出てこない）。
    /// AppのGameFlowPresenterから、シーンを読み込んだ直後に呼ばれる。
    /// </summary>
    /// <returns>ステージデータに不備があり組み立てられなかった場合はfalse</returns>
    public bool Initialize(BattleContext context)
    {
        if (_isInitialized) return true;

        _context = context;
        if (!ValidateStageData()) return false;

        BuildStageScopeModels();
        InjectPresenters();
        SubscribeStageEnd();

        _isInitialized = true;
        return true;
    }

    /// <summary>
    /// バトルを開始する。遷移演出が明けてから呼ばれる。
    /// ステージ開始の演出（ステージ名 →「結印！」）を流し終えてから、ステージを始める。
    /// 先頭セクションの敵の流し込みとEnemyStateManagerのループ開始は
    /// SectionProgressManagerが行うため、ここでStartLoopは呼ばない。
    /// </summary>
    public void BeginBattle()
    {
        if (!_isInitialized || _hasBegun) return;
        _hasBegun = true;

        BeginBattleAsync().Forget();
    }

    private async UniTaskVoid BeginBattleAsync()
    {
        try
        {
            await _enemyPresenter.PlayStageIntroAsync(FirstStage, destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 演出の途中でシーンごと破棄された（リタイア等）
            return;
        }

        _sectionProgressManager.StartStage(FirstStage);
    }

#if UNITY_EDITOR
    /// <summary>
    /// エディタでInGameだけを再生した時の入口。
    /// Appが読み込まれていなければ加算ロードする。読み込まれたAppのAppInitializerは
    /// InGameが既にあることに気付き、Titleを飛ばしてこのシーンでバトルを始める（今までの開発手順を崩さないため）。
    /// </summary>
    private void Start()
    {
        if (_isInitialized) return;
        if (SceneManager.GetSceneByName(SceneNames.App).isLoaded) return;

        Debug.Log("[BattleInitializer] InGame単体で再生されたため、Appを読み込みます");
        SceneManager.LoadSceneAsync(SceneNames.App, LoadSceneMode.Additive);
    }
#endif

    /// <summary>開始するステージ</summary>
    private StageData FirstStage => _context.Stage;

    private bool ValidateStageData()
    {
        if (FirstStage == null)
        {
            Debug.LogError("[BattleInitializer] 開始ステージが未設定です");
            return false;
        }

        if (FirstStage.sections == null || FirstStage.sections.Length == 0)
        {
            Debug.LogError($"[BattleInitializer] {FirstStage.stageName} のsectionsが空です");
            return false;
        }

        if (FirstStage.sections[0].enemyData == null)
        {
            Debug.LogError($"[BattleInitializer] {FirstStage.stageName} のセクション0のenemyDataが未設定です");
            return false;
        }

        return true;
    }

    /// <summary>
    /// ステージ全体で生存するModelを生成し、相互に配線する。
    /// </summary>
    private void BuildStageScopeModels()
    {
        // BattleModel・EnemyStateManagerは先頭セクションの敵で初期化する。
        // 以降の敵はSectionProgressManagerがSetEnemyで差し替える。
        var firstEnemy = FirstStage.sections[0].enemyData;

        _handTrackingService = new HandTrackingService(_context.Provider);
        _spellSequenceModel = new SpellSequenceModel(_context.AllSpells.ToArray(), () => Time.time);
        _playerStateManager = new PlayerStateManager();
        _enemyStateManager = new EnemyStateManager(firstEnemy);
        _battleModel = new BattleModel(_playerMaxHp, firstEnemy);
        _guardWindowManager = new GuardWindowManager();

        // EnemyStateManagerとBattleModelの両方が揃ってから配線する
        _statusEffectManager = new StatusEffectManager();
        var statusEffectFactory = new StatusEffectFactory(
            _battleModel.ApplyDotDamage,
            _enemyStateManager.ApplyStun,
            _enemyStateManager.EndStun,
            _battleModel.ApplyHeal
        );
        _battleModel.SetStatusEffectDependencies(_statusEffectManager, statusEffectFactory);

        _sectionProgressManager = new SectionProgressManager(
            _battleModel,
            _enemyStateManager,
            _statusEffectManager,
            _presentationTiming
        );

        _battleEndModel = new BattleEndModel(_sectionProgressManager, _presentationTiming);
        _pauseModel = new PauseModel(_presentationTiming.resumeCountdown);
        _context.IsAutoPauseEnabled
            .Subscribe(isEnabled => _pauseModel.IsAutoPauseEnabled = isEnabled)
            .AddTo(_disposables);
        _battleStatsModel = new BattleStatsModel(_battleModel, _spellSequenceModel, _sectionProgressManager);

        if (_context.IsTutorial) BuildTutorialModel();
    }

    /// <summary>
    /// チュートリアルの進行を組み立てる（B6）。
    /// 敵の行動はTutorialModelが決める（ガードの段階に入るまでは保留）。
    /// 購読した時点で保留されるので、StartStageでセクションが始まっても敵は動き出さない
    /// </summary>
    private void BuildTutorialModel()
    {
        _tutorialModel = new TutorialModel(_spellSequenceModel, _battleModel, _presentationTiming.tutorialStepClearHold);
        _tutorialModel.IsEnemyActive
            .Subscribe(isActive => _enemyStateManager.SetHeld(!isActive, _presentationTiming.tutorialEnemyStartDelay))
            .AddTo(_disposables);
    }

    private void InjectPresenters()
    {
        _handSignPresenter.Initialize(
            _handTrackingService,
            _spellSequenceModel,
            _playerStateManager,
            _guardWindowManager,
            _pauseModel,
            _battleModel.LastAttackElement
        );

        _pausePresenter.Initialize(
            _pauseModel,
            _context.GameStateManager,
            _context.HandPresence,
            !_handSignPresenter.IsKeyboardMode,
            _context.AllSpells,
            _context.Settings
        );

        _battlePresenter.Initialize(
            _battleModel,
            _enemyStateManager,
            _spellSequenceModel,
            _statusEffectManager,
            _guardWindowManager,
            _battleStatsModel,
            _playerHpBarView,
            _bossHpBarView
        );

        _enemyPresenter.Initialize(
            _sectionProgressManager,
            _enemyStateManager,
            _battleModel,
            _presentationTiming,
            _context.Bgm
        );
        _enemyPresenter.PrepareStage(FirstStage);

        if (_tutorialModel != null)
        {
            _tutorialPresenter.Initialize(
                _tutorialModel,
                _context.GameStateManager,
                _pauseModel,
                _context.AllSpells,
                FirstStage.sections[0].enemyData);
        }

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        var gameStateManager = _context.GameStateManager;

        var debugMenu = FindFirstObjectByType<DebugMenuView>();
        if (debugMenu != null)
        {
            debugMenu.Inject(
                _battleModel,
                _statusEffectManager,
                _enemyStateManager,
                _sectionProgressManager,
                _context.AllStages.ToArray(),
                () => gameStateManager.Retry());
        }

        // Rキーのやり直しは、シーンを直接読み直さずGameStateManager経由で行う（Appまで読み直さないため）
        var debugKeyboard = FindFirstObjectByType<DebugKeyboardInput>();
        if (debugKeyboard != null)
        {
            debugKeyboard.OnRetryRequested
                .Subscribe(_ => gameStateManager.Retry())
                .AddTo(_disposables);
        }
    #endif
    }

    /// <summary>
    /// ステージの決着（クリア・敗北）時の後片付けと、Result画面への報告を配線する。
    /// セクション撃破時の後片付けはSectionProgressManager側の担当で、ここには来ない。
    /// </summary>
    private void SubscribeStageEnd()
    {
        // ClearAllを先に呼ぶ（StunEffect.OnExpire→EndStunが走ってもStopLoopが後で確実に止める）
        Observable.Merge(
                _sectionProgressManager.OnStageCleared,
                _sectionProgressManager.OnGameOver
            )
            .Subscribe(_ =>
            {
                _statusEffectManager.ClearAll();
                _enemyStateManager.StopLoop();
            })
            .AddTo(_disposables);

        // ポーズできるのはバトルの進行中だけ。デバッグのジャンプで決着後に再開した時のため、セクション開始でも立て直す
        _sectionProgressManager.OnSectionStarted
            .Subscribe(_ => _pauseModel.SetBattleActive(true))
            .AddTo(_disposables);

        Observable.Merge(
                _sectionProgressManager.OnStageCleared,
                _sectionProgressManager.OnGameOver
            )
            .Subscribe(_ => _pauseModel.SetBattleActive(false))
            .AddTo(_disposables);

        // 決着の演出を見せ終えたらResult画面へ。挑戦したステージはデバッグのジャンプ後も現在のステージで記録する
        // チュートリアルはResult画面・戦績に出さない（B6）。木人を倒したら完了扱い、負けたらやり直す
        _battleEndModel.OnFinished
            .Subscribe(isWin =>
            {
                var gameStateManager = _context.GameStateManager;
                if (!_context.IsTutorial)
                    gameStateManager.EndBattle(CreateRecord(isWin));
                else if (isWin)
                    gameStateManager.CompleteTutorial();
                else
                    gameStateManager.Retry();
            })
            .AddTo(_disposables);

        _tutorialModel?.OnCompleted
            .Subscribe(_ => _context.GameStateManager.CompleteTutorial())
            .AddTo(_disposables);

        _sectionProgressManager.OnStageCleared
            .Subscribe(_ => Debug.Log("[Stage] ステージクリア"))
            .AddTo(_disposables);

        _sectionProgressManager.OnGameOver
            .Subscribe(_ => Debug.Log("[Stage] ゲームオーバー"))
            .AddTo(_disposables);

        _sectionProgressManager.OnSectionCleared
            .Subscribe(index => Debug.Log($"[Stage] セクション{index} 撃破"))
            .AddTo(_disposables);
    }

    private BattleRecord CreateRecord(bool isWin)
    {
        var stats = _battleStatsModel.CreateSnapshot();
        BattleRank? rank = isWin ? RankCalculator.Calculate(stats, _playerMaxHp) : null;
        return new BattleRecord(
            _sectionProgressManager.CurrentStage,
            isWin,
            stats,
            rank,
            _sectionProgressManager.CurrentSection.enemyData);
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
        _battleEndModel?.Dispose();
        _pauseModel?.Dispose();
        _battleStatsModel?.Dispose();
        _tutorialModel?.Dispose();
        _sectionProgressManager?.Dispose();
        _enemyStateManager?.StopLoop();
        _handTrackingService?.Dispose();
        _spellSequenceModel?.Dispose();
        _playerStateManager?.Dispose();
        _enemyStateManager?.Dispose();
        _battleModel?.Dispose();
        _statusEffectManager?.Dispose();
        _guardWindowManager?.Dispose();
    }
}
