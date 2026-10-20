using System;

namespace MUDRA.HandTracking
{
    /// <summary>
    /// 確定前の手印の判定状況。「今どの印として読まれていて、確定までどこまで進んだか」を表す。
    /// 確定するまで何の形として読まれているか分からない問題への対応で、判定中UIの表示に使う。
    ///
    /// ReactivePropertyは値が変わったときだけ通知するため、IEquatableで値として比較できるようにしている。
    /// </summary>
    public readonly struct HandSignCandidate : IEquatable<HandSignCandidate>
    {
        /// <summary>候補なし（手が映っていない・どのパターンにも一致しない・両手検出中の片手印抑制）</summary>
        public static readonly HandSignCandidate None = new(null, 0f);

        /// <summary>候補の印。null は候補なし</summary>
        public readonly HandSign? Sign;

        /// <summary>確定までの進み具合（0〜1）。1は確定済み（同じ印を保持し続けている間も1のまま）</summary>
        public readonly float Progress;

        public HandSignCandidate(HandSign? sign, float progress)
        {
            Sign = sign;
            Progress = progress;
        }

        public bool Equals(HandSignCandidate other) => Sign == other.Sign && Progress.Equals(other.Progress);

        public override bool Equals(object obj) => obj is HandSignCandidate other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Sign, Progress);
    }
}
