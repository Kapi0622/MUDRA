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

    // --- HP ---
    private readonly ReactiveProperty<int> _playerHp;
    public ReadOnlyReactiveProperty<int> PlayerHp => _playerHp;
    public int PlayerMaxHp { get; }

    private readonly ReactiveProperty<int> _bossHp;
    public ReadOnlyReactiveProperty<int> BossHp => _bossHp;
    public int BossMaxHp { get; }

    // --- コンボ ---
    private readonly ReactiveProperty<int> _comboCount = new(0);
    public ReadOnlyReactiveProperty<int> ComboCount => _comboCount;

    // --- バトル状態 ---
    private readonly ReactiveProperty<bool> _isBattleActive = new(false);
    public ReadOnlyReactiveProperty<bool> IsBattleActive => _isBattleActive;

    // --- 勝敗通知 ---
    private readonly Subject<bool> _onBattleEnd = new();
    /// <summary>
    /// バトル終了時に発火。true = プレイヤー勝利、false = 敗北。
    /// </summary>
    public Observable<bool> OnBattleEnd => _onBattleEnd;

    // --- 敵データ ---
    private readonly EnemyData _enemyData;

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
        CheckBattleEnd();
    }

    /// <summary>
    /// ボスの攻撃ダメージをプレイヤーに適用する。
    /// </summary>
    public void ApplyEnemyDamage(EnemyAction action, bool isGuarding)
    {
        if (!_isBattleActive.Value) return;

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
        int damage = (int)(PlayerMaxHp * MisfireDamageRate);
        _playerHp.Value = Math.Max(0, _playerHp.Value - damage);
        _comboCount.Value = 0;
        CheckBattleEnd();
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

    public void Dispose()
    {
        _playerHp.Dispose();
        _bossHp.Dispose();
        _comboCount.Dispose();
        _isBattleActive.Dispose();
        _onBattleEnd.Dispose();
    }
}