/// <summary>
/// ゲーム全体の画面フェーズ（仕様書 §4-1）。
/// B6でCalibration / Tutorialを追加する。シリアライズしていないので並べ替えても壊れないが、追加は末尾に揃える。
/// </summary>
public enum GamePhase
{
    Title,
    StageSelect,
    InGame,
    Result,
}
