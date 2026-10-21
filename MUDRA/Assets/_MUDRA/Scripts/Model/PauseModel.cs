using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

/// <summary>
/// バトル中のポーズを管理する（B5）。バトルの間だけ生存する。
///
/// 止める仕組みはtimeScale = 0（TimeScaleViewが反映する）。敵の行動ループ・セクション遷移・StatusEffect・
/// ガード受付窓・詠唱のタイムアウトは全てゲーム内時間で進むため、それだけで止まる。
/// 止まらないのは印の入力なので、HandSignPresenterがAcceptsBattleInputを見て入口で弾く。
///
/// 再開はすぐには戻さず、カウントダウンを挟む。カメラで遊ぶゲームではポーズ中に手を下ろしていることが多く、
/// 構え直す時間が無いと再開した瞬間に被弾するため。カウントダウンは実時間で数える。
/// </summary>
public class PauseModel : IDisposable
{
    /// <summary>手が映らない状態がこの秒数続いたら自動でポーズする</summary>
    private const float HandLostSecondsToPause = 5f;

    private readonly ReactiveProperty<bool> _isPaused = new(false);
    /// <summary>ポーズ中か。再開のカウントダウン中もtrueのまま</summary>
    public ReadOnlyReactiveProperty<bool> IsPaused => _isPaused;

    private readonly ReactiveProperty<bool> _isResuming = new(false);
    /// <summary>再開のカウントダウン中か</summary>
    public ReadOnlyReactiveProperty<bool> IsResuming => _isResuming;

    /// <summary>直近のポーズの理由</summary>
    public PauseReason Reason { get; private set; }

    /// <summary>自動ポーズ（フォーカス喪失・手を見失った時）を行うか。設定画面から切り替える</summary>
    public bool IsAutoPauseEnabled { get; set; } = true;

    /// <summary>再開のカウントダウンの長さ（秒）</summary>
    public float ResumeCountdownSeconds { get; }

    /// <summary>
    /// バトルの印を受け付けるか。開始前（遷移・開始演出の間）・ポーズ中・決着後は受け付けない
    /// </summary>
    public bool AcceptsBattleInput => _isBattleActive && !_isPaused.Value;

    private bool _isBattleActive;
    private float _handLostElapsed;
    private CancellationTokenSource _resumeCts;

    public PauseModel(float resumeCountdownSeconds)
    {
        ResumeCountdownSeconds = resumeCountdownSeconds;
    }

    /// <summary>
    /// バトルの進行中かどうか。進行中だけポーズできる。
    /// 決着後にポーズできると、Result画面へ移るまでの待機が止まってしまうため。
    /// </summary>
    public void SetBattleActive(bool isActive)
    {
        _isBattleActive = isActive;
        _handLostElapsed = 0f;
    }

    /// <summary>ポーズする。再開のカウントダウン中なら、カウントダウンを取り消してポーズに戻す</summary>
    /// <returns>ポーズした（またはポーズに戻した）ならtrue</returns>
    public bool RequestPause(PauseReason reason)
    {
        if (!_isBattleActive) return false;
        if (reason != PauseReason.Manual && !IsAutoPauseEnabled) return false;

        if (_isResuming.Value)
        {
            CancelResume();
            Reason = reason;
            _isResuming.Value = false;
            return true;
        }

        if (_isPaused.Value) return false;

        Reason = reason;
        _isPaused.Value = true;
        return true;
    }

    /// <summary>ポーズ中なら再開（カウントダウン開始）、そうでなければポーズする。Escキー用</summary>
    public void TogglePause()
    {
        if (_isPaused.Value && !_isResuming.Value)
            Resume();
        else
            RequestPause(PauseReason.Manual);
    }

    /// <summary>カウントダウンを経て再開する</summary>
    public void Resume()
    {
        if (!_isPaused.Value || _isResuming.Value) return;

        CancelResume();
        _resumeCts = new CancellationTokenSource();
        ResumeAsync(_resumeCts.Token).Forget();
    }

    private async UniTaskVoid ResumeAsync(CancellationToken token)
    {
        _isResuming.Value = true;
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(ResumeCountdownSeconds), ignoreTimeScale: true, cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _handLostElapsed = 0f;
        // IsPausedを先に下ろす。IsResumingを先に下ろすと、購読側から「カウントダウンが取り消されてポーズに戻った」と見分けが付かない
        _isPaused.Value = false;
        _isResuming.Value = false;
    }

    /// <summary>
    /// 手が映っているかを毎フレーム渡す（実時間）。映らない状態が続いたら自動でポーズする。
    /// キーボードモードでは呼ばない。
    /// </summary>
    public void TickHandPresence(float unscaledDeltaTime, bool isHandDetected)
    {
        if (!_isBattleActive || _isPaused.Value || isHandDetected)
        {
            _handLostElapsed = 0f;
            return;
        }

        _handLostElapsed += unscaledDeltaTime;
        if (_handLostElapsed >= HandLostSecondsToPause)
        {
            _handLostElapsed = 0f;
            RequestPause(PauseReason.HandLost);
        }
    }

    private void CancelResume()
    {
        _resumeCts?.Cancel();
        _resumeCts?.Dispose();
        _resumeCts = null;
    }

    public void Dispose()
    {
        CancelResume();
        _isPaused.Dispose();
        _isResuming.Dispose();
    }
}
