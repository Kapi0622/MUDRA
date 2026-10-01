using System;
using MUDRA.Data;

/// <summary>
/// 継続回復（HoT）の時限効果。
/// 毎秒（TickInterval）ごとにperTickHealをBattleModelに適用する。
/// DotEffectと対になる構造だが、適用先が敵ではなくプレイヤーである点が異なる。
/// そのためセクション遷移時のClearEnemyEffectsでは除去されない。
/// </summary>
public class HotEffect : IStatusEffect
{
    public StatusEffectType Type => StatusEffectType.HealOverTime;
    public bool IsExpired => _remainingTime <= 0f;

    private const float TickInterval = 1.0f;

    private float _remainingTime;
    private float _tickTimer;
    private readonly int _perTickHeal;
    private readonly Action<int> _applyHeal;

    public HotEffect(float duration, int perTickHeal, Action<int> applyHeal)
    {
        _remainingTime = duration;
        _tickTimer = 0f;
        _perTickHeal = perTickHeal;
        _applyHeal = applyHeal;
    }

    public void OnApply() { }

    public void OnTick(float deltaTime)
    {
        _remainingTime -= deltaTime;
        _tickTimer += deltaTime;

        while (_tickTimer >= TickInterval)
        {
            _tickTimer -= TickInterval;
            _applyHeal(_perTickHeal);
        }
    }

    public void OnExpire() { }
}
