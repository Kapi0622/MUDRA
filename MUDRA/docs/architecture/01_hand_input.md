# ✋ 手印認識パイプライン

> **ドキュメント種別:** アーキテクチャ復習ドキュメント
> **対象:** `Assets/_MUDRA/Scripts/Input/`（namespace `MUDRA.HandTracking`）
> **関連:** [00_overview.md](00_overview.md) / [02_battle_system.md](02_battle_system.md)
> **仕様書:** §5 手印判定システム

このプロジェクトで最も密度が高い部分。カメラ映像から「確定した印」を1つ吐き出すまでを担う。

---

## 1. 全体の流れ

```mermaid
flowchart TD
    A["Webカメラ"]
    B["MediaPipe HandLandmarkerRunner<br/>サードパーティ・バックグラウンドスレッド"]
    C["MediaPipeHandLandmarkProvider<br/>MonoBehaviour・スレッド境界はここだけ"]
    D["HandTrackingService.Tick<br/>Pure C# ・毎フレーム"]
    D1["① 手の検出数で分岐"]
    D2["② 関節角度 → 5指の曲げ bool"]
    D3["③ パターンテーブル照合"]
    D4["④ 安定判定 36フレーム + ラッチ"]
    E["OnHandSignRecognized<br/>Observable の HandSign"]
    F["HandSignPresenter<br/>→ 02_battle_system.md へ"]

    A --> B
    B -->|"static event OnLandmarkDetected"| C
    C -->|"pull / IHandLandmarkProvider"| D
    D --> D1 --> D2 --> D3 --> D4 --> E --> F
```

---

## 2. 入口の継ぎ目：MediaPipeプラグインへの改変

サードパーティのサンプルコードに**直接手を入れている**。

`Assets/MediaPipeUnity/Samples/Scenes/Hand Landmark Detection/HandLandmarkerRunner.cs`

```csharp
// :24
public static event Action<HandLandmarkerResult> OnLandmarkDetected;

// :162（推論結果が出るたびに呼ばれる箇所）
OnLandmarkDetected?.Invoke(result); // 追加
```

これがゲーム側への唯一の入口であり、**プロジェクト内で唯一の static 状態**でもある。

> **注意:** MediaPipeUnityPlugin を更新するとこの2行は消える。リポジトリ直下に `MediaPipeUnity.0.16.3.unitypackage` が置かれているが、再インポート時は必ずこの改変を入れ直すこと。P2 で「プラグイン改変は最小限・グルーは static event」と決めた経緯がある（dev_log_P2）。

`HandLandmarkerRunner` はシーン上の `Solution` GameObject に載っている。

---

## 3. MediaPipeHandLandmarkProvider

`Input/MediaPipeHandLandmarkProvider.cs` — この層で唯一の MonoBehaviour。

責務は3つだけ。

1. **static event の購読** — `OnEnable` / `OnDisable` で購読・解除
2. **スレッドの引き渡し** — コールバックはバックグラウンドスレッドで来るので、`UniTask.SwitchToMainThread()` を挟んでからキャッシュを書き換える

   ```csharp
   private void HandleLandmarkDetected(HandLandmarkerResult result)
       => UpdateCacheAsync(result).Forget();

   private async UniTaskVoid UpdateCacheAsync(HandLandmarkerResult result)
   {
       await UniTask.SwitchToMainThread();
       UpdateCache(result);
   }
   ```

   > **設計意図:** P2 では `volatile` フラグで凌いでいたが、A1 で UniTask に置き換えた。**スレッド越境をこのクラス1箇所に封じ込める**ことで、以降の層は同期的に読めるだけの世界になる。

3. **キャッシュ保持** — `List<HandLandmark>[2]`（各21点分を事前確保）+ handedness（左右）情報。3手目以降は捨てる。範囲外アクセスには `Array.Empty<HandLandmark>()` を返す（毎回 `new` しないための static フィールド）

### IHandLandmarkProvider

```csharp
IReadOnlyList<HandLandmark> GetLandmarks(int handIndex);  // 0: 1手目, 1: 2手目
int  DetectedHandCount { get; }
bool? IsLeftHand(int handIndex);                          // null = 未検出／判別不能
```

B2 で `GetCurrentLandmarks()`（1手専用）を廃止し `GetLandmarks(int)` に統一した。3手以上は想定外と割り切って、変更範囲が最小になる案を選んでいる（dev_log_B2 §3-1）。

