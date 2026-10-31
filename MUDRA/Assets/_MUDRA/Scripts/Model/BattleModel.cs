using System;
using R3;
using MUDRA.Data;

/// <summary>
/// バトル全体のデータ管理。HP増減・ダメージ計算・勝敗判定を担う。
/// </summary>
public class BattleModel : IDisposable
{
    // --- 定数 ---
    // HoTのtick間隔。HotEffect.TickIntervalと一致させること
    private const float HealTickInterval = 1.0f;

    // ガード軽減率・暴発ダメージ・コンボ・連続減衰の値（B8でconstから移した）
    private readonly BalanceData _balance;

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

    // --- 連続減衰（B8） ---
    // 直前に撃った「ダメージのある術」の属性。null = まだ撃っていない（敵が替わった直後を含む）
    private readonly ReactiveProperty<ElementType?> _lastAttackElement = new(null);
    /// <summary>
    /// 直前に撃った攻撃術の属性。次に同じ属性の術を撃つと威力が下がる。
    /// 詠唱中の術ガイドで「どの候補が減衰するか」を示すために公開している。
    /// 1つの術・1つの弱点属性の連打が最適解にならないようにするための仕組みで、
    /// 「同じ術」ではなく「同じ属性」で見るのは、同じ属性の術どうしの交互撃ちも抑えるため。
    /// </summary>
    public ReadOnlyReactiveProperty<ElementType?> LastAttackElement => _lastAttackElement;

    // --- Stunの確率（B8） ---
    // 0〜1の乱数。テストで固定できるよう外から渡す
    private readonly Func<float> _random01;
    // 今の敵にStunが続けて入った回数。失敗・敵の交代で0に戻る
    private int _stunStreak;

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

    // --- 演出用の出来事通知（B4） ---
    // HPのReactivePropertyは「値がいくつになったか」しか伝えられず、
    // ダメージ数字・弱点表示・被弾/ガード演出に必要な「何が起きたか」が分からないため、
    // HP変化とは別に出来事そのものを流す。いずれもHPを書き換えた直後・CheckBattleEndより前に発火し、
    // トドメの一撃でも演出が出るようにしている。

    private readonly Subject<DamageResult> _onSpellHit = new();
    /// <summary>術が敵に命中した時に発火。ダメージ数字・弱点/速度ボーナス表示・MultiHit時間差表示に使う</summary>
    public Observable<DamageResult> OnSpellHit => _onSpellHit;

    private readonly Subject<int> _onDotTick = new();
    /// <summary>DoTのtickダメージが敵に入った時に発火。引数はダメージ量</summary>
    public Observable<int> OnDotTick => _onDotTick;

    private readonly Subject<PlayerDamageInfo> _onPlayerDamaged = new();
    /// <summary>プレイヤーがダメージを受けた時に発火（敵の攻撃・暴発の両方）。無敵モード中は発火しない</summary>
    public Observable<PlayerDamageInfo> OnPlayerDamaged => _onPlayerDamaged;

    private readonly Subject<int> _onHealed = new();
    /// <summary>プレイヤーが回復した時に発火。引数はMaxHPでクランプした後の実回復量</summary>
    public Observable<int> OnHealed => _onHealed;

    // --- 敵データ ---
    // セクション遷移で差し替わるためreadonlyにできない
    private EnemyData _enemyData;
    
    // --- StatusEffect関連の依存解決用 ---
    private StatusEffectManager _statusEffectManager;
    private StatusEffectFactory _statusEffectFactory;

