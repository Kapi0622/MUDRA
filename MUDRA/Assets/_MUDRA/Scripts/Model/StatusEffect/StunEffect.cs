using System;
using MUDRA.Data;

/// <summary>
/// スタンの時限効果。
/// OnApplyでEnemyStateManagerをStunned状態に遷移させ、
/// OnExpireでStunを解除してIdle復帰→ループ再開を指示する。
/// 時間管理はStatusEffectManagerが駆動するOnTickで行う。
/// </summary>
public class StunEffect : IStatusEffect
{
    public StatusEffectType Type => StatusEffectType.Stun;
    public bool IsExpired => _remainingTime <= 0f;

    private float _remainingTime;
    private readonly Action _applyStun;
    private readonly Action _endStun;

    public StunEffect(float duration, Action applyStun, Action endStun)
    {
        _remainingTime = duration;
        _applyStun = applyStun;
        _endStun = endStun;
    }

    public void OnApply() => _applyStun();

    public void OnTick(float deltaTime)
    {
        _remainingTime -= deltaTime;
    }

    public void OnExpire() => _endStun();
}