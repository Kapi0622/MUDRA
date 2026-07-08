using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mediapipe.Tasks.Vision.HandLandmarker; // ← 実際のnamespaceに合わせて調整
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
        private readonly List<HandLandmark> _cachedLandmarks = new(21);
        private int _detectedHandCount;

        public int DetectedHandCount => _detectedHandCount;

        public IReadOnlyList<HandLandmark> GetCurrentLandmarks() => _cachedLandmarks;

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
            _cachedLandmarks.Clear();

            var hands = result.handLandmarks;
            if (hands == null || hands.Count == 0)
            {
                _detectedHandCount = 0;
                return;
            }

            _detectedHandCount = hands.Count;

            // 現状は最初の手のみを扱う（両手対応はUnion判定実装時に拡張）
            var landmarks = hands[0].landmarks;
            for (var i = 0; i < landmarks.Count; i++)
            {
                var l = landmarks[i];
                _cachedLandmarks.Add(new HandLandmark(new Vector3(l.x, l.y, l.z), i));
            }
        }
    }
}