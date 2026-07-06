using System;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 詠唱印・特殊印の種類（親指を除く4指の曲げパターンで識別）
/// </summary>
public enum HandSign
{
    Open,
    Fist,
    Point,
    Release,
    Cancel,
}

public class HandSignDetector : MonoBehaviour
{
    [SerializeField] private Text _handSignText;

    // 指が「曲がっている」と判定する角度の閾値
    private const float BentThreshold = 45f;

    // 安定判定に必要な連続フレーム数（60FPS想定で0.5秒）
    private const int RequiredStableFrames = 30;

    // バックグラウンドスレッドから書き込まれる受け皿
    private HandLandmarkerResult _pendingResult;
    private volatile bool _hasNewResult;

    // 安定判定用の状態（すべてメインスレッドからのみ触れる）
    private HandSign? _lastSign;
    private int _stableFrameCount;
    private bool _isConfirmed;

    /// <summary>
    /// 手印が確定した瞬間に発火する（メインスレッドから呼ばれる）
    /// </summary>
    public event Action<HandSign> OnSignConfirmed;

    private void Start()
    {
        HandLandmarkerRunner.OnLandmarkDetected += OnLandmarkDetected;
    }

    private void OnDestroy()
    {
        HandLandmarkerRunner.OnLandmarkDetected -= OnLandmarkDetected;
    }

    // バックグラウンドスレッドから呼ばれる → 結果を保存するだけ
    private void OnLandmarkDetected(HandLandmarkerResult result)
    {
        _pendingResult = result;
        _hasNewResult = true;
    }

    // メインスレッドで毎フレーム処理
    private void Update()
    {
        if (!_hasNewResult) return;
        _hasNewResult = false;
        ProcessResult(_pendingResult);
    }

    private void ProcessResult(HandLandmarkerResult result)
    {
        bool isHandDetected = result.handLandmarks != null && result.handLandmarks.Count > 0;
        HandSign? currentSign = null;

        if (result.handLandmarks != null && result.handLandmarks.Count > 0)
        {
            var landmarks = result.handLandmarks[0].landmarks;

            bool indexBent = IsFingerBent(landmarks[5], landmarks[6], landmarks[7], landmarks[8]);
            bool middleBent = IsFingerBent(landmarks[9], landmarks[10], landmarks[11], landmarks[12]);
            bool ringBent = IsFingerBent(landmarks[13], landmarks[14], landmarks[15], landmarks[16]);
            bool pinkyBent = IsFingerBent(landmarks[17], landmarks[18], landmarks[19], landmarks[20]);

            currentSign = DetectSign(indexBent, middleBent, ringBent, pinkyBent);
        }

        UpdateText(isHandDetected, currentSign);
        JudgeStability(currentSign);
    }

    /// <summary>
    /// MCP・PIP・DIP・TIPの4点から指の曲げ状態を判定する
    /// </summary>
    private bool IsFingerBent(
        Mediapipe.Tasks.Components.Containers.NormalizedLandmark mcp,
        Mediapipe.Tasks.Components.Containers.NormalizedLandmark pip,
        Mediapipe.Tasks.Components.Containers.NormalizedLandmark dip,
        Mediapipe.Tasks.Components.Containers.NormalizedLandmark tip)
    {
        var v1 = new Vector3(pip.x - mcp.x, pip.y - mcp.y, pip.z - mcp.z);
        var v2 = new Vector3(dip.x - pip.x, dip.y - pip.y, dip.z - pip.z);
        return Vector3.Angle(v1, v2) > BentThreshold;
    }

    /// <summary>
    /// 4指の曲げ状態から手印を識別する（親指は今回除外）
    /// 一致するパターンがなければ null（＝無）を返す
    /// </summary>
    private HandSign? DetectSign(bool indexBent, bool middleBent, bool ringBent, bool pinkyBent)
    {
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
    /// 安定判定：同一手印がRequiredStableFrames続いたら確定イベントを発火する
    /// 手印が変わるまでは再確定させない
    /// </summary>
    private void JudgeStability(HandSign? currentSign)
    {
        // 「無」は比較にもカウントにも一切関与させない
        if (currentSign == null) return;

        if (currentSign != _lastSign)
        {
            _stableFrameCount = 0;
            _isConfirmed = false;
            _lastSign = currentSign;
        }
        else
        {
            _stableFrameCount++;
        }

        if (_stableFrameCount >= RequiredStableFrames && !_isConfirmed)
        {
            _isConfirmed = true;
            OnSignConfirmed?.Invoke(currentSign.Value);
        }
    }

    private void UpdateText(bool isHandDetected, HandSign? sign)
    {
        string display = !isHandDetected
            ? "No Hand"
            : sign?.ToString() ?? "Unknown";

        if (_handSignText != null)
            _handSignText.text = display;

        Debug.Log($"[HandSign] {display}");
    }
}