using UnityEngine;

namespace MUDRA.HandTracking
{
    /// <summary>
    /// ランドマーク座標1点分のデータ。MediaPipeの21点それぞれに対応する。
    /// Position は正規化座標（0.0〜1.0）のまま保持する。
    /// </summary>
    public readonly struct HandLandmark
    {
        public Vector3 Position { get; }
        public int Index { get; }

        public HandLandmark(Vector3 position, int index)
        {
            Position = position;
            Index = index;
        }
    }
}
