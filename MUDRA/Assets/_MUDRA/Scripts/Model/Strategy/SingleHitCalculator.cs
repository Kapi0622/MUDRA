using MUDRA.Data;

/// <summary>
/// 単発高火力のダメージ計算。
/// 最終ダメージ = basePower × 弱点倍率 × 速度ボーナス × コンボ倍率
/// </summary>
public class SingleHitCalculator : IDamageCalculator
{
    public DamageResult Calculate(SpellData spellData, EnemyData enemyData, float speedBonus, int comboCount)
    {
        bool isWeak = spellData.element == enemyData.weakElement;
        float weakMul = isWeak ? enemyData.weakMultiplier : 1.0f;
        float comboMul = 1.0f + comboCount * 0.1f;

        int totalDamage = (int)(spellData.basePower * weakMul * speedBonus * comboMul);

        return new DamageResult
        {
            TotalDamage = totalDamage,
            HitCount = 1,
            IsWeakness = isWeak,
            HasSpeedBonus = speedBonus > 1.0f,
            AppliedEffect = StatusEffectType.None,
            EffectDuration = 0f,
        };
    }
}
