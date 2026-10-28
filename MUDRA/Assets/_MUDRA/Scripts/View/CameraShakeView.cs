using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// カメラを揺らすView（B4）。Main Cameraに付ける。
/// 揺らすのはワールド（背景・敵・エフェクト）だけで、Overlay CanvasのHUDは揺れない。
/// 揺れの強さの意味づけ（大技は強く、ガード時は弱く）はこのViewが持つ。
///
/// 3D化（ステップ2a）でカメラの位置はCinemachineBrainが毎フレーム書くようになったため、
/// Transformを直接動かさず、CinemachineImpulseSourceで揺れを発生させる。
/// 揺れは仮想カメラ（CinemachineImpulseListener）の出力に足されるので、カメラの移動（前進演出）とぶつからない。
/// Impulseは既定でゲーム内時間を使うため、ヒットストップ・ポーズ中は揺れが止まり、時間が戻ると続きから揺れる。
///
/// 揺れの波形はB4の式（減衰する正弦波）をカスタムカーブにして渡す。
/// 標準のRumble等は「0から膨らんで山が中央」の形で、被弾の瞬間に最も強く揺れる今の手触りと合わないため。
/// Impulseの揺れは1方向なので、B4のX/Yで周期をずらした揺れの代わりに、毎回方向をランダムに変えて単調さを避ける。
/// </summary>
public class CameraShakeView : MonoBehaviour
{
    // --- 演出定数（振れ幅はワールド単位。z=0の面での揺れの最大量） ---
    private const float DamageAmplitude = 0.15f;
    private const float HeavyDamageAmplitude = 0.35f;
    private const float GuardAmplitude = 0.06f;
    private const float ShakeDuration = 0.3f;
    private const float HeavyShakeDuration = 0.45f;
    private const float BossRoarAmplitude = 0.4f;
    private const float BossRoarDuration = 0.6f;
    private const float BossDefeatAmplitude = 0.5f;
    private const float BossDefeatDuration = 0.8f;
    private const float ShakeFrequency = 37f;       // 角周波数（rad/s）。B4のX方向と同じ
    private const int CurveSamplesPerSecond = 120;  // 1周期（約0.17秒）あたり約20点

    [Tooltip("揺れの発生源。Impulse TypeはUniformにしておく（Shapeと長さはこのViewが毎回書き換える）")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    private CancellationTokenSource _delayCts;

    // 長さごとの波形。揺れの長さは数種類しかないので、作ったものを使い回す
    private readonly Dictionary<float, AnimationCurve> _curves = new();

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
    /// 揺れを発生させる。連続で呼ばれたら前の揺れ（待機中のものも含む）を止めて揺れ直す。
    /// delayはゲーム内時間で待つ（ポーズ中は進まない。ボス登場の時間表と揃えるため）。
    /// </summary>
    private void Shake(float amplitude, float duration, float delay = 0f)
    {
        CancelDelay();

        if (delay <= 0f)
        {
            Emit(amplitude, duration);
            return;
        }

        _delayCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        EmitAfterDelayAsync(amplitude, duration, delay, _delayCts.Token).Forget();
    }

    private async UniTaskVoid EmitAfterDelayAsync(float amplitude, float duration, float delay, CancellationToken token)
    {
        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token)
            .SuppressCancellationThrow();
        if (canceled) return;

        Emit(amplitude, duration);
    }

    /// <summary>
    /// 前の揺れを消してから、ランダムなXY方向へ揺らす。
    /// Impulseは重ねると足し合わされるため、今までの「上書き」の挙動に揃えて先にClearする。
    /// 発生中の揺れは定義（波形・長さ）を毎フレーム読み直すので、書き換えはClearの後に行う。
    /// </summary>
    private void Emit(float amplitude, float duration)
    {
        CinemachineImpulseManager.Instance.Clear();

        var definition = _impulseSource.ImpulseDefinition;
        definition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Custom;
        definition.CustomImpulseShape = GetCurve(duration);
        definition.ImpulseDuration = duration;
        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
        if (direction == Vector2.zero) direction = Vector2.right;
        _impulseSource.GenerateImpulseWithVelocity(new Vector3(direction.x, direction.y, 0f) * amplitude);
    }

    /// <summary>
    /// B4の揺れの式 sin(経過秒 × 角周波数) × (1 - 進み具合) をカーブにする。横軸は0〜1（長さで引き伸ばされる）。
    /// 接線も式の微分で与え、点と点の間が滑らかにつながるようにする。
    /// </summary>
    private AnimationCurve GetCurve(float duration)
    {
        if (_curves.TryGetValue(duration, out var cached)) return cached;

        int count = Mathf.Max(2, Mathf.CeilToInt(duration * CurveSamplesPerSecond)) + 1;
        var keys = new Keyframe[count];
        float omega = duration * ShakeFrequency;   // 横軸0〜1あたりの角周波数
        for (int i = 0; i < count; i++)
        {
            float u = (float)i / (count - 1);
            float value = Mathf.Sin(u * omega) * (1f - u);
            float slope = omega * Mathf.Cos(u * omega) * (1f - u) - Mathf.Sin(u * omega);
            keys[i] = new Keyframe(u, value, slope, slope);
        }

        var curve = new AnimationCurve(keys);
        _curves[duration] = curve;
        return curve;
    }

    private void CancelDelay()
    {
        if (_delayCts == null) return;
        _delayCts.Cancel();
        _delayCts.Dispose();
        _delayCts = null;
    }

    private void OnDestroy()
    {
        CancelDelay();
    }
}