    /// <param name="balance">調整値。PlayerMaxHpだけは生成時に確定し、他の値は使う度に読む</param>
    /// <param name="random01">0〜1の乱数を返す関数。Stunの確率判定に使う</param>
    public BattleModel(BalanceData balance, EnemyData enemyData, Func<float> random01)
    {
        _balance = balance;
        _random01 = random01;
        PlayerMaxHp = balance.playerMaxHp;
        BossMaxHp = enemyData.maxHp;
        _playerHp = new ReactiveProperty<int>(PlayerMaxHp);
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
    /// 連続減衰の記憶とStunの連続成功回数は消す（新しい敵には何を撃っても減衰しない・Stunは基本の確率に戻る）。
    /// 決着済みで落ちている_isBattleActiveもここでtrueに戻す。
    /// </summary>
    public void SetEnemy(EnemyData enemyData)
    {
        _enemyData = enemyData;
        BossMaxHp = enemyData.maxHp;
        _bossHp.Value = enemyData.maxHp;
        _isBattleActive.Value = true;
        _lastAttackElement.Value = null;
        _stunStreak = 0;

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

        int before = _playerHp.Value;
        _playerHp.Value = Math.Min(PlayerMaxHp, _playerHp.Value + amount);

        // 満タン時は実回復量0になる。「+0」を出しても意味がないので通知しない
        int healed = _playerHp.Value - before;
        if (healed > 0)
            _onHealed.OnNext(healed);
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

        var spell = result.Spell;
        // 回復術（威力0）は連続減衰の対象外で、記憶も更新しない。
        // 更新すると「攻撃→回復→同じ攻撃」で減衰を消せてしまうため
        bool isAttack = spell.basePower > 0f;
        bool isRepeated = isAttack && _lastAttackElement.Value == spell.element;

        var bonus = new DamageBonus(
            result.SpeedBonus,
            CalculateComboMultiplier(_comboCount.Value),
            isRepeated ? _balance.repeatElementMultiplier : 1.0f);

        var calculator = ResolveCalculator(spell.damageType);
        var damageResult = calculator.Calculate(spell, _enemyData, bonus);

        _bossHp.Value = Math.Max(0, _bossHp.Value - damageResult.TotalDamage);
        _comboCount.Value++;
        if (isAttack)
            _lastAttackElement.Value = spell.element;

        // Stunは確率で判定する。OnSpellHitより前に決め、失敗を数字のラベルで見せる
        if (damageResult.AppliedEffect == StatusEffectType.Stun && ShouldRollStun() && !RollStun())
        {
            damageResult.AppliedEffect = StatusEffectType.None;
            damageResult.StunResisted = true;
        }

        _onSpellHit.OnNext(damageResult);

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
        _onDotTick.OnNext(damage);
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
            float guardRate = action.isHeavy ? _balance.heavyGuardRate : _balance.normalGuardRate;
            baseDamage = (int)(baseDamage * guardRate);
        }

        _playerHp.Value = Math.Max(0, _playerHp.Value - baseDamage);
        _onPlayerDamaged.OnNext(new PlayerDamageInfo(baseDamage, isGuarding, action.isHeavy, isMisfire: false));
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
        
        int damage = (int)(PlayerMaxHp * _balance.misfireDamageRate);
        _playerHp.Value = Math.Max(0, _playerHp.Value - damage);
        _comboCount.Value = 0;
        _onPlayerDamaged.OnNext(new PlayerDamageInfo(damage, wasGuarded: false, isHeavy: false, isMisfire: true));
        CheckBattleEnd();
    }

    /// <summary>
    /// HoTのtick1回あたりの回復量を算出する。
    /// healPowerを持続時間で割って均等に分配する（端数は切り捨て）。
    /// </summary>
    /// <summary>
    /// Stunの確率判定をするか。倒した一撃と、既にStun中の敵への一撃は判定しない。
    /// どちらもStunは実際には掛からない（後者はStatusEffectManagerが重複を無視する）ので、
    /// 判定すると連続成功回数だけが進んだり「抵抗」が出たりして、見た目と中身がずれるため。
    /// </summary>
    private bool ShouldRollStun()
    {
        if (_bossHp.Value <= 0) return false;
        return _statusEffectManager == null || !_statusEffectManager.HasEffect(StatusEffectType.Stun);
    }

    /// <summary>
    /// Stunが入るかを判定する（B8の封殺し対策）。
    /// 確率 = stunBaseChance × 敵のstunChanceMultiplier × stunChanceDecay ^ 連続成功回数。
    /// 入るたびに次が入りにくくなり、失敗したら基本の確率に戻る。
    /// 運の要素を残しつつ、同じ敵を止め続けられる確率を急速に下げるための形。
    /// </summary>
    private bool RollStun()
    {
        float chance = _balance.stunBaseChance
                       * _enemyData.stunChanceMultiplier
                       * MathF.Pow(_balance.stunChanceDecay, _stunStreak);
        // 1以上は必ず入れる（UnityEngine.Random.value は 1.0 ちょうどを返すことがある）
        if (chance >= 1f || _random01() < chance)
        {
            _stunStreak++;
            return true;
        }
        _stunStreak = 0;
        return false;
    }

    /// <summary>
    /// コンボ数から倍率を出す。倍率 = 1 + comboBonusPerCount × min(コンボ数, comboBonusCap)。
    /// 上限を超えた分は倍率に乗せない（カウント自体は表示とランク評価のため増え続ける）
    /// </summary>
    private float CalculateComboMultiplier(int comboCount)
    {
        return 1.0f + _balance.comboBonusPerCount * Math.Min(comboCount, _balance.comboBonusCap);
    }

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
        _onSpellHit.Dispose();
        _onDotTick.Dispose();
        _onPlayerDamaged.Dispose();
        _onHealed.Dispose();
        _lastAttackElement.Dispose();
    }
}