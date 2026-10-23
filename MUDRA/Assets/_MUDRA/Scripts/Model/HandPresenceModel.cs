using System;
using R3;

/// <summary>
/// カメラに手が映っているか、映らない状態が続いているかを判定する（B6）。
/// App側に1つだけ置き、Calibration画面の「手が認識されません」ガイドと、バトル中の自動ポーズ（PauseModel）で共有する。
/// 「何秒映らなければ見失ったとするか」をここ1か所にまとめるため、B5でPauseModelが持っていた判定を移した。
///
/// IsHandLostは一度trueになると、手が映るか RestartLostTimer を呼ぶまでtrueのまま。
/// 受け手は立ち上がり（false→true）で反応すればよい。
/// </summary>
public class HandPresenceModel : IDisposable
{
    /// <summary>手が映らない状態がこの秒数続いたら「見失った」とする</summary>
    private const float HandLostSeconds = 5f;

    private readonly ReactiveProperty<bool> _isHandVisible = new(false);
    /// <summary>今、手が1本以上映っているか</summary>
    public ReadOnlyReactiveProperty<bool> IsHandVisible => _isHandVisible;

    private readonly ReactiveProperty<bool> _isHandLost = new(false);
    /// <summary>手が映らない状態が HandLostSeconds 続いたか</summary>
    public ReadOnlyReactiveProperty<bool> IsHandLost => _isHandLost;

    private float _lostElapsed;

    /// <summary>毎フレーム呼ぶ。ポーズ中も数えるため実時間を渡す</summary>
    public void Tick(float unscaledDeltaTime, bool isHandDetected)
    {
        _isHandVisible.Value = isHandDetected;

        if (isHandDetected)
        {
            _lostElapsed = 0f;
            _isHandLost.Value = false;
            return;
        }

        if (_isHandLost.Value) return;

        _lostElapsed += unscaledDeltaTime;
        if (_lostElapsed >= HandLostSeconds)
            _isHandLost.Value = true;
    }

    /// <summary>
    /// 見失った判定を取り下げ、今から数え直す。
    /// ポーズから再開した直後に、手がまだ映っていなくてもすぐに再びポーズしないよう、
    /// もう一度 HandLostSeconds 待ってから判定させるために使う（B5のPauseModelと同じ挙動）
    /// </summary>
    public void RestartLostTimer()
    {
        _lostElapsed = 0f;
        _isHandLost.Value = false;
    }

    public void Dispose()
    {
        _isHandVisible.Dispose();
        _isHandLost.Dispose();
    }
}
