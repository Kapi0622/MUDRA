using UnityEngine;
using MUDRA.Data;

/// <summary>
/// 発動印(Release)受信時の判定結果。
/// 成功・暴発の両方をこの型に統一して OnSpellCast で通知する。
/// ComboCount は BattleModel が自身の ReactiveProperty を直接参照するため持たない
/// (SpellSequenceModel はコンボ状態を知らない設計を維持する)。
/// </summary>
public readonly struct SpellCastResult
{
    /// <summary>true: 術発動成功 / false: 暴発</summary>
    public readonly bool IsSuccess;

    /// <summary>成功時: 発動した術データ / 暴発時: null</summary>
    public readonly SpellData Spell;

    /// <summary>速度ボーナス倍率。暴発時は 1.0 固定(未使用値)</summary>
    public readonly float SpeedBonus;

    public SpellCastResult(bool isSuccess, SpellData spell, float speedBonus)
    {
        IsSuccess = isSuccess;
        Spell = spell;
        SpeedBonus = speedBonus;
    }
}