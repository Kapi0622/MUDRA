/// <summary>カメラ（Webカメラ＋MediaPipeの推論）の状態（B6）。Calibration画面のエラー表示の出し分けに使う</summary>
public enum CameraStatus
{
    /// <summary>起動中。まだ推論結果が1つも届いていない</summary>
    Starting,

    /// <summary>推論結果が届いている。手が映っているかどうかは問わない</summary>
    Running,

    /// <summary>カメラが1台も接続されていない</summary>
    NoDevice,

    /// <summary>カメラはあるが推論結果が届かない（起動に失敗した・他のアプリが使っている・途中で抜かれた等）</summary>
    NotResponding,
}
