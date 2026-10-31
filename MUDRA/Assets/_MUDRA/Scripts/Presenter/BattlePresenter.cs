using System;
using UnityEngine;
using R3;
using MUDRA.Data;

/// <summary>
/// BattleModel・EnemyStateManagerの通知を購読し、
/// バトル状態の変化をViewに反映する配線役。
/// </summary>
public class BattlePresenter : MonoBehaviour
{
    private BattleModel _battleModel;
    private EnemyStateManager _enemyStateManager;
    private SpellSequenceModel _spellSequenceModel;
    private StatusEffectManager _statusEffectManager;
    private GuardWindowManager _guardWindowManager;
    private BattleStatsModel _battleStatsModel;
    
    private HpBarView _playerHpBarView;
    private HpBarView _bossHpBarView;

    // B4で追加したプレイヤー側の演出View。
    // Initializeの引数を増やし続けないよう、HandSignPresenter・EnemyPresenterと同じくInspectorで受け取る
    [Header("プレイヤー側の演出（B4）")]
    [SerializeField] private ScreenFlashView _screenFlashView;
    [SerializeField] private CameraShakeView _cameraShakeView;
    [SerializeField] private GuardView _guardView;
    [SerializeField] private SpellEffectView _spellEffectView;

    [Header("バトルUI（B4）")]
    [SerializeField] private DamageNumberView _damageNumberView;
    [SerializeField] private ComboView _comboView;
    [SerializeField] private StatusIconView _statusIconView;

    [Header("サウンド（B7）")]
    [SerializeField] private SePlayer _se;
    [SerializeField] private SoundData _sound;

    private readonly CompositeDisposable _disposables = new();

    public void Initialize(
        BattleModel battleModel,
        EnemyStateManager enemyStateManager,
        SpellSequenceModel spellSequenceModel,
        StatusEffectManager statusEffectManager,
        GuardWindowManager guardWindowManager,
        BattleStatsModel battleStatsModel,
        HpBarView playerHpBarView,
        HpBarView bossHpBarView)
    {
        _battleModel = battleModel;
        _enemyStateManager = enemyStateManager;
        _spellSequenceModel = spellSequenceModel;
        _statusEffectManager = statusEffectManager;
        _guardWindowManager = guardWindowManager;
        _battleStatsModel = battleStatsModel;
        _playerHpBarView = playerHpBarView;
        _bossHpBarView = bossHpBarView;

        // --- HP初期表示(初期化アニメーションは今のところなし) ---
        _playerHpBarView.InitializeHp(_battleModel.PlayerHp.CurrentValue, _battleModel.PlayerMaxHp);
        _bossHpBarView.InitializeHp(_battleModel.BossHp.CurrentValue, _battleModel.BossMaxHp);
        
        // --- 敵差し替え（セクション遷移）→ ボスHPバーを新しい敵で初期化し直す ---
        // BossHpの値変化だけではBossMaxHpが変わったことを拾えないため別途購読する
        _battleModel.OnEnemyChanged
            .Subscribe(enemyData =>
            {
                _bossHpBarView.InitializeHp(_battleModel.BossHp.CurrentValue, _battleModel.BossMaxHp);
                _bossHpBarView.SetName(enemyData.enemyName);
                Debug.Log($"[Battle] 敵出現: {enemyData.enemyName} (HP:{_battleModel.BossMaxHp})");
            })
            .AddTo(_disposables);

        // --- HP監視 ---
        _battleModel.PlayerHp
            .Skip(1)
            .Subscribe(hp => _playerHpBarView.SetHp(hp, _battleModel.PlayerMaxHp))
            .AddTo(_disposables);

        _battleModel.BossHp
            .Skip(1)
            .Subscribe(hp => _bossHpBarView.SetHp(hp, _battleModel.BossMaxHp))
            .AddTo(_disposables);

        _battleModel.ComboCount
            .Subscribe(count => _comboView.SetCombo(count))
            .AddTo(_disposables);

        // --- ダメージ数字・回復量 ---
        // 威力0の術（回復術）も OnSpellHit を流すため、0ダメージは数字を出さない
        _battleModel.OnSpellHit
            .Where(result => result.TotalDamage > 0)
            .Subscribe(result => _damageNumberView.ShowSpellHit(
                result.PerHitDamage, result.HitCount, result.IsWeakness, result.HasSpeedBonus, result.IsRepeated,
                result.StunResisted))
            .AddTo(_disposables);

        // 命中の音は術の音（castSE）に含めているので、重ねるのは弱点の時のアクセントだけ（B7）
        _battleModel.OnSpellHit
            .Where(result => result.TotalDamage > 0 && result.IsWeakness)
            .Subscribe(_ => _se.Play(_sound.hitWeakness))
            .AddTo(_disposables);

        _battleModel.OnDotTick
            .Subscribe(damage =>
            {
                _damageNumberView.ShowDotTick(damage);
                _se.Play(_sound.dotTick);
            })
            .AddTo(_disposables);

        _battleModel.OnHealed
            .Subscribe(amount =>
            {
                _damageNumberView.ShowHeal(amount);
                _se.Play(_sound.healTick);
            })
            .AddTo(_disposables);

        // --- 継続中の効果のアイコン ---
        _statusEffectManager.OnEffectApplied
            .Subscribe(type => _statusIconView.SetEffectActive(type, true))
            .AddTo(_disposables);

        _statusEffectManager.OnEffectRemoved
            .Subscribe(type => _statusIconView.SetEffectActive(type, false))
            .AddTo(_disposables);

        // --- ボス攻撃 → ダメージ適用 ---
        _enemyStateManager.OnAttackExecuted
            .Subscribe(HandleEnemyAttack)
            .AddTo(_disposables);

        // --- 被弾 → 暴発 / ガード成功 / 通常の被弾 で演出を出し分ける ---
        _battleModel.OnPlayerDamaged
            .Subscribe(HandlePlayerDamaged)
            .AddTo(_disposables);

        // --- ガード受付窓の開閉 → 構え枠 ---
        _guardWindowManager.IsGuarding
            .Subscribe(isGuarding => _guardView.SetStance(isGuarding))
            .AddTo(_disposables);

        _guardWindowManager.IsGuarding
            .Where(isGuarding => isGuarding)
            .Subscribe(_ => _se.Play(_sound.guardStance))
            .AddTo(_disposables);

        // --- 術発動結果 → ダメージ適用(成功・暴発の両方をBattleModelに委ねる) ---
        _spellSequenceModel.OnSpellCast
            .Subscribe(result => _battleModel.ApplySpellDamage(result))
            .AddTo(_disposables);

        // --- Cancel時のUI演出フック(現時点ではログのみ) ---
        _spellSequenceModel.OnSequenceReset
            .Subscribe(reason => Debug.Log($"[Spell] シーケンスリセット: {reason}"))
            .AddTo(_disposables);
    }

