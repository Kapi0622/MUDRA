using MUDRA.Data;
using MUDRA.HandTracking;
using UnityEngine;
using R3;

/// <summary>
/// 全Modelの生成と各Presenterへの注入を行うエントリーポイント。
/// シーン上に1つ配置し、SerializeFieldで素材を集約する。
/// </summary>
public class BattleInitializer : MonoBehaviour
{
    [Header("Presenter参照")]
    [SerializeField] private SpellSequenceRunner _spellSequenceRunner;
    [SerializeField] private BattlePresenter _battlePresenter;

    [Header("Input")]
    [SerializeField] private MediaPipeHandLandmarkProvider _provider;

    [Header("Data")]
    [SerializeField] private SpellData[] _allSpells;
    [SerializeField] private EnemyData _enemyData;
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

    private readonly CompositeDisposable _disposables = new();

    private void Awake()
    {
        // --- Model生成 ---
        _handTrackingService = new HandTrackingService(_provider);
        _spellSequenceModel = new SpellSequenceModel(_allSpells, () => Time.time);
        _playerStateManager = new PlayerStateManager();
        _enemyStateManager = new EnemyStateManager(_enemyData);
        _battleModel = new BattleModel(_playerMaxHp, _enemyData);
        _guardWindowManager = new GuardWindowManager();

        // EnemyStateManagerとBattleModelの両方が揃ってから配線する
        _statusEffectManager = new StatusEffectManager();
        var statusEffectFactory = new StatusEffectFactory(
            _battleModel.ApplyDotDamage,
            _enemyStateManager.ApplyStun,
            _enemyStateManager.EndStun
        );
        _battleModel.SetStatusEffectDependencies(_statusEffectManager, statusEffectFactory);
        
        // --- Presenterへ注入 ---
        _spellSequenceRunner.Initialize(
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

        // --- バトル開始 ---
        _enemyStateManager.StartLoop();

        // --- バトル終了時の後片付け ---
        // ClearAllを先に呼ぶ（StunEffect.OnExpire→EndStunが走ってもStopLoopが後で確実に止める）
        _battleModel.OnBattleEnd
            .Subscribe(_ => _statusEffectManager.ClearAll())
            .AddTo(_disposables);
        
        _battleModel.OnBattleEnd
            .Subscribe(_ => _enemyStateManager.StopLoop())
            .AddTo(_disposables);
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
        _enemyStateManager.StopLoop();
        _handTrackingService.Dispose();
        _spellSequenceModel.Dispose();
        _playerStateManager.Dispose();
        _enemyStateManager.Dispose();
        _battleModel.Dispose();
    }
}