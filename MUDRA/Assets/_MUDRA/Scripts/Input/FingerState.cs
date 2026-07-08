using UnityEngine;

namespace MUDRA.HandTracking
{
    /// <summary>
    /// 指1本分の曲げ状態。
    /// </summary>
    public readonly struct FingerState
    {
        public bool IsBent { get; }
        public float Angle { get; }

        public FingerState(bool isBent, float angle)
        {
            IsBent = isBent;
            Angle = angle;
        }
    }
}
