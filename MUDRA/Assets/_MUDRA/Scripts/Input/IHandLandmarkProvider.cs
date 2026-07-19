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
        /// 指定した手のランドマーク座標を返す。
        /// 該当する手が検出されていない場合は空リストを返す。
        /// </summary>
        /// <param name="handIndex">手のインデックス（0: 1手目, 1: 2手目）</param>
        IReadOnlyList<HandLandmark> GetLandmarks(int handIndex);

        /// <summary>
        /// 現在検出されている手の数を返す。
        /// </summary>
        int DetectedHandCount { get; }
    }
}