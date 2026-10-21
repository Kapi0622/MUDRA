/// <summary>
/// シーン名。App（常駐）にInGameを加算ロードする構成（B5）で、両方のシーンから参照する。
/// Build Settingsに登録した名前と一致させること。
/// </summary>
public static class SceneNames
{
    /// <summary>常駐シーン。起動シーンで、Title / StageSelect / Result の画面とカメラ（MediaPipe）を持つ</summary>
    public const string App = "App";

    /// <summary>バトルシーン。バトルのたびに加算ロードし、終わったらアンロードする</summary>
    public const string InGame = "InGame";
}
