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
        private readonly IHandLandmarkProvider _provider;
        private readonly float _bentThreshold;
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
            int stableFrameCount = 24)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _bentThreshold = bentThreshold;
            _stableFrameCount = stableFrameCount;
        }

        /// <summary>
        /// 駆動役（Presenter）から毎フレーム呼ばれる。
        /// Providerから最新座標を取得 → 識別 → 安定判定 の順に処理する。
        /// </summary>
        public void Tick()
        {
            var landmarks = _provider.GetCurrentLandmarks();
            var isHandDetected = _provider.DetectedHandCount > 0 && landmarks.Count > 0;

            var currentSign = isHandDetected ? DetectSign(landmarks) : (HandSign?)null;
            JudgeStability(currentSign);
        }

        /// <summary>
        /// 5本指の曲げ状態から手印を識別する。
        /// A1時点ではOpen / Fist / Pointの3種に対応。親指判定はA3で追加予定。
        /// </summary>
        private HandSign? DetectSign(IReadOnlyList<HandLandmark> landmarks)
        {
            // 各指のMCP・PIP・DIP・TIPのインデックスは仕様書5-1の対応表通り
            var indexBent = IsFingerBent(landmarks, mcpIndex: 5, pipIndex: 6, dipIndex: 7);
            var middleBent = IsFingerBent(landmarks, mcpIndex: 9, pipIndex: 10, dipIndex: 11);
            var ringBent = IsFingerBent(landmarks, mcpIndex: 13, pipIndex: 14, dipIndex: 15);
            var pinkyBent = IsFingerBent(landmarks, mcpIndex: 17, pipIndex: 18, dipIndex: 19);
            
            // Open: 全指が伸びている
            if (!indexBent && !middleBent && !ringBent && !pinkyBent)
                return HandSign.Open;

            // Fist: 全指が曲がっている
            if (indexBent && middleBent && ringBent && pinkyBent)
                return HandSign.Fist;

            // Point: 人差し指だけ伸びている
            if (!indexBent && middleBent && ringBent && pinkyBent)
                return HandSign.Point;

            // Release（発動印, 仮）: 人差し指・小指が伸び、中指・薬指が曲がっている
            if (!indexBent && middleBent && ringBent && !pinkyBent)
                return HandSign.Release;

            // Cancel（解除印, 仮）: 小指だけ伸びている
            if (indexBent && middleBent && ringBent && !pinkyBent)
                return HandSign.Cancel;

            return null;
        }
        
        /// <summary>
        /// 指1本の曲げ状態を判定する
        /// MCP→PIPのベクトルとPIP→DIPのベクトルのなす角で判定する
        /// </summary>
        private bool IsFingerBent(IReadOnlyList<HandLandmark> landmarks, int mcpIndex, int pipIndex, int dipIndex)
        {
            var mcp = landmarks[mcpIndex].Position;
            var pip = landmarks[pipIndex].Position;
            var dip = landmarks[dipIndex].Position;

            var vectorA = pip - mcp;
            var vectorB = dip - pip;
            var angle = Vector3.Angle(vectorA, vectorB);

            return angle > _bentThreshold;
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

        public void Dispose()
        {
            _onHandSignRecognized.Dispose();
        }
    }
}