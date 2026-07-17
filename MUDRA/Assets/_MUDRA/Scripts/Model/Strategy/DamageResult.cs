using MUDRA.Data;

/// <summary>
/// ダメージ計算結果。Strategyからの戻り値として使用する。
/// </summary>
public struct DamageResult
{
    public int TotalDamage;
    public int PerHitDamage; // 1ヒットあたりのダメージ（MultiHitのView側時間差表示用）
    public int HitCount;
    public bool IsWeakness; // 弱点属性かどうか
    public bool HasSpeedBonus; // 速度ボーナスが適用されたか
    public StatusEffectType AppliedEffect;
    public float EffectDuration;
    public int PerTickDamage;       // tick1回あたりのダメージ（DoT専用。他Strategyは0）
    public int TickCount;           // tick回数（DoT専用。他Strategyは0）
}