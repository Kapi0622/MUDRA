using System;
using MUDRA.Data;

/// <summary>
/// 継続ダメージ（DoT）の時限効果。
/// 毎秒（TickInterval）ごとにperTickDamageをBattleModelに適用する。
/// 初撃ダメージはBattleModel.ApplySpellDamage側で処理済みのため、
/// このクラスはtickダメージのスケジュール管理のみを担う。
/// </summary>
public class DotEffect : IStatusEffect
{
    public StatusEffectType Type => StatusEffectType.DamageOverTime;
    public bool IsExpired => _remainingTime <= 0f;

    private const float TickInterval = 1.0f;

    private float _remainingTime;
    private float _tickTimer;
    private readonly int _perTickDamage;
    private readonly Action<int> _applyDamage;

    public DotEffect(float duration, int perTickDamage, Action<int> applyDamage)
    {
        _remainingTime = duration;
        _tickTimer = 0f;
        _perTickDamage = perTickDamage;
        _applyDamage = applyDamage;
    }

    public void OnApply() { }

    public void OnTick(float deltaTime)
    {
        _remainingTime -= deltaTime;
        _tickTimer += deltaTime;

        while (_tickTimer >= TickInterval)
        {
            _tickTimer -= TickInterval;
            _applyDamage(_perTickDamage);
        }
    }

    public void OnExpire() { }
}