using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;
using UnityEngine.UI;

public class HandSignDetector : MonoBehaviour
{
    [SerializeField] private Text _handSignText;
    
    // 指が「曲がっている」と判定する角度の閾値
    private const float BentThreshold = 45f;
    
    // バックグラウンドスレッドから書き込まれる受け皿
    private HandLandmarkerResult _pendingResult;
    private volatile bool _hasNewResult;
    
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
        // 手が未検出の場合は何もしない
        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
        {
            UpdateText("No Hand");
            return;
        }

        // 最初の1手分のランドマーク(21点)を取得
        var landmarks = result.handLandmarks[0].landmarks;

        // 各指の曲げ状態を判定(親指は他の指と軸が異なり、別の処理が必要なため一旦除外)
        bool indexBent = IsFingerBent(landmarks[5], landmarks[6], landmarks[7], landmarks[8]);
        bool middleBent = IsFingerBent(landmarks[9], landmarks[10], landmarks[11], landmarks[12]);
        bool ringBent = IsFingerBent(landmarks[13], landmarks[14], landmarks[15], landmarks[16]);
        bool pinkyBent = IsFingerBent(landmarks[17], landmarks[18], landmarks[19], landmarks[20]);
        
        // 手印の識別
        UpdateText(DetectSign(indexBent, middleBent, ringBent, pinkyBent));
    }

    /// <summary>
    /// MCP・PIP・DIP・TIPの4点から指の曲げ状態を判定する
    /// 付け根（MCP）,第二関節（PIP）,第一関節（DIP）,各指先（TIP）
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
    /// </summary>
    private string DetectSign(bool indexBent, bool middleBent, bool ringBent, bool pinkyBent)
    {
        // Open: 全指が伸びている
        if (!indexBent && !middleBent && !ringBent && !pinkyBent)
            return "Open";

        // Fist: 全指が曲がっている
        if (indexBent && middleBent && ringBent && pinkyBent)
            return "Fist";

        // Point: 人差し指だけ伸びている
        if (!indexBent && middleBent && ringBent && pinkyBent)
            return "Point";

        return "Unknown";
    }
    
    private void UpdateText(string sign)
    {
        if (_handSignText != null)
            _handSignText.text = sign;

        Debug.Log($"[HandSign] {sign}");
    }
}
