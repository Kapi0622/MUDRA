using MUDRA.HandTracking;
using UnityEngine;

/// <summary>
/// カメラの状態（CameraStatusModel）と手の映り具合（HandPresenceModel）を毎フレーム進める（B6）。App側に常駐する。
///
/// 画面に関係なく常に回すので、Calibration・チュートリアル・バトルのどこでも同じ判定を使える。
/// ポーズ中（timeScale = 0）も数え続けるため、実時間で進める。
/// Unity APIを使う部分（カメラの台数の列挙）はここで行い、Modelには数値だけを渡す。
/// </summary>
public class HandStatusPresenter : MonoBehaviour
{
    private IHandLandmarkProvider _provider;
    private CameraStatusModel _cameraStatus;
    private HandPresenceModel _handPresence;

    /// <summary>AppInitializerから呼ばれる</summary>
    public void Initialize(IHandLandmarkProvider provider, CameraStatusModel cameraStatus, HandPresenceModel handPresence)
    {
        _provider = provider;
        _cameraStatus = cameraStatus;
        _handPresence = handPresence;

        RecheckCamera();
    }

    /// <summary>カメラの台数を調べ直し、カメラの状態の判定を最初からやり直す。起動時と再試行の時に呼ぶ</summary>
    public void RecheckCamera()
    {
        if (_cameraStatus == null) return;
        _cameraStatus.Reset(WebCamTexture.devices.Length, _provider.ResultVersion);
    }

    private void Update()
    {
        if (_provider == null) return;

        float deltaTime = Time.unscaledDeltaTime;
        _cameraStatus.Tick(deltaTime, _provider.ResultVersion);
        _handPresence.Tick(deltaTime, _provider.DetectedHandCount > 0);
    }
}
