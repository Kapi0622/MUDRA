using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace MUDRA.HandTracking
{
    /// <summary>
    /// ランドマーク座標から手印（HandSign）を識別する、入力パイプラインの中核クラス。
    ///
    /// 【責務】
    /// - IHandLandmarkProviderから毎フレーム最新のランドマーク座標を取得（Pullパターン）
    /// - 指の関節角度を算出し、テーブル駆動のパターンマッチで手印を識別する
    /// - 安定判定（Nフレーム連続で同一手印を確認）を経て、確定した手印をR3ストリームで通知する
    ///
    /// 【判定フロー概要】
    /// 1手検出時:
    ///   Stage 1（片手パターン照合）→ 安定判定
    ///
    /// 2手検出時:
    ///   合掌チェック（手首距離ベース）→ 成功なら Union
    ///   失敗なら Stage 1 を左右各手に実行 → Stage 2（両手パターン照合）→ 安定判定
    ///   ※2手検出中は片手印を抑制し、両手印の判定を優先する
    ///
    /// 【設計意図】
    /// - Pure C#クラス（MonoBehaviour非継承）として構成し、テスタビリティを確保
    /// - 駆動は外部（Presenter）からの毎フレームTick()呼び出しに依存
    /// - パターン定義はテーブル駆動で管理し、新しい印の追加をデータの1行追加で完結させる
    /// </summary>
    public sealed class HandTrackingService : IDisposable
    {
        // =========================================================================
        // 依存・設定
        // =========================================================================

        private readonly IHandLandmarkProvider _provider;

        /// <summary>人差し指〜小指の曲げ閾値（度）。この角度を超えると「曲がっている」と判定</summary>
        private readonly float _bentThreshold;

        /// <summary>
        /// 親指専用の曲げ閾値（度）。
        /// 親指は関節構造が他4指と異なり可動域が狭いため、専用の低い閾値を使用する。
        /// A3の実測で Open時10° / Fist時26° → 閾値20°で14°以上のマージンを確保。
        /// </summary>
        private readonly float _thumbBentThreshold;

        /// <summary>安定判定に必要な連続フレーム数（この回数同じ判定が続いたら確定）</summary>
        private readonly int _stableFrameCount;

        // =========================================================================
        // 安定判定の内部状態
        // =========================================================================

        /// <summary>前フレームの識別結果（安定判定の比較対象）</summary>
        private HandSign? _lastSign;

        /// <summary>同一手印の連続検出カウンタ</summary>
        private int _stableCount;

        /// <summary>
        /// 確定済みフラグ（ラッチ）。
        /// trueの間は同じ印が続いても再発火しない。別の印に変わるとリセットされる。
        /// </summary>
        private bool _isConfirmed;

        // =========================================================================
        // R3ストリーム
        // =========================================================================

        /// <summary>手印が確定した際に発火する通知ストリーム</summary>
        private readonly Subject<HandSign> _onHandSignRecognized = new();

        public Observable<HandSign> OnHandSignRecognized => _onHandSignRecognized;

        // =========================================================================
        // 定数：ランドマークインデックス
        // =========================================================================

        /// <summary>手首のランドマークインデックス</summary>
        private const int WristIndex = 0;

        /// <summary>中指の付け根（MCP）のランドマークインデックス</summary>
        private const int MiddleFingerMcpIndex = 9;

        // =========================================================================
        // 定数：Union判定
        // =========================================================================

        /// <summary>
        /// Union判定の手首間距離しきい値（手のひら長基準の比率）。
        /// 手のひら長 = 手首(0)〜中指付け根(9)の距離をスケーリング基準とし、
        /// 両手の手首間距離をこの基準長で割った比率がこの値以下なら合掌と判定する。
        /// カメラとの距離に依存しない正規化された判定を実現する。
        /// 実機テストで調整する前提の仮値。β版でScriptableObject化候補。
        /// </summary>
        private const float UnionWristDistanceRatio = 1.5f;

        // =========================================================================
        // テーブル定義：片手パターン（Stage 1）
        // =========================================================================

        /// <summary>
        /// 片手の指パターン定義。
        /// 5本指の曲げ状態（true=曲げ, false=伸び）の組み合わせで手印を識別する。
        /// null はワイルドカード（どちらでも一致）。現行パターンでは未使用だが、
        /// 将来的に「親指はどちらでも良い」等の柔軟なパターン定義に備えている。
        /// </summary>
        private readonly struct SingleHandPattern
        {
            public readonly bool? Thumb;
            public readonly bool? Index;
            public readonly bool? Middle;
            public readonly bool? Ring;
            public readonly bool? Pinky;
            public readonly HandSign Sign;

            public SingleHandPattern(
                bool? thumb, bool? index, bool? middle, bool? ring, bool? pinky,
                HandSign sign)
            {
                Thumb = thumb;
                Index = index;
                Middle = middle;
                Ring = ring;
                Pinky = pinky;
                Sign = sign;
            }

            /// <summary>
            /// 5指の曲げ状態がこのパターンに一致するかを判定する。
            /// ワイルドカード（null）の指は常に一致として扱う。
            /// </summary>
            public bool Matches(bool thumb, bool index, bool middle, bool ring, bool pinky)
            {
                return (Thumb == null || Thumb == thumb)
                    && (Index == null || Index == index)
                    && (Middle == null || Middle == middle)
                    && (Ring == null || Ring == ring)
                    && (Pinky == null || Pinky == pinky);
            }
        }

        /// <summary>
        /// 片手パターンテーブル。上から順に照合し、最初にマッチしたものを返す。
        /// 配列の順序がそのまま判定の優先度になる。
        ///
        /// パターン表（O=伸び, X=曲げ）:
        ///   Open     : [O, O, O, O, O] 全指伸び
        ///   Fist     : [X, X, X, X, X] 全指曲げ
        ///   Guard    : [O, X, X, X, X] 親指のみ伸び
        ///   Point    : [X, O, X, X, X] 人差し指のみ伸び
        ///   Scissors : [X, O, O, X, X] 人差し指+中指伸び
        ///   Palm     : [X, O, O, O, O] 親指のみ曲げ
        ///   Release  : [X, O, X, X, O] 親指曲げ+人差し指+小指伸び
        ///   Cancel   : [X, X, X, X, O] 小指のみ伸び
        /// </summary>
        private static readonly SingleHandPattern[] SingleHandPatterns = new[]
        {
            //                          thumb  index  mid    ring   pinky  sign
            new SingleHandPattern(      false, false, false, false, false, HandSign.Open),
            new SingleHandPattern(      true,  true,  true,  true,  true,  HandSign.Fist),
            new SingleHandPattern(      false, true,  true,  true,  true,  HandSign.Guard),
            new SingleHandPattern(      true,  false, true,  true,  true,  HandSign.Point),
            new SingleHandPattern(      true,  false, false, true,  true,  HandSign.Scissors),
            new SingleHandPattern(      true,  false, false, false, false, HandSign.Palm),
            new SingleHandPattern(      true,  false, true,  true,  false, HandSign.Release),
            new SingleHandPattern(      true,  true,  true,  true,  false, HandSign.Cancel),
        };

        // =========================================================================
        // テーブル定義：両手パターン（Stage 2）
        // =========================================================================

        /// <summary>
        /// 両手の指パターン定義。
        /// Stage 1で左右それぞれ識別した片手の形の組み合わせで、両手印を照合する。
        /// null はワイルドカード（その手の形は問わない）。
        /// </summary>
        private readonly struct TwoHandPattern
        {
            public readonly HandSign? Left;
            public readonly HandSign? Right;
            public readonly HandSign ResultSign;

            public TwoHandPattern(HandSign? left, HandSign? right, HandSign resultSign)
            {
                Left = left;
                Right = right;
                ResultSign = resultSign;
            }

            /// <summary>
            /// 左右の片手判定結果がこのパターンに一致するかを判定する。
            /// ワイルドカード（null）の手は常に一致として扱う。
            /// </summary>
            public bool Matches(HandSign? left, HandSign? right)
            {
                return (Left == null || Left == left)
                    && (Right == null || Right == right);
            }
        }

        /// <summary>
        /// 両手パターンテーブル。上から順に照合し、最初にマッチしたものを返す。
        /// 合掌Unionは手首距離チェックで別途判定するため、ここには含まない。
        ///
        /// 新しい両手印はここに1行追加するだけで定義可能。
        /// 例: new TwoHandPattern(HandSign.Fist, HandSign.Open, HandSign.SomeNewSign),
        /// </summary>
        private static readonly TwoHandPattern[] TwoHandPatterns = new[]
        {
            // 現在は空。10本指パターンはここに追加していく。
            // 例：左グー + 右パー → 新しい印
            new TwoHandPattern(HandSign.Fist, HandSign.Open, HandSign.Scissors),
        };

        // =========================================================================
        // コンストラクタ
        // =========================================================================

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

        // =========================================================================
        // 毎フレーム駆動（公開メソッド）
        // =========================================================================

        /// <summary>
        /// 駆動役（Presenter）から毎フレーム呼ばれるエントリーポイント。
        /// Providerから最新座標を取得し、手の検出数に応じて判定フローを分岐する。
        ///
        /// 2手検出時:
        ///   1. 合掌チェック（手首距離ベース）→ 成功なら Union
        ///   2. 失敗なら Stage 1（左右各手の片手判定）→ Stage 2（両手パターン照合）
        ///   ※ どちらにもマッチしなければ null（両手検出中は片手印を抑制）
        ///
        /// 1手検出時:
        ///   Stage 1（片手パターン照合）のみ
        /// </summary>
        public void Tick()
        {
            var hand0 = _provider.GetLandmarks(0);

            HandSign? currentSign;

            if (_provider.DetectedHandCount >= 2)
            {
                var hand1 = _provider.GetLandmarks(1);

                // 合掌Union判定：2手検出かつ手首間距離が閾値以下
                if (hand0.Count > 0 && hand1.Count > 0 && IsUnionPose(hand0, hand1))
                {
                    currentSign = HandSign.Union;
                }
                else
                {
                    // 合掌条件未達 → 10本指パターン判定（Stage 1 → Stage 2）
                    // マッチしなければ null を返す（片手印は抑制される）
                    currentSign = DetectTwoHandSign(hand0, hand1);
                }
            }
            else
            {
                // 1手のみ検出 → 通常の片手判定
                var isHandDetected = _provider.DetectedHandCount > 0 && hand0.Count > 0;
                currentSign = isHandDetected ? DetectSign(hand0) : null;
            }

            JudgeStability(currentSign);
        }

        // =========================================================================
        // 片手判定（Stage 1）
        // =========================================================================

        /// <summary>
        /// 1手分のランドマークから5本指の曲げ状態を算出し、
        /// テーブル駆動のパターンマッチで手印を識別する。
        ///
        /// 親指はCMC(1)-MCP(2)-IP(3)のなす角で判定（閾値20°）。
        /// 他4指はMCP-PIP-DIPのなす角で判定（閾値45°）。
        /// どのパターンにも一致しない場合は null（未知）を返す。
        /// </summary>
        private HandSign? DetectSign(IReadOnlyList<HandLandmark> landmarks)
        {
            // 親指: CMC(1)-MCP(2)-IP(3) の3点で判定
            // 検証結果（A3 ThumbAngleDebugger）:
            //   Open時 10°前後 / Fist時 26°前後 / Palm時 38°前後
            //   → 閾値20°で14°以上のマージンを確保
            var thumbBent = IsFingerBent(landmarks, mcpIndex: 1, pipIndex: 2, dipIndex: 3,
                threshold: _thumbBentThreshold);

            // 他4指: MCP-PIP-DIP の3点で判定
            var indexBent = IsFingerBent(landmarks, mcpIndex: 5, pipIndex: 6, dipIndex: 7);
            var middleBent = IsFingerBent(landmarks, mcpIndex: 9, pipIndex: 10, dipIndex: 11);
            var ringBent = IsFingerBent(landmarks, mcpIndex: 13, pipIndex: 14, dipIndex: 15);
            var pinkyBent = IsFingerBent(landmarks, mcpIndex: 17, pipIndex: 18, dipIndex: 19);

            // テーブル駆動パターンマッチ：上から順に照合し、最初の一致を返す
            foreach (var pattern in SingleHandPatterns)
            {
                if (pattern.Matches(thumbBent, indexBent, middleBent, ringBent, pinkyBent))
                    return pattern.Sign;
            }

            return null;
        }

        /// <summary>
        /// 指1本の曲げ状態を判定する。
        /// 3点（付け根・中間関節・先端側関節）のなす角が閾値を超えていれば「曲がっている」と判定。
        /// 親指は関節構造が異なるため、呼び出し側でインデックスと閾値を適切に指定する。
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

        // =========================================================================
        // 両手判定（Stage 1 → Stage 2）
        // =========================================================================

        /// <summary>
        /// 両手の指パターンを個別に識別し（Stage 1）、
        /// 左右の組み合わせで両手印を照合する（Stage 2）。
        ///
        /// Providerの handedness 情報を使って左右を振り分けた上で、
        /// TwoHandPatterns テーブルを上から順に照合する。
        /// どの両手パターンにもマッチしなければ null を返す（片手印は抑制される）。
        /// </summary>
        private HandSign? DetectTwoHandSign(
            IReadOnlyList<HandLandmark> hand0,
            IReadOnlyList<HandLandmark> hand1)
        {
            // Stage 1: 各手の片手判定
            var sign0 = hand0.Count > 0 ? DetectSign(hand0) : null;
            var sign1 = hand1.Count > 0 ? DetectSign(hand1) : null;

            // 左右の振り分け（MediaPipeの handedness 分類結果を使用）
            var isHand0Left = _provider.IsLeftHand(0);
            var isHand1Left = _provider.IsLeftHand(1);

            HandSign? leftSign;
            HandSign? rightSign;

            if (isHand0Left == true)
            {
                leftSign = sign0;
                rightSign = sign1;
            }
            else if (isHand1Left == true)
            {
                leftSign = sign1;
                rightSign = sign0;
            }
            else
            {
                // handedness が不明な場合は判定不能
                return null;
            }

            // Stage 2: 両手パターンテーブルを上から順に照合
            foreach (var pattern in TwoHandPatterns)
            {
                if (pattern.Matches(leftSign, rightSign))
                    return pattern.ResultSign;
            }

            return null;
        }

        // =========================================================================
        // 合掌判定（Union専用）
        // =========================================================================

        /// <summary>
        /// 両手の手首間距離が、1手目の手のひら長を基準にした比率で
        /// 閾値以下であれば true を返す（合掌判定）。
        ///
        /// 【正規化の仕組み】
        /// MediaPipeのランドマーク座標は画面に対する正規化座標（0.0〜1.0）で返るため、
        /// カメラからの距離によって手の映り具合が変わる。
        /// 手のひら長（手首→中指付け根）をスケーリング基準に使うことで、
        /// カメラ距離に依存しない安定した判定を実現する。
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

        // =========================================================================
        // 安定判定
        // =========================================================================

        /// <summary>
        /// Nフレーム連続で同一手印を検出したら確定させる。
        ///
        /// 【null時の挙動】
        /// 「手なし」「未知パターン」「両手検出中の片手印抑制」は全て null として届く。
        /// null時は安定判定の状態に一切触れず早期returnする。
        /// これにより手が一瞬消えても直前の確定状態を保持したまま継続できる。
        ///
        /// 【ラッチ機構】
        /// 一度確定した手印は _isConfirmed フラグで記録し、
        /// 別の手印に変わるまで再確定しない。
        /// 同じ印を保持し続けても確定イベントは1回だけ発火する。
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

        // =========================================================================
        // リソース解放
        // =========================================================================

        public void Dispose()
        {
            _onHandSignRecognized.Dispose();
        }
    }
}