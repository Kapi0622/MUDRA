/// <summary>ポーズした理由（B5）。ポーズメニューの説明文の出し分けに使う</summary>
public enum PauseReason
{
    /// <summary>Escキーやポーズボタン</summary>
    Manual,

    /// <summary>ウィンドウのフォーカスを失った</summary>
    FocusLost,

    /// <summary>カメラに手が映らない時間が続いた</summary>
    HandLost,
}
