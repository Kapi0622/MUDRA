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
        
        /// <summary>
        /// 指定した手が左手かどうかを返す。
        /// 該当する手が検出されていない場合はnullを返す。
        /// </summary>
        bool? IsLeftHand(int handIndex);

        /// <summary>
        /// 推論結果を受け取るたびに1ずつ増える番号。
        /// 判定側は毎描画フレーム呼ばれるが、推論結果の更新はそれより粗い（約35回/秒）。
        /// この値の変化で「新しい推論結果が届いたか」を見分け、手の速さなど
        /// 推論間の差分を使う計算で、同じ古い結果を重複して扱わないようにする。
        /// </summary>
        int ResultVersion { get; }
    }
}