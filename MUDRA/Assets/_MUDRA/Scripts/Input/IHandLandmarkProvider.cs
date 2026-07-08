using System.Collections.Generic;

namespace MUDRA.HandTracking
{
    /// <summary>
    /// ランドマーク座標を提供するインターフェース。
    /// PC版はMediaPipeUnityPlugin実装、将来のモック差し替えにも対応可能。
    /// </summary>
    public interface IHandLandmarkProvider
    {
        /// <summary>
        /// 現在キャッシュされているランドマーク座標一覧を返す。
        /// 検出なしの場合は空リスト。
        /// 複数手対応時は最初の手のみを返す暫定仕様（Union判定実装時に拡張予定）。
        /// </summary>
        IReadOnlyList<HandLandmark> GetCurrentLandmarks();

        /// <summary>
        /// 現在検出されている手の数（0, 1, 2）。
        /// </summary>
        int DetectedHandCount { get; }
    }
}