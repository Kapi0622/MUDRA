using System;
using R3;
using MUDRA.Data;

/// <summary>
/// バトル全体のデータ管理。HP増減・ダメージ計算・勝敗判定を担う。
/// </summary>
public class BattleModel : IDisposable
{
    // --- 定数 ---
    private const float NormalGuardRate = 0.5f;
    private const float HeavyGuardRate = 0.3f;
    private const float MisfireDamageRate = 0.05f;
    // HoTのtick間隔。HotEffect.TickIntervalと一致させること
    private const float HealTickInterval = 1.0f;

    // --- HP ---
    private readonly ReactiveProperty<int> _playerHp;
    public ReadOnlyReactiveProperty<int> PlayerHp => _playerHp;
    public int PlayerMaxHp { get; }

    private readonly ReactiveProperty<int> _bossHp;
    public ReadOnlyReactiveProperty<int> BossHp => _bossHp;
    /// <summary>現在交戦中の敵の最大HP。SetEnemyで差し替わる。</summary>
    public int BossMaxHp { get; private set; }

    // --- コンボ ---
    private readonly ReactiveProperty<int> _comboCount = new(0);
    public ReadOnlyReactiveProperty<int> ComboCount => _comboCount;

    // --- バトル状態 ---
    private readonly ReactiveProperty<bool> _isBattleActive = new(false);
    public ReadOnlyReactiveProperty<bool> IsBattleActive => _isBattleActive;

    // --- 勝敗通知 ---
    private readonly Subject<bool> _onBattleEnd = new();
    /// <summary>
    /// 「現在交戦中の敵1体との」決着時に発火。true = 敵を撃破、false = プレイヤー敗北。
    /// ステージ全体の決着ではない点に注意。
    /// trueがステージクリアを意味するのか次セクションへの前進を意味するのかは
    /// SectionProgressManagerが判断する（BattleModelはセクションの概念を持たない）。
    /// </summary>
    public Observable<bool> OnBattleEnd => _onBattleEnd;

    // --- 敵差し替え通知 ---
    private readonly Subject<EnemyData> _onEnemyChanged = new();
    /// <summary>
    /// SetEnemyで交戦相手が差し替わった時に発火。
    /// BossHpの値変化だけではBossMaxHpが変わったことをViewに伝えられないため、
    /// HPバーの再初期化トリガとして別途用意している。
    /// </summary>
    public Observable<EnemyData> OnEnemyChanged => _onEnemyChanged;

    // --- 敵データ ---
    // セクション遷移で差し替わるためreadonlyにできない
    private EnemyData _enemyData;
    
    // --- StatusEffect関連の依存解決用 ---
    private StatusEffectManager _statusEffectManager;
    private StatusEffectFactory _statusEffectFactory;

    public BattleModel(int playerMaxHp, EnemyData enemyData)
    {
        PlayerMaxHp = playerMaxHp;
        BossMaxHp = enemyData.maxHp;
        _playerHp = new ReactiveProperty<int>(playerMaxHp);
        _bossHp = new ReactiveProperty<int>(enemyData.maxHp);
        _enemyData = enemyData;
        _isBattleActive.Value = true;
    }
    
    /// <summary>
    /// StatusEffect関連の依存を注入する。BattleInitializerからコンストラクタ後に1回だけ呼ぶ。
    /// EnemyStateManagerとの循環依存を避けるため、コンストラクタでなくこのメソッドで注入する。
    /// </summary>
    public void SetStatusEffectDependencies(StatusEffectManager statusEffectManager, StatusEffectFactory statusEffectFactory)
    {
        _statusEffectManager = statusEffectManager;
        _statusEffectFactory = statusEffectFactory;
    }

    /// <summary>
    /// 交戦相手を差し替える。セクション遷移時にSectionProgressManagerから呼ぶ。
    /// 敵側の状態（HP・最大HP）のみをリセットし、
    /// プレイヤーHPとコンボは意図的に据え置く（セクション間の引き継ぎ要素）。
    /// 決着済みで落ちている_isBattleActiveもここでtrueに戻す。
    /// </summary>
    public void SetEnemy(EnemyData enemyData)
    {
        _enemyData = enemyData;
        BossMaxHp = enemyData.maxHp;
        _bossHp.Value = enemyData.maxHp;
        _isBattleActive.Value = true;

        _onEnemyChanged.OnNext(enemyData);
    }

    /// <summary>
    /// プレイヤーを回復する。HotEffectから毎秒呼ばれる。
    /// PlayerMaxHpを上限にクランプする。
    /// 回復は勝敗に影響しないためCheckBattleEndは呼ばない
    /// （HPが0の時点で既に決着済みであり、そこから回復して復帰することはない）。
    /// </summary>
    public void ApplyHeal(int amount)
    {
        if (!_isBattleActive.Value) return;
        if (amount <= 0) return;

        _playerHp.Value = Math.Min(PlayerMaxHp, _playerHp.Value + amount);
    }

