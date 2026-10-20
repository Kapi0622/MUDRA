using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Time.timeScaleを書き換える唯一の場所（B5）。
///
/// ポーズ・ヒットストップ・スローの3つが同じtimeScaleを取り合うため、ここで合成して決める。
///   ポーズ中またはヒットストップ中 → 0、それ以外 → スローの倍率（通常は1）
/// ヒットストップとスローは実時間で待ち、終わったら自分の分を下ろすだけにしている。
/// 以前のようにヒットストップの終わりでtimeScaleを1に戻すと、その間にポーズしていても勝手に動き出してしまう。
///
/// InGameに置く。アンロード時は必ず1に戻す（ポーズ中にリタイアしても、App側の時間を止めたままにしない）。
/// </summary>
public class TimeScaleView : MonoBehaviour
{
    private const float NormalScale = 1f;

    private bool _isPaused;
    private bool _isHitStopping;
    private float _slowScale = NormalScale;

    private CancellationTokenSource _effectCts;

    /// <summary>ポーズの開始・解除</summary>
    public void SetPaused(bool isPaused)
    {
        _isPaused = isPaused;
        Apply();
    }

    /// <summary>
    /// ヒットストップ（時間を止める）の後に、スロー（時間を遅くする）を続ける。
    /// どちらも実時間で数える。slowSecondsが0ならヒットストップだけ。
    /// </summary>
    public void PlayHitStop(float hitStopSeconds, float slowScale = NormalScale, float slowSeconds = 0f)
    {
        CancelEffect();
        _effectCts = new CancellationTokenSource();
        HitStopAsync(hitStopSeconds, slowScale, slowSeconds, _effectCts.Token).Forget();
    }

    private async UniTaskVoid HitStopAsync(float hitStopSeconds, float slowScale, float slowSeconds, CancellationToken token)
    {
        try
        {
            _isHitStopping = true;
            Apply();
            await UniTask.Delay(TimeSpan.FromSeconds(hitStopSeconds), ignoreTimeScale: true, cancellationToken: token);

            _isHitStopping = false;
            _slowScale = slowScale;
            Apply();
            if (slowSeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(slowSeconds), ignoreTimeScale: true, cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            // 次の演出に置き換えられた・破棄された。finallyで自分の分を下ろす
        }
        finally
        {
            _isHitStopping = false;
            _slowScale = NormalScale;
            Apply();
        }
    }

    private void Apply()
    {
        Time.timeScale = _isPaused || _isHitStopping ? 0f : _slowScale;
    }

    private void CancelEffect()
    {
        _effectCts?.Cancel();
        _effectCts?.Dispose();
        _effectCts = null;
    }

    private void OnDestroy()
    {
        CancelEffect();
        Time.timeScale = NormalScale;
    }
}
