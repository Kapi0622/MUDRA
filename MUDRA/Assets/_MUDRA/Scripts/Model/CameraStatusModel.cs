using System;
using R3;

/// <summary>
/// カメラが動いているかを、推論結果が届き続けているかで判定する（B6）。
///
/// MediaPipeは手が映っていないフレームでも（空の）結果を返すので、推論結果の番号（IHandLandmarkProvider.ResultVersion）は
/// カメラと推論が動いている限り増え続ける。番号が止まったら、手の有無ではなくカメラ側の問題と判断できる。
///
/// カメラの台数はPresenterが調べて Reset で渡す（Unity APIをModelに入れないため）。
/// 台数の列挙は軽くないので毎フレームは調べず、起動時と再試行の時だけにしている。
/// </summary>
public class CameraStatusModel : IDisposable
{
    /// <summary>
    /// 起動からこの秒数経っても推論結果が届かなければ NotResponding にする。
    /// カメラの起動とMediaPipeのモデル読み込みで数秒かかるため長めにとっている
    /// </summary>
    private const float StartupTimeoutSeconds = 15f;

    /// <summary>動き出した後、この秒数推論結果が届かなければ NotResponding にする（推論は約35回/秒）</summary>
    private const float StallSeconds = 3f;

    private readonly ReactiveProperty<CameraStatus> _status = new(CameraStatus.Starting);
    /// <summary>現在のカメラの状態</summary>
    public ReadOnlyReactiveProperty<CameraStatus> Status => _status;

    private int _lastResultVersion;
    private float _elapsedSinceResult;
    private bool _hasReceivedResult;

    /// <summary>
    /// 判定を最初からやり直す。起動時と、カメラを再起動した（再試行）時に呼ぶ。
    /// </summary>
    /// <param name="deviceCount">接続されているカメラの台数。0なら NoDevice のまま、再試行まで判定しない</param>
    /// <param name="currentResultVersion">今の推論結果の番号。これより後に届いた結果だけを数える</param>
    public void Reset(int deviceCount, int currentResultVersion)
    {
        _lastResultVersion = currentResultVersion;
        _elapsedSinceResult = 0f;
        _hasReceivedResult = false;
        _status.Value = deviceCount > 0 ? CameraStatus.Starting : CameraStatus.NoDevice;
    }

    /// <summary>毎フレーム呼ぶ。ポーズ中も数えるため実時間を渡す</summary>
    public void Tick(float unscaledDeltaTime, int resultVersion)
    {
        if (_status.Value == CameraStatus.NoDevice) return;

        if (resultVersion != _lastResultVersion)
        {
            // 届かなくなった後に再び届いた（一時的な詰まりから戻った）場合も Running に戻す
            _lastResultVersion = resultVersion;
            _elapsedSinceResult = 0f;
            _hasReceivedResult = true;
            _status.Value = CameraStatus.Running;
            return;
        }

        _elapsedSinceResult += unscaledDeltaTime;

        float timeout = _hasReceivedResult ? StallSeconds : StartupTimeoutSeconds;
        if (_elapsedSinceResult >= timeout)
            _status.Value = CameraStatus.NotResponding;
    }

    public void Dispose()
    {
        _status.Dispose();
    }
}