このインターフェースがあるおかげで、将来 ルールベース判定 → PyTorch/ONNX/Sentis のモデル推論に差し替える余地が残されている（仕様書 §9-5）。

### HandLandmark

```csharp
public readonly struct HandLandmark
{
    public Vector3 Position { get; }  // MediaPipeの正規化座標（0.0〜1.0）のまま保持
    public int Index { get; }
}
```

**正規化座標のまま**扱うのが重要。ピクセル変換していないので、後述の比率判定が効く。

---

## 4. HandTrackingService（中核・484行）

`Input/HandTrackingService.cs`。Pure C# / `IDisposable`。MonoBehaviour を継承しないのでテスト可能な形になっている（実際のテストはまだ無い）。

駆動は外部任せで、`HandSignPresenter.Update()` から毎フレーム `Tick()` が呼ばれる。

### コンストラクタの既定値

```csharp
public HandTrackingService(
    IHandLandmarkProvider provider,
    float bentThreshold      = 45f,   // 人差し指〜小指の曲げ閾値（度）
    float thumbBentThreshold = 20f,   // 親指専用の閾値（度）
    int   stableFrameCount   = 36)    // 確定に必要な連続フレーム数
```

現状これらは全てハードコード。キャリブレーション機能（B6）でScriptableObject化する対象。

### Tick() の分岐

```mermaid
flowchart TD
    T["Tick"] --> Q{"DetectedHandCount"}

    Q -->|"2手以上"| U{"IsUnionPose ?<br/>手首間距離 ÷ 手のひら長 が 0.8 以下"}
    U -->|"Yes"| UN["HandSign.Union"]
    U -->|"No"| S1["Stage 1<br/>左右それぞれ DetectSign"]
    S1 --> HD{"handedness で左右を振り分け"}
    HD -->|"左右不明"| N1["null"]
    HD -->|"振り分け成功"| S2{"Stage 2<br/>TwoHandPatterns を照合"}
    S2 -->|"マッチ"| TWO["両手印<br/>例 DoubleScissors"]
    S2 -->|"マッチ無し"| N2["null<br/>片手印は抑制される"]

    Q -->|"1手"| S1B{"Stage 1<br/>SingleHandPatterns を照合"}
    S1B -->|"マッチ"| ONE["片手印"]
    S1B -->|"マッチ無し"| N3["null"]

    Q -->|"0手"| N4["null"]

    UN --> JS["JudgeStability"]
    TWO --> JS
    ONE --> JS
    N1 --> JS
    N2 --> JS
    N3 --> JS
    N4 --> JS
```

> **設計意図:** 2手検出中は**意図的に片手印を抑制する**。両手を写しているのに片方だけ拾って誤爆する、を防ぐため。結果として「手が2つ映っているが両手印ではない」状態は `null` になり、安定判定には届かない。

---

## 5. Stage 1：片手の判定

### 指の曲げ判定（`IsFingerBent`）

3点のなす角が閾値を超えていたら「曲がっている」。

| 指 | 使うランドマーク | 閾値 |
|---|---|---|
| 人差し指 | MCP(5) - PIP(6) - DIP(7) | 45° |
| 中指 | MCP(9) - PIP(10) - DIP(11) | 45° |
| 薬指 | MCP(13) - PIP(14) - DIP(15) | 45° |
| 小指 | MCP(17) - PIP(18) - DIP(19) | 45° |
| **親指** | **CMC(1) - MCP(2) - IP(3)** | **20°** |

```csharp
var vectorA = pip - mcp;
var vectorB = dip - pip;
var angle   = Vector3.Angle(vectorA, vectorB);
return angle > (threshold ?? _bentThreshold);
```

> **設計意図:** 親指は関節構造が他4指と違い可動域が狭く、45°では一度も「曲がっている」と判定されない。A3 で `ThumbAngleDebugger` を使って実測したところ **Open時 約10° / Fist時 約26° / Palm時 約38°** だったため、閾値20°を採用して14°以上のマージンを確保した（dev_log_A3 §3-1）。P2〜A1 の間、親指は判定から除外されていた。

### パターンテーブル（`SingleHandPatterns`）

5指の曲げ状態（O = 伸び / X = 曲げ）の組み合わせで印を決める。**上から順に照合し、最初にマッチしたものを返す**（＝配列順がそのまま優先度）。

