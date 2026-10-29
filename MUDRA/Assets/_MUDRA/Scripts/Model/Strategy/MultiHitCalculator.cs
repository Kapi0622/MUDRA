using MUDRA.Data;

/// <summary>
/// 多段ヒットのダメージ計算。
/// 1ヒットあたり = (basePower / hitCount) × 弱点倍率 × 速度ボーナス × コンボ倍率 × 連続減衰
/// 合計ダメージ = 1ヒットあたり × hitCount
///
/// 端数処理の注意:
/// 1ヒットあたりをintに丸めてからhitCount倍すると、
/// SingleHitで同じbasePowerを撃った場合と合計値がズレる。
/// 「多段の方が総ダメージが微妙に低い」は仕様として許容する。
/// </summary>
public class MultiHitCalculator : IDamageCalculator
{
    public DamageResult Calculate(SpellData spellData, EnemyData enemyData, DamageBonus bonus)
    {
        bool isWeak = spellData.element == enemyData.weakElement;
        float weakMul = isWeak ? enemyData.weakMultiplier : 1.0f;

        int hitCount = spellData.hitCount > 0 ? spellData.hitCount : 1;
        int perHitDamage = (int)(spellData.basePower / hitCount * weakMul * bonus.Total);
        int totalDamage = perHitDamage * hitCount;

        return new DamageResult
        {
            TotalDamage = totalDamage,
            PerHitDamage = perHitDamage,
            HitCount = hitCount,
            IsWeakness = isWeak,
            HasSpeedBonus = bonus.HasSpeedBonus,
            IsRepeated = bonus.IsRepeated,
            AppliedEffect = spellData.statusEffect,
            EffectDuration = spellData.statusEffectDuration,
        };
    }
}