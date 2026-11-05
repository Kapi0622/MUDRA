using MUDRA.Data;

/// <summary>
/// ダメージ計算結果。Strategyからの戻り値として使用する。
/// </summary>
public struct DamageResult
{
    public int TotalDamage;
    public int PerHitDamage; // 1ヒットあたりのダメージ（MultiHitのView側時間差表示用）
    public int HitCount;
    public bool HasSpeedBonus; // 速度ボーナスが適用されたか
    public bool IsRepeated; // 直前と同じ属性で連続減衰がかかったか（B8）
    public StatusEffectType AppliedEffect;
    public float EffectDuration;
    public int PerTickDamage;       // tick1回あたりのダメージ（DoT専用。他Strategyは0）
    public int TickCount;           // tick回数（DoT専用。他Strategyは0）
    public bool StunResisted;       // Stunが確率判定で入らなかったか（B8）。BattleModelが埋める

    /// <summary>術と敵の属性の関係（B8.3）。BattleModelが埋める（Strategyは倍率しか受け取らない）</summary>
    public ElementRelation Relation;

    /// <summary>敵の弱点を突いたか（有利）。チュートリアル・効果音・戦績・ダメージ数字が見る</summary>
    public bool IsWeakness => Relation == ElementRelation.Advantage;

    /// <summary>敵に耐性があったか（不利）。ダメージ数字の「耐性」表示用</summary>
    public bool IsDisadvantage => Relation == ElementRelation.Disadvantage;

    // tick1回あたりの回復量（HoT専用）。
    // 回復は相性やコンボに影響されないため、Strategyでは算出せず
    // BattleModel.ApplySpellDamageがSpellDataから直接埋める。
    public int PerTickHeal;
}