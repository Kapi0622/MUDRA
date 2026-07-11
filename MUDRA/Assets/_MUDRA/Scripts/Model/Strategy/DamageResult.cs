using MUDRA.Data;

/// <summary>
/// ダメージ計算結果。Strategyからの戻り値として使用する。
/// </summary>
public struct DamageResult
{
    public int TotalDamage;
    public int PerHitDamage;
    public int HitCount;
    public bool IsWeakness;
    public bool HasSpeedBonus;
    public StatusEffectType AppliedEffect;
    public float EffectDuration;
}