    private void Update()
    {
        _statusEffectManager?.Tick(Time.deltaTime);
        _guardWindowManager?.Tick(Time.deltaTime);
        _battleStatsModel?.Tick(Time.deltaTime);
    }

    private void HandleEnemyAttack(EnemyAction action)
    {
        bool isGuarding = _guardWindowManager.IsGuarding.CurrentValue;
        _battleModel.ApplyEnemyDamage(action, isGuarding);
        _spellEffectView.PlayEnemyAttackEffect(action.attackData.effectPrefab);
        // 攻撃の系統の音。当たった結果（被弾・ガード成功）の音は HandlePlayerDamaged で重ねる（B7）
        _se.Play(action.attackData.attackSE);

        string attackType = action.isHeavy ? "大技" : "通常";
        string guardStatus = isGuarding ? " [GUARD]" : "";
        Debug.Log($"[Enemy] 攻撃: {action.attackData.attackName}({attackType}) DMG:{action.attackData.damage}{guardStatus}");
    }

    private void HandlePlayerDamaged(PlayerDamageInfo info)
    {
        _damageNumberView.ShowPlayerDamage(info.Damage, info.WasGuarded);

        // 暴発の自傷では被弾音を鳴らさない（暴発の音は HandSignPresenter が鳴らす）
        if (info.IsMisfire)
        {
            _screenFlashView.FlashMisfire();
            return;
        }

        if (info.WasGuarded)
        {
            _se.Play(_sound.guardSuccess);
            _guardView.PlayGuardSuccess();
            _screenFlashView.FlashGuard();
            _cameraShakeView.ShakeGuard();
            return;
        }

        _se.Play(info.IsHeavy ? _sound.playerDamageHeavy : _sound.playerDamage);
        _screenFlashView.FlashDamage(info.IsHeavy);
        _cameraShakeView.ShakeDamage(info.IsHeavy);
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}
