using MUDRA.Data;

/// <summary>
/// 単発高火力のダメージ計算。
/// 最終ダメージ = basePower × 弱点倍率 × 速度ボーナス × コンボ倍率 × 連続減衰
/// </summary>
public class SingleHitCalculator : IDamageCalculator
{
    public DamageResult Calculate(SpellData spellData, EnemyData enemyData, DamageBonus bonus)
    {
        bool isWeak = spellData.element == enemyData.weakElement;
        float weakMul = isWeak ? enemyData.weakMultiplier : 1.0f;

        int totalDamage = (int)(spellData.basePower * weakMul * bonus.Total);

        return new DamageResult
        {
            TotalDamage = totalDamage,
            PerHitDamage = totalDamage,
            HitCount = 1,
            IsWeakness = isWeak,
            HasSpeedBonus = bonus.HasSpeedBonus,
            IsRepeated = bonus.IsRepeated,
            AppliedEffect = spellData.statusEffect,
            EffectDuration = spellData.statusEffectDuration,
        };
    }
}