| 印 | 親指 | 人差し | 中 | 薬 | 小 | 種別 |
|---|---|---|---|---|---|---|
| `Open`（開） | O | O | O | O | O | 詠唱印 |
| `Fist`（握） | X | X | X | X | X | 詠唱印 |
| `Guard`（盾） | O | X | X | X | X | 特殊（ガード） |
| `Point`（指） | X | O | X | X | X | 詠唱印 |
| `Scissors`（刃） | X | O | O | X | X | 詠唱印 |
| `Palm`（掌） | X | O | O | O | O | 詠唱印 |
| `Release`（射） | X | O | X | X | O | 特殊（発動） |
| `Cancel`（散） | X | X | X | X | O | 特殊（解除） |

`SingleHandPattern` の各指は `bool?` 型で、`null` はワイルドカード（どちらでもマッチ）。現行8パターンでは未使用だが、「親指はどちらでも良い」のような緩い定義に将来対応できる。

> **設計意図:** B2 Step 3 で `if` の羅列からテーブル駆動に全面リファクタした。**新しい印の追加が配列への1行追加で完結する**ことが狙い（dev_log_B2 §2 Step3）。

---

## 6. Stage 2：両手の判定

### 合掌（Union）判定 — 別枠の専用ロジック

`IsUnionPose()` は指の形を見ない。**両手の手首がどれだけ近いか**だけを見る。

```csharp
palmLength    = |landmark[0] - landmark[9]|            // 1手目の手のひら長（手首→中指付け根）
wristDistance = |hand0[0] - hand1[0]|                  // 両手の手首間距離
return wristDistance / palmLength <= 0.8f;             // UnionWristDistanceRatio
```

> **設計意図:** MediaPipe の座標は**画面に対する正規化座標**なので、カメラに近づくと手が大きく映り、すべての距離が伸びる。生の距離で閾値を切るとカメラ距離依存になってしまう。そこで**手のひら長をスケール基準にした比率**で判定し、距離非依存にしている（dev_log_B2 §3）。閾値 `0.8f` は実機調整前の仮値で、β版でScriptableObject化する候補。

### 10本指パターン（`TwoHandPatterns`）

Stage 1 で左右それぞれ片手判定した結果を組み合わせる。左右の振り分けには MediaPipe の handedness（`IsLeftHand`）を使い、**不明なら判定を諦めて `null`**。

```csharp
new TwoHandPattern(HandSign.Scissors, HandSign.Scissors, HandSign.DoubleScissors),
```

現在**この1行だけ**。`TwoHandPattern` の左右も `bool?` ならぬ `HandSign?` のワイルドカード対応済みなので、「左は何でもいい」も書ける。ここが今後の両手印の拡張ポイント。

---

## 7. 安定判定とラッチ（`JudgeStability`）

認識結果のチラつきを吸収して「確定」に変える最後の関門。

```mermaid
flowchart TD
    IN["currentSign を受け取る"] --> Q1{"currentSign は null ?"}
    Q1 -->|"Yes"| R1["何もせず return<br/>状態に一切触れない"]
    Q1 -->|"No"| Q2{"前フレームと同じ印 ?"}
    Q2 -->|"No = 印が変わった"| RESET["_lastSign を更新<br/>_stableCount = 1<br/>_isConfirmed = false"]
    Q2 -->|"Yes"| Q3{"_isConfirmed ?"}
    Q3 -->|"Yes = 確定済み"| R2["return<br/>同じ印を保持し続けても再発火しない"]
    Q3 -->|"No"| INC["_stableCount++"]
    INC --> Q4{"_stableCount が 36 に到達 ?"}
    Q4 -->|"No"| WAIT["次フレームを待つ"]
    Q4 -->|"Yes"| FIRE["_isConfirmed = true<br/>OnHandSignRecognized を発火"]
```

ポイントは2つ。

**① null を「何もしない」扱いにしている**
「手が映っていない」「どのパターンにも一致しない」「両手検出中で片手印を抑制した」は全て `null` として届く。ここでカウンタをリセットしてしまうと、認識が1フレーム途切れただけで積み上げが台無しになる。**状態に触れずに早期returnする**ことで、手が一瞬消えても直前の確定状態を保ったまま継続できる（dev_log_P3 の設計判断をそのまま継承）。

