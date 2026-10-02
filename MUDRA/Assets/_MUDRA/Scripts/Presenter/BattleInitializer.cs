using MUDRA.Data;
using MUDRA.HandTracking;
using UnityEngine;
using R3;

/// <summary>
/// 全Modelの生成と各Presenterへの注入を行うエントリーポイント。
/// シーン上に1つ配置し、SerializeFieldで素材を集約する。
///
/// ここで生成するModelは全てステージ全体で生存する。
/// セクション遷移で作り直すインスタンスは無く、
/// SectionProgressManagerがBattleModelとEnemyStateManagerの中身だけを差し替える。
/// そのため一度張った購読は最後まで有効で、再配線は発生しない。
/// </summary>
public class BattleInitializer : MonoBehaviour
{
    [Header("Presenter参照")]
    [SerializeField] private HandSignPresenter _handSignPresenter;
    [SerializeField] private BattlePresenter _battlePresenter;

    [Header("Input")]
    [SerializeField] private MediaPipeHandLandmarkProvider _provider;

    [Header("Data")]
    [SerializeField] private SpellData[] _allSpells;
    [Tooltip("全ステージ。起動時は要素0から開始する。デバッグメニューのステージジャンプ候補にもなる")]
    [SerializeField] private StageData[] _allStages;
    [SerializeField] private int _playerMaxHp = 100;

    [Header("View")]
    [SerializeField] private HpBarView _playerHpBarView;
    [SerializeField] private HpBarView _bossHpBarView;

    // Model群（Dispose管理のため保持）
    private HandTrackingService _handTrackingService;
    private SpellSequenceModel _spellSequenceModel;
    private PlayerStateManager _playerStateManager;
    private EnemyStateManager _enemyStateManager;
    private BattleModel _battleModel;
    private StatusEffectManager _statusEffectManager;
    private GuardWindowManager _guardWindowManager;
    private SectionProgressManager _sectionProgressManager;

    private readonly CompositeDisposable _disposables = new();

    private void Awake()
    {
        if (!ValidateStageData()) return;

        BuildStageScopeModels();
        InjectPresenters();
        SubscribeStageEnd();

        // バトル開始の唯一の入口。
        // 先頭セクションの敵の流し込みとEnemyStateManagerのループ開始は
        // SectionProgressManagerが行うため、ここでStartLoopは呼ばない。
        _sectionProgressManager.StartStage(FirstStage);
    }

    /// <summary>起動時に開始するステージ</summary>
    private StageData FirstStage => _allStages[0];

    private bool ValidateStageData()
    {
        if (_allStages == null || _allStages.Length == 0)
        {
            Debug.LogError("[BattleInitializer] StageDataが1つも設定されていません");
            return false;
        }

        if (FirstStage == null)
        {
            Debug.LogError("[BattleInitializer] 開始ステージ（_allStagesの要素0）が未設定です");
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

        _handTrackingService = new HandTrackingService(_provider);
        _spellSequenceModel = new SpellSequenceModel(_allSpells, () => Time.time);
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
            _statusEffectManager
        );
    }

    private void InjectPresenters()
    {
        _handSignPresenter.Initialize(
            _handTrackingService,
            _spellSequenceModel,
            _playerStateManager,
            _guardWindowManager
        );

        _battlePresenter.Initialize(
            _battleModel,
            _enemyStateManager,
            _spellSequenceModel,
            _statusEffectManager,
            _guardWindowManager,
            _playerHpBarView,
            _bossHpBarView
        );

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        var debugMenu = FindFirstObjectByType<DebugMenuView>();
        if (debugMenu != null)
        {
            debugMenu.Inject(
                _battleModel,
                _statusEffectManager,
                _enemyStateManager,
                _sectionProgressManager,
                _allStages);
        }
    #endif
    }

    /// <summary>
    /// ステージの決着（クリア・敗北）時の後片付けを配線する。
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

    private void OnDestroy()
    {
        _disposables.Dispose();
        _sectionProgressManager?.Dispose();
        _enemyStateManager?.StopLoop();
        _handTrackingService?.Dispose();
        _spellSequenceModel?.Dispose();
        _playerStateManager?.Dispose();
        _enemyStateManager?.Dispose();
        _battleModel?.Dispose();
    }
}
