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
    /// - 安定判定（同一手印が一定時間続くことを確認）を経て、確定した手印をR3ストリームで通知する
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
    /// - 駆動は外部（Presenter）からの毎フレームTick(deltaTime)呼び出しに依存
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
        /// A3の実測で Open時10° / Fist時26° → 閾値20°。マージンは Open側10° / Fist側6°。
        /// </summary>
        private readonly float _thumbBentThreshold;

        /// <summary>
        /// 安定判定に必要な継続時間（秒）。同じ判定が、手が静止している間にこの時間続いたら確定。
        /// 以前は描画フレーム数（36）で数えていたが、FPSで確定時間が変わるため秒に変更した。
        /// 既定値0.15秒は、静止判定（StillSpeedThreshold）で組み替え途中の形を除いたうえで、
        /// 変更前の 36フレーム ÷ 約160fps ≒ 0.23秒 から短くした値。
        /// </summary>
        private readonly float _stableSeconds;

        // =========================================================================
        // 安定判定の内部状態
        // =========================================================================

        /// <summary>前フレームの識別結果（安定判定の比較対象）</summary>
        private HandSign? _lastSign;

        /// <summary>同一手印が続いている時間（秒）</summary>
        private float _stableElapsed;

        /// <summary>
        /// 確定済みフラグ（ラッチ）。
        /// trueの間は同じ印が続いても再発火しない。別の印に変わるとリセットされる。
        /// </summary>
        private bool _isConfirmed;

        // =========================================================================
        // ヒステリシスの内部状態
        // =========================================================================

        /// <summary>
        /// 指ごとの前回の曲げ状態。[手の区分, 指(0=親指〜4=小指)]。null は履歴なし。
        /// 2手検出時はMediaPipeが返す手の順番が入れ替わることがあるため、
        /// 配列の添字ではなく左右（handedness）で区分して状態が混ざらないようにする。
        /// </summary>
        private readonly bool?[,] _prevBent = new bool?[HandSlotCount, FingerCount];

        /// <summary>今回のTickで判定した手の区分。見えなかった区分の履歴はTick末尾で破棄する</summary>
        private readonly bool[] _seenSlots = new bool[HandSlotCount];

        // =========================================================================
        // 手の速さの内部状態
        // =========================================================================

        /// <summary>前回処理した推論結果の番号（IHandLandmarkProvider.ResultVersion）</summary>
        private int _lastResultVersion = -1;

        /// <summary>前回の推論結果からの経過時間（秒）。速さの分母に使う</summary>
        private float _timeSinceLastResult;

        /// <summary>前回の推論結果での計測点の位置。[手の区分, 計測点]</summary>
        private readonly Vector3[,] _prevMotionPoints = new Vector3[HandSlotCount, MotionPointIndices.Length];

        /// <summary>_prevMotionPoints に有効な値が入っているか（手の区分ごと）</summary>
        private readonly bool[] _hasPrevMotion = new bool[HandSlotCount];

        /// <summary>
        /// 平滑化後の手の速さ（手のひら長/秒）。静止判定に使う。2手のときは速いほう。
        /// 手のひら長（手首〜中指の付け根）で割ることで、カメラとの距離に左右されない値にしている。
        /// 推論1回ごとの値は保持中でもぶれが大きいため、指数移動平均でならしてから使う。
        /// </summary>
        private float _smoothedHandSpeed;

        // =========================================================================
        // 合の保持の内部状態
        // =========================================================================

        /// <summary>合の保持の残り時間（秒）。2手検出でUnionと判定されるたびにUnionHoldSecondsへ延長する</summary>
        private float _unionHoldRemaining;

        /// <summary>合の保持中か（1本のOpenをUnionとみなす期間か）</summary>
        private bool IsUnionHoldActive => _unionHoldRemaining > 0f;

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
        // 定数：ヒステリシス
        // =========================================================================

        /// <summary>
        /// 親指の閾値に持たせる余裕（度）。伸び→曲げは閾値+余裕、曲げ→伸びは閾値-余裕で切り替える。
        /// Fist時の実測26°と閾値20°の差が6°しかないため、他の指より狭くしている
        /// （伸び→曲げの切り替えが23°になり、Fistに対して3°のマージンが残る）。
        /// </summary>
        private const float ThumbHysteresisDegrees = 3f;

        /// <summary>人差し指〜小指の閾値に持たせる余裕（度）</summary>
        private const float FingerHysteresisDegrees = 5f;

        /// <summary>手の区分数（左・右・左右不明）</summary>
        private const int HandSlotCount = 3;

        private const int LeftHandSlot = 0;
        private const int RightHandSlot = 1;
        private const int UnknownHandSlot = 2;

        /// <summary>指の本数（親指〜小指）</summary>
        private const int FingerCount = 5;

        // =========================================================================
        // 定数：手の速さ
        // =========================================================================

        /// <summary>
        /// 手の速さの計測に使うランドマーク（手首と5本の指先）。
        /// 指の曲げ伸ばしは指先に最も大きく現れるため、手全体の移動と印の組み替えの両方を拾える。
        /// </summary>
        private static readonly int[] MotionPointIndices = { 0, 4, 8, 12, 16, 20 };

        /// <summary>
        /// 静止とみなす速さの上限（手のひら長/秒）。平滑化後の速さがこれ未満のときだけ確定時間を進める。
        /// ステップ3aの実測: 保持中は平均0.7前後（1回ごとの最大2.5）、
        /// 組み替え中は平均2〜8（最大16〜39）、組み替え途中に一瞬成立したCancelは3.6〜4.9。
        /// </summary>
        private const float StillSpeedThreshold = 3.0f;

        /// <summary>
        /// 速さの指数移動平均の時定数（秒）。
        /// 重みを推論間隔から求めるため、推論頻度が揺れても時間あたりのならし具合は一定になる。
        /// </summary>
        private const float SpeedSmoothingSeconds = 0.1f;

        private const int ThumbFinger = 0;
        private const int IndexFinger = 1;
        private const int MiddleFinger = 2;
        private const int RingFinger = 3;
        private const int PinkyFinger = 4;

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
        private const float UnionWristDistanceRatio = 0.8f;

        /// <summary>
        /// 合の保持時間（秒）。2手検出でUnionと判定されてからこの時間は、
        /// 1本しか見えなくてもその1本がOpenならUnionとみなす。
        /// 手のひらを合わせるとMediaPipeは重なった手を「開いた片手」として1本だけ検出しがちなため
        /// （ステップ3aの実測で 1本 68〜89% / 2本 11%）。2本検出は1秒に2〜3回で、
        /// 間隔は平均0.4秒前後なので、それを少し上回る値にしている。
        /// </summary>
        private const float UnionHoldSeconds = 0.5f;

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
            new TwoHandPattern(HandSign.Scissors, HandSign.Scissors, HandSign.DoubleScissors),
        };

        // =========================================================================
        // コンストラクタ
        // =========================================================================

        public HandTrackingService(
            IHandLandmarkProvider provider,
            float bentThreshold = 45f,
            float thumbBentThreshold = 20f,
            float stableSeconds = 0.15f)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _bentThreshold = bentThreshold;
            _thumbBentThreshold = thumbBentThreshold;
            _stableSeconds = stableSeconds;
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
        /// <param name="deltaTime">前回のTickからの経過時間（秒）。安定判定の時間計測に使う</param>
        public void Tick(float deltaTime)
        {
            Array.Clear(_seenSlots, 0, _seenSlots.Length);

            if (IsUnionHoldActive)
            {
                _unionHoldRemaining = MathF.Max(_unionHoldRemaining - deltaTime, 0f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!IsUnionHoldActive) DebugLog("合の保持 終了（時間切れ）");
#endif
            }

            _timeSinceLastResult += deltaTime;
            if (_provider.ResultVersion != _lastResultVersion)
            {
                _lastResultVersion = _provider.ResultVersion;
                UpdateHandSpeed(_timeSinceLastResult);
                _timeSinceLastResult = 0f;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugCandidateWallElapsed += deltaTime;
#endif

            var hand0 = _provider.GetLandmarks(0);

            HandSign? currentSign;

            if (_provider.DetectedHandCount >= 2)
            {
                var hand1 = _provider.GetLandmarks(1);

                // 合掌Union判定：2手検出かつ手首間距離が閾値以下
                if (hand0.Count > 0 && hand1.Count > 0 && IsUnionPose(hand0, hand1))
                {
                    currentSign = HandSign.Union;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (!IsUnionHoldActive) DebugLog("合の保持 開始");
#endif
                    _unionHoldRemaining = UnionHoldSeconds;
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
                currentSign = isHandDetected ? DetectSign(hand0, GetHandSlot(0)) : null;
                currentSign = ApplyUnionHold(currentSign);
            }

            ClearUnseenFingerHistory();
            JudgeStability(currentSign, deltaTime);
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
        /// 各指の判定には手の区分ごとの前回状態によるヒステリシスがかかる。
        /// どのパターンにも一致しない場合は null（未知）を返す。
        /// </summary>
        /// <param name="handSlot">手の区分（GetHandSlotの戻り値）。ヒステリシスの履歴の参照先</param>
        private HandSign? DetectSign(IReadOnlyList<HandLandmark> landmarks, int handSlot)
        {
            _seenSlots[handSlot] = true;

            // 親指: CMC(1)-MCP(2)-IP(3) の3点で判定
            // 検証結果（A3 ThumbAngleDebugger）:
            //   Open時 10°前後 / Fist時 26°前後 / Palm時 38°前後
            //   → 閾値20°（マージン Open側10° / Fist側6°）
            var thumbBent = IsFingerBent(landmarks, handSlot, ThumbFinger,
                mcpIndex: 1, pipIndex: 2, dipIndex: 3,
                _thumbBentThreshold, ThumbHysteresisDegrees);

            // 他4指: MCP-PIP-DIP の3点で判定
            var indexBent = IsFingerBent(landmarks, handSlot, IndexFinger,
                mcpIndex: 5, pipIndex: 6, dipIndex: 7, _bentThreshold, FingerHysteresisDegrees);
            var middleBent = IsFingerBent(landmarks, handSlot, MiddleFinger,
                mcpIndex: 9, pipIndex: 10, dipIndex: 11, _bentThreshold, FingerHysteresisDegrees);
            var ringBent = IsFingerBent(landmarks, handSlot, RingFinger,
                mcpIndex: 13, pipIndex: 14, dipIndex: 15, _bentThreshold, FingerHysteresisDegrees);
            var pinkyBent = IsFingerBent(landmarks, handSlot, PinkyFinger,
                mcpIndex: 17, pipIndex: 18, dipIndex: 19, _bentThreshold, FingerHysteresisDegrees);

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
        ///
        /// 【ヒステリシス】
        /// 角度が閾値付近で揺れると曲げ/伸びがフレームごとに入れ替わり、印の候補が途切れる。
        /// これを防ぐため、前回の状態に応じて切り替え点をずらす。
        /// - 前回「曲げ」: 角度が 閾値-余裕 を下回るまで「曲げ」のまま
        /// - 前回「伸び」: 角度が 閾値+余裕 を上回るまで「伸び」のまま
        /// - 履歴なし: 閾値そのもので判定
        /// </summary>
        private bool IsFingerBent(
            IReadOnlyList<HandLandmark> landmarks,
            int handSlot,
            int finger,
            int mcpIndex,
            int pipIndex,
            int dipIndex,
            float threshold,
            float hysteresis)
        {
            var mcp = landmarks[mcpIndex].Position;
            var pip = landmarks[pipIndex].Position;
            var dip = landmarks[dipIndex].Position;

            var vectorA = pip - mcp;
            var vectorB = dip - pip;
            var angle = Vector3.Angle(vectorA, vectorB);

            var previous = _prevBent[handSlot, finger];
            var effectiveThreshold = previous switch
            {
                true => threshold - hysteresis,
                false => threshold + hysteresis,
                null => threshold,
            };

            var isBent = angle > effectiveThreshold;
            _prevBent[handSlot, finger] = isBent;
            return isBent;
        }

        /// <summary>
        /// Providerの handedness 情報から、ヒステリシス履歴の区分を返す。
        /// 2手とも同じ側と分類された場合は区分が衝突するが、
        /// その場合は両手判定自体も不安定なため、履歴の共有を許容する。
        /// </summary>
        private int GetHandSlot(int handIndex)
        {
            return _provider.IsLeftHand(handIndex) switch
            {
                true => LeftHandSlot,
                false => RightHandSlot,
                null => UnknownHandSlot,
            };
        }

        /// <summary>
        /// 今回のTickで判定しなかった区分のヒステリシス履歴を破棄する。
        /// 手が消えて再び現れたときに、古い曲げ状態を引きずらないようにするため。
        /// </summary>
        private void ClearUnseenFingerHistory()
        {
            for (var slot = 0; slot < HandSlotCount; slot++)
            {
                if (_seenSlots[slot]) continue;

                for (var finger = 0; finger < FingerCount; finger++)
                {
                    _prevBent[slot, finger] = null;
                }
            }
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
            var sign0 = hand0.Count > 0 ? DetectSign(hand0, GetHandSlot(0)) : null;
            var sign1 = hand1.Count > 0 ? DetectSign(hand1, GetHandSlot(1)) : null;

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
        // 手の速さ
        // =========================================================================

        /// <summary>
        /// 新しい推論結果が届いたときに呼ばれ、前回の推論結果からの手の速さを更新する。
        /// 速さ = 計測点の最大移動量 ÷ 手のひら長 ÷ 経過秒（単位: 手のひら長/秒）。
        /// 前回の位置は手の区分（左右）ごとに保持し、2手のときは速いほうを採用する。
        /// 求めた速さは指数移動平均でならして _smoothedHandSpeed に反映する。
        ///
        /// 【新しく現れた手】
        /// 前回その区分の手が見えていなかった場合は速さを測れない。
        /// 手を上げてきた直後に止まる前に確定するのを防ぐため、「動いている」扱い
        /// （StillSpeedThreshold）から始め、静止が数回続いてから確定時間が進むようにする。
        /// </summary>
        private void UpdateHandSpeed(float elapsedSeconds)
        {
            Span<bool> seenNow = stackalloc bool[HandSlotCount];
            var maxSpeed = 0f;
            var hasNewHand = false;
            var handCount = Math.Min(_provider.DetectedHandCount, 2);

            for (var hand = 0; hand < handCount; hand++)
            {
                var landmarks = _provider.GetLandmarks(hand);
                if (landmarks.Count == 0) continue;

                var palmLength = Vector3.Distance(
                    landmarks[WristIndex].Position,
                    landmarks[MiddleFingerMcpIndex].Position);
                if (palmLength < 0.001f) continue;

                var slot = GetHandSlot(hand);
                var maxDisplacement = 0f;

                for (var p = 0; p < MotionPointIndices.Length; p++)
                {
                    var current = landmarks[MotionPointIndices[p]].Position;
                    if (_hasPrevMotion[slot])
                    {
                        var displacement = Vector3.Distance(current, _prevMotionPoints[slot, p]);
                        if (displacement > maxDisplacement) maxDisplacement = displacement;
                    }
                    _prevMotionPoints[slot, p] = current;
                }

                if (!_hasPrevMotion[slot])
                {
                    hasNewHand = true;
                }
                else if (elapsedSeconds > 0f)
                {
                    var speed = maxDisplacement / palmLength / elapsedSeconds;
                    if (speed > maxSpeed) maxSpeed = speed;
                }

                _hasPrevMotion[slot] = true;
                seenNow[slot] = true;
            }

            // 今回見えなかった区分は、次に現れたときに古い位置と比べないよう破棄する
            for (var slot = 0; slot < HandSlotCount; slot++)
            {
                if (!seenNow[slot]) _hasPrevMotion[slot] = false;
            }

            // 手が0本のときは片手印の判定自体が null になるため、速さは更新しない
            if (handCount == 0) return;

            var weight = 1f - MathF.Exp(-elapsedSeconds / SpeedSmoothingSeconds);
            _smoothedHandSpeed += (maxSpeed - _smoothedHandSpeed) * weight;

            // 合の保持中は、2本目の手の出入りや1本時の左右判定の入れ替わりで「新しい手」が頻発する。
            // そのたびに動いている扱いにすると Union の確定時間が進まなくなるため、保持中は引き上げない
            if (hasNewHand && !IsUnionHoldActive)
            {
                _smoothedHandSpeed = MathF.Max(_smoothedHandSpeed, StillSpeedThreshold);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // =========================================================================
        // 調整用ログ
        // =========================================================================

        /// <summary>
        /// 判定の経過（候補切替・確定・合の保持）をログに出すか。既定はオフ。
        /// 閾値（StillSpeedThreshold・stableSeconds・UnionHoldSeconds）を調整するときだけ
        /// HandSignPresenter のインスペクターからオンにする。手順は docs/hand_sign_tuning.md。
        /// </summary>
        public bool IsDebugLogEnabled { get; set; }

        /// <summary>候補の印になってからの実時間（静止待ち・nullフレームを含む）</summary>
        private float _debugCandidateWallElapsed;

        private void DebugLog(string message)
        {
            if (IsDebugLogEnabled) Debug.Log($"[HandSign] {message}");
        }
#endif

        // =========================================================================
        // 合の保持
        // =========================================================================

        /// <summary>
        /// 1手検出時の判定結果に、合の保持を適用する。
        ///
        /// 手のひらを合わせるとMediaPipeは重なった手を「開いた片手」として1本だけ検出しがちで、
        /// そのままでは合の途中でOpenが確定してしまう。そこで保持中は:
        /// - Open → Union とみなす
        /// - Open以外の印 → 保持をすぐやめてその印を返す（合のあと別の印へ移るときに待たせないため）
        /// - null → そのまま null
        ///
        /// 合の形に限定した対応。合わせた手が別の形（Palmなど）に見える例が出たら見直す。
        /// </summary>
        private HandSign? ApplyUnionHold(HandSign? singleHandSign)
        {
            if (!IsUnionHoldActive || singleHandSign is null) return singleHandSign;

            if (singleHandSign == HandSign.Open) return HandSign.Union;

            _unionHoldRemaining = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugLog($"合の保持 終了（他の印: {singleHandSign}）");
#endif
            return singleHandSign;
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
        /// 同一手印が、手が静止している間に _stableSeconds 秒続いたら確定させる。
        /// 時間は描画フレームのdeltaTimeで積算するため、FPSによらず確定時間が一定になる。
        ///
        /// 【静止判定】
        /// 平滑化後の手の速さが StillSpeedThreshold 未満のときだけ時間を積算する。
        /// 印を組み替える途中の中間の形（例: Cancel → Release の途中）は手が動いているため積算されず、
        /// 確定時間を短くしても誤確定しにくい。候補の切り替え自体は速さに関係なく行う。
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
        private void JudgeStability(HandSign? currentSign, float deltaTime)
        {
            if (currentSign is null)
            {
                // 状態には一切触れず、比較・カウントの対象から除外するだけ
                // 手が一瞬消えても、直前の確定状態を保持したまま継続する
                return;
            }

            if (currentSign != _lastSign)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // 確定前に別の印へ切り替わった（＝判定が揺れた）ときだけ記録する
                if (_lastSign != null && !_isConfirmed)
                {
                    DebugLog($"候補切替 {_lastSign} → {currentSign}" +
                             $"（{_lastSign}は積算{_stableElapsed:F2}秒・実時間{_debugCandidateWallElapsed:F2}秒で途切れ" +
                             $"・速さ {_smoothedHandSpeed:F2}）");
                }
                _debugCandidateWallElapsed = 0f;
#endif
                _lastSign = currentSign;
                _stableElapsed = 0f;
                _isConfirmed = false;
                return;
            }

            if (_isConfirmed)
            {
                return;
            }

            // 手が動いている間（印の組み替え中）は確定時間を進めない
            if (_smoothedHandSpeed < StillSpeedThreshold)
            {
                _stableElapsed += deltaTime;
            }

            if (_stableElapsed >= _stableSeconds)
            {
                _isConfirmed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DebugLog($"確定 {currentSign}（積算{_stableElapsed:F2}秒" +
                         $"・実時間{_debugCandidateWallElapsed:F2}秒・速さ {_smoothedHandSpeed:F2}）");
#endif
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