using UnityEngine;
using LitMotion;

/// <summary>
/// カメラを揺らすView（B4）。Main Cameraに付ける。
/// 揺らすのはワールド（背景・敵・エフェクト）だけで、Overlay CanvasのHUDは揺れない。
/// 揺れの強さの意味づけ（大技は強く、ガード時は弱く）はこのViewが持つ。
/// </summary>
public class CameraShakeView : MonoBehaviour
{
    // --- 演出定数 ---
    private const float DamageAmplitude = 0.15f;
    private const float HeavyDamageAmplitude = 0.35f;
    private const float GuardAmplitude = 0.06f;
    private const float ShakeDuration = 0.3f;
    private const float HeavyShakeDuration = 0.45f;
    private const float ShakeFrequencyX = 37f;
    private const float ShakeFrequencyY = 29f;      // X と周期をずらして単調な往復にしない
    private const float BossRoarAmplitude = 0.4f;
    private const float BossRoarDuration = 0.6f;
    private const float BossDefeatAmplitude = 0.5f;
    private const float BossDefeatDuration = 0.8f;

    private Vector3 _basePosition;
    private MotionHandle _shakeHandle;

    private void Awake()
    {
        _basePosition = transform.localPosition;
    }

    /// <summary>敵の攻撃を受けた。大技は強く長く揺らす。</summary>
    public void ShakeDamage(bool isHeavy)
    {
        Shake(isHeavy ? HeavyDamageAmplitude : DamageAmplitude,
            isHeavy ? HeavyShakeDuration : ShakeDuration);
    }

    /// <summary>ガードで受け止めた。手応えだけ伝える程度に小さく揺らす。</summary>
    public void ShakeGuard()
    {
        Shake(GuardAmplitude, ShakeDuration);
    }

    /// <summary>ボスの咆哮（登場シーケンスの山場）。登場の開始からdelay秒後に揺らす。</summary>
    public void ShakeBossRoar(float delay)
    {
        Shake(BossRoarAmplitude, BossRoarDuration, delay);
    }

    /// <summary>ボスの撃破。ヒットストップ中は止まり、時間が戻ってから揺れる。</summary>
    public void ShakeBossDefeat()
    {
        Shake(BossDefeatAmplitude, BossDefeatDuration);
    }

    /// <summary>
    /// 減衰する揺れ。連続で呼ばれたら前の揺れを止め、基準位置から揺れ直す。
    /// </summary>
    private void Shake(float amplitude, float duration, float delay = 0f)
    {
        if (_shakeHandle.IsActive()) _shakeHandle.Cancel();

        _shakeHandle = LMotion.Create(1f, 0f, duration)
            .WithDelay(delay)
            .WithOnComplete(() => transform.localPosition = _basePosition)
            .WithOnCancel(() => transform.localPosition = _basePosition)
            .Bind(decay =>
            {
                float elapsed = (1f - decay) * duration;
                var offset = new Vector3(
                    Mathf.Sin(elapsed * ShakeFrequencyX),
                    Mathf.Sin(elapsed * ShakeFrequencyY),
                    0f) * (amplitude * decay);
                transform.localPosition = _basePosition + offset;
            });
    }

    private void OnDestroy()
    {
        if (_shakeHandle.IsActive()) _shakeHandle.Cancel();
    }
}
