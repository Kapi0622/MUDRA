using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace MUDRA.HandTracking
{
    /// <summary>
    /// ランドマーク座標から指の関節角度を算出し、手印を識別する。
    /// 安定判定（Nフレーム連続で同一手印を確認）までを責務範囲とする。
    /// </summary>
    public sealed class HandTrackingService : IDisposable
    {
        // Union判定の手首間距離しきい値（手のひら長基準の比率）
        // 実機テストで調整する前提の仮値
        private const float UnionWristDistanceRatio = 1.5f;

        // 手首・中指付け根のランドマークインデックス
        private const int WristIndex = 0;
        private const int MiddleFingerMcpIndex = 9;
        
        private readonly IHandLandmarkProvider _provider;
        private readonly float _bentThreshold;
        private readonly float _thumbBentThreshold;
        private readonly int _stableFrameCount;

        // 安定判定用の内部状態
        private HandSign? _lastSign;
        private int _stableCount;
        private bool _isConfirmed;

        /// <summary>
        /// 手印が確定した際に発火する。
        /// </summary>
        private readonly Subject<HandSign> _onHandSignRecognized = new(); 
        
        public Observable<HandSign> OnHandSignRecognized => _onHandSignRecognized;

        public HandTrackingService(
            IHandLandmarkProvider provider,
            float bentThreshold = 45f,
            float thumbBentThreshold = 20f,
            int stableFrameCount = 36)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _bentThreshold = bentThreshold;
            _thumbBentThreshold = thumbBentThreshold;
            _stableFrameCount = stableFrameCount;
        }

        /// <summary>
        /// 駆動役（Presenter）から毎フレーム呼ばれる。
        /// Providerから最新座標を取得 → 識別 → 安定判定 の順に処理する。
        /// </summary>
        public void Tick()
        {
            var hand0 = _provider.GetLandmarks(0);
            var isHandDetected = _provider.DetectedHandCount > 0 && hand0.Count > 0;

            HandSign? currentSign;

            // Union判定：2手検出かつ手首間距離が閾値以下
            if (_provider.DetectedHandCount >= 2)
            {
                var hand1 = _provider.GetLandmarks(1);
                if (hand1.Count > 0 && IsUnionPose(hand0, hand1))
                {
                    currentSign = HandSign.Union;
                }
                else
                {
                    // 2手見えているがUnion条件未達 → 判定保留
                    // （両手検出中は片手印を抑制し、両手印の判定を優先する）
                    currentSign = null;
                    
                    // 片手判定を優先させたいとき用
                    // 2手見えているがUnion条件未達 → 1手目で通常判定
                    // currentSign = isHandDetected ? DetectSign(hand0) : null;
                }
            }
            else
            {
                currentSign = isHandDetected ? DetectSign(hand0) : null;
            }

            JudgeStability(currentSign);
        }

        /// <summary>
        /// 5本指の曲げ状態から手印を識別する。
        /// 親指はCMC(1)-MCP(2)-IP(3)のなす角で判定（他4指より閾値が低い）。
        /// 他4指はMCP-PIP-DIPのなす角で判定する。
        /// </summary>
        private HandSign? DetectSign(IReadOnlyList<HandLandmark> landmarks)
        {
            // --- 親指: CMC(1)-MCP(2)-IP(3) のなす角で判定 ---
            // 検証結果（A3 ThumbAngleDebugger）:
            //   Open時 10°前後 / Fist時 26°前後 / Palm時 38°前後
            //   → 閾値20°で14°以上のマージンを確保
            var thumbBent = IsFingerBent(landmarks, mcpIndex: 1, pipIndex: 2, dipIndex: 3,
                threshold: _thumbBentThreshold);
            
            // --- 他4指: MCP-PIP-DIP のなす角で判定 ---
            var indexBent = IsFingerBent(landmarks, mcpIndex: 5, pipIndex: 6, dipIndex: 7);
            var middleBent = IsFingerBent(landmarks, mcpIndex: 9, pipIndex: 10, dipIndex: 11);
            var ringBent = IsFingerBent(landmarks, mcpIndex: 13, pipIndex: 14, dipIndex: 15);
            var pinkyBent = IsFingerBent(landmarks, mcpIndex: 17, pipIndex: 18, dipIndex: 19);
            
            // --- 5指パターンで手印を識別 ---
            // パターン表（O=伸び, X=曲げ）:
            //   Open     : [O, O, O, O, O] 全指伸び
            //   Fist     : [X, X, X, X, X] 全指曲げ
            //   Point    : [X, O, X, X, X] 人差し指のみ
            //   Scissors : [X, O, O, X, X] 人差し指+中指
            //   Palm     : [X, O, O, O, O] 親指のみ曲げ
            //   Release  : [X, O, X, X, O] 親指曲げ+人差し指+小指
            //   Cancel   : [X, X, X, X, O] 小指のみ
            //   Guard    : [O, X, X, X, X] 親指のみ
            
            // Open: 全指が伸びている（親指含む）
            if (!thumbBent && !indexBent && !middleBent && !ringBent && !pinkyBent)
                return HandSign.Open;

            // Fist: 全指が曲がっている
            if (thumbBent && indexBent && middleBent && ringBent && pinkyBent)
                return HandSign.Fist;
            
            // Guard: 親指だけ伸びている
            if (!thumbBent && indexBent && middleBent && ringBent && pinkyBent)
                return HandSign.Guard;

            // Point: 人差し指だけ伸びている
            if (thumbBent && !indexBent && middleBent && ringBent && pinkyBent)
                return HandSign.Point;

            // Scissors: 人差し指+中指が伸びている
            if (thumbBent && !indexBent && !middleBent && ringBent && pinkyBent)
                return HandSign.Scissors;

            // Palm: 親指だけ曲がっている（他4指はすべて伸び）
            if (thumbBent && !indexBent && !middleBent && !ringBent && !pinkyBent)
                return HandSign.Palm;

            // Release: 親指曲げ+人差し指+小指が伸び、中指+薬指は曲げ
            if (thumbBent && !indexBent && middleBent && ringBent && !pinkyBent)
                return HandSign.Release;

            // Cancel: 小指だけ伸びている
            if (thumbBent && indexBent && middleBent && ringBent && !pinkyBent)
                return HandSign.Cancel;

            return null;
        }
        
        /// <summary>
        /// 指1本の曲げ状態を判定する。
        /// 3点のなす角が閾値を超えていれば「曲がっている」と判定する。
        /// 親指は関節構造が異なるため、呼び出し側で適切なインデックスと閾値を渡す。
        /// </summary>
        private bool IsFingerBent(
            IReadOnlyList<HandLandmark> landmarks,
            int mcpIndex,
            int pipIndex,
            int dipIndex,
            float? threshold = null)
        {
            var mcp = landmarks[mcpIndex].Position;
            var pip = landmarks[pipIndex].Position;
            var dip = landmarks[dipIndex].Position;

            var vectorA = pip - mcp;
            var vectorB = dip - pip;
            var angle = Vector3.Angle(vectorA, vectorB);

            return angle > (threshold ?? _bentThreshold);
        }

        /// <summary>
        /// Nフレーム連続で同一手印を検出したら確定させる。
        /// 「手なし・未知」は安定判定の対象外（早期return）。
        /// 一度確定した手印は、別の手印に変わるまで再確定しない（_isConfirmedでラッチ）。
        /// </summary>
        private void JudgeStability(HandSign? currentSign)
        {
            if (currentSign is null)
            {
                // 状態には一切触れず、比較・カウントの対象から除外するだけ
                // 手が一瞬消えても、直前の確定状態を保持したまま継続する
                return;
            }

            if (currentSign != _lastSign)
            {
                _lastSign = currentSign;
                _stableCount = 1;
                _isConfirmed = false;
                return;
            }

            if (_isConfirmed)
            {
                return;
            }

            _stableCount++;
            if (_stableCount >= _stableFrameCount)
            {
                _isConfirmed = true;
                _onHandSignRecognized.OnNext(currentSign.Value);
            }
        }
        
        /// <summary>
        /// 両手の手首間距離が、1手目の手のひら長を基準にした比率で
        /// 閾値以下であればtrueを返す（合掌判定）。
        /// </summary>
        private bool IsUnionPose(
            IReadOnlyList<HandLandmark> hand0,
            IReadOnlyList<HandLandmark> hand1)
        {
            // 1手目の手のひら長（手首→中指付け根）を基準長として取得
            var palmLength = Vector3.Distance(
                hand0[WristIndex].Position,
                hand0[MiddleFingerMcpIndex].Position);

            // 基準長が極端に小さい場合は判定不能（ゼロ除算防止）
            if (palmLength < 0.001f) return false;

            // 両手の手首間距離を算出
            var wristDistance = Vector3.Distance(
                hand0[WristIndex].Position,
                hand1[WristIndex].Position);

            return wristDistance / palmLength <= UnionWristDistanceRatio;
        }

        public void Dispose()
        {
            _onHandSignRecognized.Dispose();
        }
    }
}