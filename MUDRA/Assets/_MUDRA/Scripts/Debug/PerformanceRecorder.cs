#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Globalization;
using System.IO;
using MUDRA.HandTracking;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MUDRA.Debugging
{
    /// <summary>
    /// 描画と推論の負荷を1秒ごとにCSVへ記録する計測ツール（InGame 3D化の事前計測）。
    /// F3で計測の開始・停止を切り替える。保存先は Application.persistentDataPath/perf_日時.csv。
    ///
    /// 3D化の前後で同じ手順を測り比べるためのもの。FPSだけでなく推論結果の届く頻度（infer_hz）を取るのは、
    /// Windowsでは推論がCPUで動き（HandLandmarkDetectionConfig）、描画のCPU負荷と取り合うため。
    /// FPSが保たれていても推論が間引かれていれば、印の認識の手応えが落ちる。
    ///
    /// Title〜InGame〜Resultをまたいで測るため、常駐のApp.unityに置く。
    /// 値は実時間（unscaled）で数え、ポーズやヒットストップで止まらないようにしている。
    /// main_msはDevelopment Build（とエディタ）でしか取れないため、取れない環境では-1を書く。
    /// GPU時間はPlayer設定（Frame Timing Stats）の変更が要るため、今は取らない。
    /// </summary>
    public sealed class PerformanceRecorder : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F3;
        private const float SampleInterval = 1f;
        private const string MainThreadStatName = "Main Thread";
        private const double NanosecondsPerMillisecond = 1_000_000.0;
        private const string FileNameFormat = "yyyyMMdd_HHmmss";
        private const string CsvHeader = "t,scene,fps,frame_avg_ms,frame_max_ms,main_ms,infer_hz,hands";
        private const int UnavailableValue = -1;

        // --- 画面表示 ---
        private const int LabelFontSize = 24;
        private const float LabelWidth = 720f;
        private const float LabelHeight = 40f;
        private const float LabelMargin = 10f;
        private static readonly Color LabelColor = new(1f, 0.35f, 0.35f);

        [Tooltip("推論結果の番号（ResultVersion）と手の数を読む")]
        [SerializeField] private MediaPipeHandLandmarkProvider _provider;

        private ProfilerRecorder _mainThreadRecorder;
        private StreamWriter _writer;
        private bool _isRecording;
        private float _recordStartTime;

        // --- 1区間ぶんの集計 ---
        private float _windowElapsed;
        private int _windowFrames;
        private float _windowFrameMax;
        private double _windowMainThreadNs;
        private int _windowMainThreadSamples;
        private int _windowStartResultVersion;

        // --- 画面表示用の直近の値 ---
        private float _lastFps;
        private float _lastInferHz;

        private GUIStyle _labelStyle;

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                if (_isRecording) StopRecording();
                else StartRecording();
            }

            if (!_isRecording) return;

            float dt = Time.unscaledDeltaTime;
            _windowElapsed += dt;
            _windowFrames++;
            _windowFrameMax = Mathf.Max(_windowFrameMax, dt);

            // LastValueは直前に完了したフレームの値。毎フレーム足して区間の平均を出す
            if (_mainThreadRecorder.Valid && _mainThreadRecorder.Count > 0)
            {
                _windowMainThreadNs += _mainThreadRecorder.LastValue;
                _windowMainThreadSamples++;
            }

            if (_windowElapsed >= SampleInterval) WriteRow();
        }

        private void StartRecording()
        {
            if (_provider == null)
            {
                Debug.LogError("[PerformanceRecorder] MediaPipeHandLandmarkProviderが未設定です");
                return;
            }

            var path = Path.Combine(
                Application.persistentDataPath,
                $"perf_{DateTime.Now.ToString(FileNameFormat, CultureInfo.InvariantCulture)}.csv");

            try
            {
                _writer = new StreamWriter(path, append: false);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PerformanceRecorder] CSVを作成できません: {path}\n{e}");
                return;
            }

            _writer.WriteLine(CsvHeader);
            _mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, MainThreadStatName);
            _recordStartTime = Time.realtimeSinceStartup;
            _isRecording = true;
            ResetWindow();

            Debug.Log($"[PerformanceRecorder] 計測開始: {path}");
        }

        private void StopRecording()
        {
            if (!_isRecording) return;
            _isRecording = false;

            _mainThreadRecorder.Dispose();
            _writer?.Dispose();
            _writer = null;

            Debug.Log("[PerformanceRecorder] 計測終了");
        }

        /// <summary>
        /// 1区間ぶんの集計を1行書き出す。
        /// handsは区間の終わりの時点の値（平均ではない）。手を映し続けているかの目安にする。
        /// </summary>
        private void WriteRow()
        {
            int resultVersion = _provider.ResultVersion;
            _lastFps = _windowFrames / _windowElapsed;
            _lastInferHz = (resultVersion - _windowStartResultVersion) / _windowElapsed;

            float frameAvgMs = _windowElapsed / _windowFrames * 1000f;
            float frameMaxMs = _windowFrameMax * 1000f;
            double mainMs = _windowMainThreadSamples > 0
                ? _windowMainThreadNs / _windowMainThreadSamples / NanosecondsPerMillisecond
                : UnavailableValue;
            bool isInGame = SceneManager.GetSceneByName(SceneNames.InGame).isLoaded;

            _writer.WriteLine(string.Join(",",
                Format(Time.realtimeSinceStartup - _recordStartTime),
                isInGame ? SceneNames.InGame : SceneNames.App,
                Format(_lastFps),
                Format(frameAvgMs),
                Format(frameMaxMs),
                Format(mainMs),
                Format(_lastInferHz),
                _provider.DetectedHandCount.ToString(CultureInfo.InvariantCulture)));
            _writer.Flush();

            ResetWindow();
        }

        private void ResetWindow()
        {
            _windowElapsed = 0f;
            _windowFrames = 0;
            _windowFrameMax = 0f;
            _windowMainThreadNs = 0;
            _windowMainThreadSamples = 0;
            _windowStartResultVersion = _provider.ResultVersion;
        }

        /// <summary>小数点をロケールによらず「.」にする（CSVの区切りと衝突させない）</summary>
        private static string Format(double value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        /// <summary>計測中だけ左下に直近の値を出す。計測していることを画面から確認できるようにする</summary>
        private void OnGUI()
        {
            if (!_isRecording) return;

            _labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = LabelFontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = LabelColor }
            };

            float elapsed = Time.realtimeSinceStartup - _recordStartTime;
            GUI.Label(
                new Rect(LabelMargin, Screen.height - LabelHeight - LabelMargin, LabelWidth, LabelHeight),
                $"● REC {elapsed:F0}s  FPS {_lastFps:F0}  推論 {_lastInferHz:F1}Hz",
                _labelStyle);
        }

        private void OnDestroy()
        {
            StopRecording();
        }
    }
}

#endif
