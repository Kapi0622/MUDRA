using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

namespace MUDRA.HandTracking
{
    /// <summary>
    /// MediaPipeUnityPluginを経由してランドマーク座標を提供するProvider。
    /// HandLandmarkerRunnerからstatic event経由で検出結果を受信し、
    /// メインスレッドに切り替えて内部キャッシュを更新する。
    /// </summary>
    public sealed class MediaPipeHandLandmarkProvider : MonoBehaviour, IHandLandmarkProvider
    {
        // 21点分の容量を確保して再確保コストを避ける
        // 2手分のキャッシュを配列で保持（各21点分の容量を確保）
        private readonly List<HandLandmark>[] _cachedLandmarks = new[]
        {
            new List<HandLandmark>(21),
            new List<HandLandmark>(21)
        };
        
        // 範囲外アクセス時に返す空リスト（毎回newを避ける）
        private static readonly IReadOnlyList<HandLandmark> EmptyLandmarks 
            = Array.Empty<HandLandmark>();
        
        private int _detectedHandCount;

        public int DetectedHandCount => _detectedHandCount;
        
        // 各手の左右情報（true = 左手、null = 未検出）
        private readonly bool?[] _cachedIsLeftHand = new bool?[2];

        public IReadOnlyList<HandLandmark> GetLandmarks(int handIndex)
        {
            // 範囲外または未検出の手には空リストを返す
            if (handIndex < 0 || handIndex >= _cachedLandmarks.Length)
                return EmptyLandmarks;

            return _cachedLandmarks[handIndex];
        }
        
        public bool? IsLeftHand(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _cachedIsLeftHand.Length)
                return null;

            return _cachedIsLeftHand[handIndex];
        }

        private void OnEnable()
        {
            HandLandmarkerRunner.OnLandmarkDetected += HandleLandmarkDetected;
        }

        private void OnDisable()
        {
            HandLandmarkerRunner.OnLandmarkDetected -= HandleLandmarkDetected;
        }

        /// <summary>
        /// MediaPipeからバックグラウンドスレッドで呼ばれる。
        /// 直接キャッシュを触らず、UniTaskでメインスレッドに切り替える。
        /// </summary>
        private void HandleLandmarkDetected(HandLandmarkerResult result)
        {
            UpdateCacheAsync(result).Forget();
        }

        private async UniTaskVoid UpdateCacheAsync(HandLandmarkerResult result)
        {
            await UniTask.SwitchToMainThread();
            UpdateCache(result);
        }

        private void UpdateCache(HandLandmarkerResult result)
        {
            // 全手のキャッシュをクリア
            for (var h = 0; h < _cachedLandmarks.Length; h++)
            {
                _cachedLandmarks[h].Clear();
                _cachedIsLeftHand[h] = null;
            }

            var hands = result.handLandmarks;
            if (hands == null || hands.Count == 0)
            {
                _detectedHandCount = 0;
                return;
            }

            _detectedHandCount = hands.Count;

            // 検出された手の数（最大2）分だけキャッシュに格納
            var handCount = Mathf.Min(hands.Count, _cachedLandmarks.Length);
            for (var h = 0; h < handCount; h++)
            {
                // ランドマーク座標のキャッシュ
                var landmarks = hands[h].landmarks;
                for (var i = 0; i < landmarks.Count; i++)
                {
                    var l = landmarks[i];
                    _cachedLandmarks[h].Add(new HandLandmark(new Vector3(l.x, l.y, l.z), i));
                }
                
                // 左右情報のキャッシュ
                if (result.handedness != null && h < result.handedness.Count)
                {
                    var categories = result.handedness[h].categories;
                    if (categories != null && categories.Count > 0)
                    {
                        _cachedIsLeftHand[h] = categories[0].categoryName == "Left";
                    }
                }
            }
        }
    }
}