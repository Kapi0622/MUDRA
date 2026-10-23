/// <summary>
/// ゲーム全体の画面フェーズ（仕様書 §4-1）。
/// シリアライズしていないので並べ替えても壊れないが、追加は末尾に揃える。
/// </summary>
public enum GamePhase
{
    Title,
    StageSelect,
    InGame,
    Result,

    /// <summary>カメラと手の認識の確認（B6）。Titleの後に毎回通る</summary>
    Calibration,

    /// <summary>練習バトル（B6）。InGameシーンを使う。StageSelectの前に毎回通る（スキップ可）</summary>
    Tutorial,
}
