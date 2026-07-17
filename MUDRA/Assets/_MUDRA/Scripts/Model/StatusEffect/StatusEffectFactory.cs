using System;
using MUDRA.Data;

/// <summary>
/// DamageResultの情報からIStatusEffectを生成するファクトリ。
/// 呼び出し元（BattleModel等）がDotEffect/StunEffectの生成詳細を知らなくて済むようにする。
/// </summary>
public class StatusEffectFactory
{
    private readonly Action<int> _applyDotDamage;
    private readonly Action _applyStun;
    private readonly Action _endStun;

    public StatusEffectFactory(
        Action<int> applyDotDamage,
        Action applyStun,
        Action endStun)
    {
        _applyDotDamage = applyDotDamage;
        _applyStun = applyStun;
        _endStun = endStun;
    }

    /// <summary>
    /// DamageResultからStatusEffectを生成する。
    /// 効果なし（None）の場合はnullを返す。
    /// </summary>
    public IStatusEffect CreateFromDamageResult(DamageResult result)
    {
        return result.AppliedEffect switch
        {
            StatusEffectType.DamageOverTime => new DotEffect(
                result.EffectDuration,
                result.PerTickDamage,
                _applyDotDamage
            ),
            StatusEffectType.Stun => new StunEffect(
                result.EffectDuration,
                _applyStun,
                _endStun
            ),
            StatusEffectType.None => null,
            _ => null,
        };
    }
}