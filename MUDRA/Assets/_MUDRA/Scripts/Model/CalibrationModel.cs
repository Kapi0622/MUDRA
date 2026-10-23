using System;
using R3;

/// <summary>
/// Calibration画面で「先へ進めるか」を判定する（B6）。
///
/// 進める条件は「カメラが動いていて、この画面を開いてから手が一度でも映った」こと。
/// 認識テスト（印を組んでみる）は任意で、条件に入れていない。苦手な印で先へ進めなくなるのを避けるため。
/// 2回目以降のプレイでも毎回通るが、手をかざせばすぐ進める。
/// </summary>
public class CalibrationModel : IDisposable
{
    private readonly ReactiveProperty<bool> _hasSeenHand = new(false);
    /// <summary>Beginしてから手が一度でも映ったか</summary>
    public ReadOnlyReactiveProperty<bool> HasSeenHand => _hasSeenHand;

    private readonly ReadOnlyReactiveProperty<bool> _canProceed;
    /// <summary>「次へ」を押せるか。カメラが Running の間だけ。途中でカメラが止まったらfalseに戻る</summary>
    public ReadOnlyReactiveProperty<bool> CanProceed => _canProceed;

    private readonly IDisposable _handSubscription;
    private bool _isActive;

    public CalibrationModel(CameraStatusModel cameraStatus, HandPresenceModel handPresence)
    {
        _handSubscription = handPresence.IsHandVisible
            .Where(isVisible => isVisible && _isActive)
            .Subscribe(_ => _hasSeenHand.Value = true);

        _canProceed = cameraStatus.Status
            .CombineLatest(_hasSeenHand, (status, hasSeen) => status == CameraStatus.Running && hasSeen)
            .ToReadOnlyReactiveProperty();
    }

    /// <summary>
    /// 画面を開いた時に呼ぶ。手が映ったかを数え直す。
    /// 既に手が映っている状態で開いた場合は、次に IsHandVisible が通知されるまで（映り直すまで）待たずに済むよう、今の値を渡す
    /// </summary>
    public void Begin(bool isHandVisibleNow)
    {
        _isActive = true;
        _hasSeenHand.Value = isHandVisibleNow;
    }

    /// <summary>画面を閉じた時に呼ぶ</summary>
    public void End()
    {
        _isActive = false;
    }

    public void Dispose()
    {
        _handSubscription.Dispose();
        _canProceed.Dispose();
        _hasSeenHand.Dispose();
    }
}
