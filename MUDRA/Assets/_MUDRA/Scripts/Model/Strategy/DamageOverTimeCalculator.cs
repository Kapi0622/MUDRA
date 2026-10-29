using MUDRA.Data;

/// <summary>
/// 継続ダメージ（DoT）の計算部分のみ。
/// 初撃ダメージ = basePower × 0.3 × 弱点倍率 × 速度ボーナス × コンボ倍率 × 連続減衰
/// tick1回分ダメージ = basePower × 0.7 / tick数 × 弱点倍率（速度・コンボ・連続減衰は乗せない）
/// tick数 = statusEffectDuration（秒） ÷ 1.0（tick間隔固定）
/// このCalculatorは「各値がいくらになるか」を算出するだけ。
/// </summary>
public class DamageOverTimeCalculator : IDamageCalculator
{
    private const float InitialDamageRatio = 0.3f;
    private const float DotDamageRatio = 0.7f;
    private const float TickInterval = 1.0f;

    public DamageResult Calculate(SpellData spellData, EnemyData enemyData, DamageBonus bonus)
    {
        bool isWeak = spellData.element == enemyData.weakElement;
        float weakMul = isWeak ? enemyData.weakMultiplier : 1.0f;

        // 初撃: 弱点に加えて速度・コンボ・連続減衰を乗せる（倍率は初撃にのみ適用する方針）
        int initialDamage = (int)(spellData.basePower * InitialDamageRatio * weakMul * bonus.Total);

        // tick情報の算出
        int tickCount = spellData.statusEffectDuration > 0f
            ? (int)(spellData.statusEffectDuration / TickInterval)
            : 0;
        int perTickDamage = tickCount > 0
            ? (int)(spellData.basePower * DotDamageRatio / tickCount * weakMul)
            : 0;

        // A3時点: TotalDamageには初撃分のみ。tick駆動はA5で接続する
        return new DamageResult
        {
            TotalDamage = initialDamage,
            PerHitDamage = initialDamage,
            HitCount = 1,
            IsWeakness = isWeak,
            HasSpeedBonus = bonus.HasSpeedBonus,
            IsRepeated = bonus.IsRepeated,
            AppliedEffect = spellData.statusEffect,
            EffectDuration = spellData.statusEffectDuration,
            PerTickDamage = perTickDamage,
            TickCount = tickCount,
        };
    }
}