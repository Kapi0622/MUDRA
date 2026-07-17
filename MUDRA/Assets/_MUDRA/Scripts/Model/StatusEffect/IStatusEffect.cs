using MUDRA.Data;

/// <summary>
/// 時限効果（DoT・Stun・Slow等）の共通インターフェース。
/// StatusEffectManagerがコレクションで保持し、
/// OnApply→OnTick(毎フレーム)→OnExpireのライフサイクルで駆動する。
/// </summary>
public interface IStatusEffect
{
    StatusEffectType Type { get; }
    bool IsExpired { get; }

    /// <summary>効果開始時に1回だけ呼ばれる</summary>
    void OnApply();

    /// <summary>毎フレームの時間経過処理</summary>
    void OnTick(float deltaTime);

    /// <summary>効果終了時に1回だけ呼ばれる</summary>
    void OnExpire();
}