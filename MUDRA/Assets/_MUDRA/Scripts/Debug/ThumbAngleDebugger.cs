using System.Text;
using MUDRA.HandTracking;
using UnityEngine;

namespace MUDRA.Debugging
{
    /// <summary>
    /// 親指の曲げ角度を検証するための一時スクリプト。
    /// 検証完了後は削除する。
    /// 
    /// 使い方:
    /// 1. シーン上のGameObjectにアタッチ
    /// 2. Inspectorで providerBehaviour に IHandLandmarkProvider実装のMonoBehaviourをセット
    /// 3. Playモードでポーズを作り、数字キーで意図するポーズをタグ付け
    /// 4. Spaceキーで角度をログ出力
    /// </summary>
    public sealed class ThumbAngleDebugger : MonoBehaviour
    {
        [SerializeField, Tooltip("IHandLandmarkProviderを実装したMonoBehaviourをセット")]
        private MonoBehaviour providerBehaviour;

        private IHandLandmarkProvider _provider;
        private string _currentPoseLabel = "未タグ";

        private void Awake()
        {
            _provider = providerBehaviour as IHandLandmarkProvider;
            if (_provider == null)
            {
                Debug.LogError("providerBehaviourがIHandLandmarkProviderを実装していません");
            }
        }

        private void Update()
        {
            // ポーズのタグ付け（記録を見返しやすくするため）
            if (Input.GetKeyDown(KeyCode.Alpha1)) _currentPoseLabel = "Open想定";
            if (Input.GetKeyDown(KeyCode.Alpha2)) _currentPoseLabel = "Fist想定";
            if (Input.GetKeyDown(KeyCode.Alpha3)) _currentPoseLabel = "Palm想定（親指だけ折る）";

            if (Input.GetKeyDown(KeyCode.Space))
            {
                LogThumbAngles();
            }
        }

        private void LogThumbAngles()
        {
            if (_provider == null) return;

            var landmarks = _provider.GetCurrentLandmarks();
            if (_provider.DetectedHandCount <= 0 || landmarks.Count == 0)
            {
                Debug.LogWarning("手が検出されていません");
                return;
            }

            // 親指のランドマーク: 0=CMC(1), 1=MCP(2), 2=IP(3), 3=TIP(4)
            var cmc = landmarks[1].Position;
            var mcp = landmarks[2].Position;
            var ip = landmarks[3].Position;
            var tip = landmarks[4].Position;

            // パターンA: 他4指と同じ「3関節目まで」の構成（CMC→MCP→IP）
            var angleA = CalcAngle(cmc, mcp, ip);

            // パターンB: 1関節ずらした構成（MCP→IP→TIP）
            var angleB = CalcAngle(mcp, ip, tip);

            var sb = new StringBuilder();
            sb.AppendLine($"[ThumbAngleDebugger] タグ: {_currentPoseLabel}");
            sb.AppendLine($"  パターンA (CMC-MCP-IP)  : {angleA:F1}°");
            sb.AppendLine($"  パターンB (MCP-IP-TIP)  : {angleB:F1}°");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 3点から関節の曲げ角度を算出する（HandTrackingService.IsFingerBentと同じ計算式）
        /// </summary>
        private static float CalcAngle(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            var vectorA = p1 - p0;
            var vectorB = p2 - p1;
            return Vector3.Angle(vectorA, vectorB);
        }
    }
}