    /// <summary>
    /// 術の発動結果を受けてダメージを処理する。
    /// 成功時はボスへダメージ+コンボ加算、暴発時はセルフダメージ+コンボリセット。
    /// </summary>
    public void ApplySpellDamage(SpellCastResult result)
    {
        if (!_isBattleActive.Value) return;

        if (!result.IsSuccess)
        {
            ApplyMisfireDamage();
            return;
        }

        var calculator = ResolveCalculator(result.Spell.damageType);
        var damageResult = calculator.Calculate(
            result.Spell, _enemyData, result.SpeedBonus, _comboCount.Value
        );

        _bossHp.Value = Math.Max(0, _bossHp.Value - damageResult.TotalDamage);
        _comboCount.Value++;

        // --- 副次効果の付与 ---
        if (_statusEffectFactory != null && _statusEffectManager != null)
        {
            // 回復量だけはStrategyで算出しない。
            // 弱点属性・速度ボーナス・コンボはいずれも「敵に与えるダメージ」の倍率であり、
            // 自分の回復量に乗せる筋合いがないため、SpellDataの値をそのまま分割する。
            if (result.Spell.statusEffect == StatusEffectType.HealOverTime)
                damageResult.PerTickHeal = CalculatePerTickHeal(result.Spell);

            var effect = _statusEffectFactory.CreateFromDamageResult(damageResult);
            if (effect != null)
                _statusEffectManager.ApplyEffect(effect);
        }
        
        CheckBattleEnd();
    }
    
    /// <summary>
    /// DoTのtickダメージをボスに適用する。
    /// StatusEffectManager経由でDotEffectから毎秒呼ばれる。
    /// tickダメージにはコンボ倍率・速度ボーナスを乗せない（初撃のみ適用の設計方針）。
    /// </summary>
    public void ApplyDotDamage(int damage)
    {
        if (!_isBattleActive.Value) return;

        _bossHp.Value = Math.Max(0, _bossHp.Value - damage);
        CheckBattleEnd();
    }

    /// <summary>
    /// ボスの攻撃ダメージをプレイヤーに適用する。
    /// </summary>
    public void ApplyEnemyDamage(EnemyAction action, bool isGuarding)
    {
        if (!_isBattleActive.Value) return;
        
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsInvincible) return;
    #endif

        int baseDamage = action.attackData.damage;

        if (isGuarding)
        {
            float guardRate = action.isHeavy ? HeavyGuardRate : NormalGuardRate;
            baseDamage = (int)(baseDamage * guardRate);
        }

        _playerHp.Value = Math.Max(0, _playerHp.Value - baseDamage);
        CheckBattleEnd();
    }

    /// <summary>
    /// 暴発時のセルフダメージ。MaxHpの固定割合ダメージ+コンボリセット。
    /// </summary>
    private void ApplyMisfireDamage()
    {
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsInvincible)
        {
            _comboCount.Value = 0;  // コンボリセットだけは無敵でも発生させる
            return;
        }
    #endif
        
        int damage = (int)(PlayerMaxHp * MisfireDamageRate);
        _playerHp.Value = Math.Max(0, _playerHp.Value - damage);
        _comboCount.Value = 0;
        CheckBattleEnd();
    }

    /// <summary>
    /// HoTのtick1回あたりの回復量を算出する。
    /// healPowerを持続時間で割って均等に分配する（端数は切り捨て）。
    /// </summary>
    private static int CalculatePerTickHeal(SpellData spell)
    {
        int tickCount = spell.statusEffectDuration > 0f
            ? (int)(spell.statusEffectDuration / HealTickInterval)
            : 0;

        return tickCount > 0 ? (int)(spell.healPower / tickCount) : 0;
    }

    private void CheckBattleEnd()
    {
        if (!_isBattleActive.Value) return;

        if (_bossHp.Value <= 0)
        {
            _isBattleActive.Value = false;
            _onBattleEnd.OnNext(true);
        }
        else if (_playerHp.Value <= 0)
        {
            _isBattleActive.Value = false;
            _onBattleEnd.OnNext(false);
        }
    }

    private IDamageCalculator ResolveCalculator(DamageType damageType)
    {
        return damageType switch
        {
            DamageType.SingleHit => new SingleHitCalculator(),
            DamageType.MultiHit => new MultiHitCalculator(),
            DamageType.DamageOverTime => new DamageOverTimeCalculator(),
            _ => throw new ArgumentOutOfRangeException(nameof(damageType), damageType, null),
        };
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // デバッグ専用

    /// <summary> 無敵モードフラグ。trueの間、プレイヤーへのダメージを無効化する。 </summary>
    public bool IsInvincible { get; set; }

    /// <summary>
    /// プレイヤー側の状態を初期値に戻す。デバッグのステージジャンプから呼ぶ。
    /// 削れたHPや積み上がったコンボ倍率が残っているとダメージ量の見積もりが狂い、
    /// 飛んだ先のセクション単体のバランスを観察できないため。
    /// 敵側（BossHp・_enemyData）には触らない。差し替えはSetEnemyの担当。
    /// </summary>
    public void DebugResetPlayerState()
    {
        _playerHp.Value = PlayerMaxHp;
        _comboCount.Value = 0;
    }

    public void DebugModifyPlayerHp(int delta)
    {
        _playerHp.Value = Math.Clamp(_playerHp.Value + delta, 0, PlayerMaxHp);
        CheckBattleEnd();
    }

    public void DebugModifyBossHp(int delta)
    {
        _bossHp.Value = Math.Clamp(_bossHp.Value + delta, 0, BossMaxHp);
        CheckBattleEnd();
    }
#endif
    
    public void Dispose()
    {
        _playerHp.Dispose();
        _bossHp.Dispose();
        _comboCount.Dispose();
        _isBattleActive.Dispose();
        _onBattleEnd.Dispose();
        _onEnemyChanged.Dispose();
    }
}