using MUDRA.Data;

/// <summary>
/// 単発高火力のダメージ計算。
/// 最終ダメージ = basePower × 相性倍率 × 速度ボーナス × コンボ倍率 × 連続減衰
/// </summary>
public class SingleHitCalculator : IDamageCalculator
{
    public DamageResult Calculate(SpellData spellData, float affinity, DamageBonus bonus)
    {
        int totalDamage = (int)(spellData.basePower * affinity * bonus.Total);

        return new DamageResult
        {
            TotalDamage = totalDamage,
            PerHitDamage = totalDamage,
            HitCount = 1,
            HasSpeedBonus = bonus.HasSpeedBonus,
            IsRepeated = bonus.IsRepeated,
            AppliedEffect = spellData.statusEffect,
            EffectDuration = spellData.statusEffectDuration,
        };
    }
}