**② `_isConfirmed` によるラッチ**
一度確定した印は、**別の印に変わるまで二度と発火しない**。これが無いと36フレーム目以降も毎フレーム発火して、印を1つ組んだだけでシーケンスが詰まる。

36フレーム = 60fps で約0.6秒。仕様書 §5-3 は24フレーム（0.4秒）と記載しており、**実装のほうが長い**。体感調整の余地がある箇所。

出力は R3 の `Subject<HandSign>`：

```csharp
public Observable<HandSign> OnHandSignRecognized => _onHandSignRecognized;
```

---

## 8. HandSign 一覧

`Input/HandSignEnum.cs`。**グローバル名前空間**に置かれている点に注意。

| 値 | 名前 | 和名 | 形 | 用途 |
|---|---|---|---|---|
| 0 | `Open` | 壱印「開」 | パー（全指伸び） | 詠唱印 |
| 1 | `Fist` | 弐印「握」 | グー（全指曲げ） | 詠唱印 |
| 2 | `Point` | 参印「指」 | 人差し指のみ伸ばす | 詠唱印 |
| 3 | `Release` | 発動印「射」 | 人差し指 + 小指を伸ばす | **特殊** — シーケンスを発動 |
| 4 | `Cancel` | 解除印「散」 | 小指のみ伸ばす | **特殊** — シーケンスをリセット |
| 5 | `Scissors` | 肆印「刃」 | チョキ | 詠唱印 |
| 6 | `Palm` | 伍印「掌」 | 親指だけ折る | 詠唱印 |
| 7 | `Guard` | 捌印「盾」 | 親指のみ伸ばす | **特殊** — ガード受付窓を開く |
| 8 | `Union` | 陸印「合」 | 両手を合わせる | 両手印・詠唱印 |
| 9 | `DoubleScissors` | 「刃」二つ | 両手チョキ | 両手印・詠唱印 |

`Release` / `Cancel` / `Guard` に**意味を与えるのは `HandTrackingService` ではない**。ここは「どの形か」を返すだけで、それが発動なのか解除なのかの解釈は `HandSignPresenter.HandleSignConfirmed()` が行う（仕様書 §5-3）。

> **重要:** 値は `SpellData.sequence` に**シリアライズ済み**。既存値の変更・並べ替えは全アセットを壊す。追加は必ず末尾に。

---

## 9. デバッグ／検証用スクリプト

| スクリプト | 役割 | 状態 |
|---|---|---|
| `Debug/DebugKeyboardInput.cs` | キーボードで `HandSign` を直接発火。カメラ無しで全機能テスト（B1） | シーンに存在するが `HandSignPresenter` 側が**未割り当て**＝無効 |
| `Debug/DebugMenuView.cs` | F1でOnGUIオーバーレイ。HP操作・StatusEffect付与・無敵・FPS（B1） | 有効 |
| `Debug/ThumbAngleDebugger.cs` | 親指の角度を実測ログ出力。A3の閾値20°はこれで決めた | シーン未配置（役目を終えた一時スクリプト） |
| `Input/HandTracking/TempHandTrackingRunner.cs` | A1以前の旧ドライバ。**自前で `HandTrackingService` を生成**して `Debug.Log` するだけ | **シーンで有効** |
| `Debug/HandTrackingActiveTest.cs` | 毎フレーム検出手数をログ出力。クラス名は `Test`（ファイル名と不一致） | **シーンで有効** |

---

## 10. 気をつける点

現コードを読んだ上での事実メモ。バグ報告ではなく、読むときに混乱しないための注記。

- **`TempHandTrackingRunner` と `HandTrackingActiveTest` が InGame.unity で有効になっている。** 前者は `HandSignPresenter` とは**別の `HandTrackingService` インスタンス**を生成して毎フレーム `Tick()` している（＝認識処理が二重に走り、「確定: Xxx」ログがもう1系統出る）。後者は毎フレーム `Debug.Log` する。どちらも本番経路ではないので、整理する場合は両方オフにしてよい
- **`FingerState` 構造体はどこからも参照されていない。** 角度も返す設計だったが、現在の `IsFingerBent` は `bool` を直接返している
- `HandSignPresenter` のシーン上の `m_EditorClassIdentifier` が `SpellSequenceRunner` のまま（B1のリネーム前の名残）。動作には影響しない
- 安定判定のフレーム数は 36 だが、仕様書 §5-3 の記述は 24。どちらに寄せるかは未決
