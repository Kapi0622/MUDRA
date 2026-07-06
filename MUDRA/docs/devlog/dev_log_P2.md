# 📋 開発ログ：Prototype P2

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** Prototype P2
> **期間:** 2026/06/28 〜 2026/06/30
> **ステータス:** ✅ 完了

---

## 1. マイルストーン概要

| 項目 | 内容 |
|------|------|
| **完了条件** | Open / Fist / Point の3手印を画面テキストにリアルタイム表示 |
| **開発方針** | サンプルシーンをベースに最短経路で実装。アーキテクチャ整備はα版に委ねる |
| **作業シーン** | `Hand Landmark Detection`（MediaPipeUnityPlugin 付属サンプル） |

---

## 2. 実装ファイル一覧

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `HandLandmarkerRunner.cs` | **既存ファイルに追記** | `static event` を追加しランドマーク検出結果を外部へ通知 |
| `HandSignDetector.cs` | **新規作成** | イベント購読・指の曲げ角度計算・手印識別・UIテキスト更新 |

### 追記箇所（HandLandmarkerRunner.cs）

```csharp
// フィールド追加
public static event Action<HandLandmarkerResult> OnLandmarkDetected;

// OnHandLandmarkDetectionOutput に1行追加
private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
{
    _handLandmarkerResultAnnotationController.DrawLater(result);
    OnLandmarkDetected?.Invoke(result); // ← 追加
}
```

---

## 3. データフロー

```
MediaPipe（C++内部）
    ↓ 検出結果をコールバックで返す（バックグラウンドスレッド）
HandLandmarkerRunner.OnHandLandmarkDetectionOutput()
    ↓ static event を Invoke
HandSignDetector.OnLandmarkDetected()  ← バックグラウンドスレッドで受信
    ↓ _pendingResult に保存 / _hasNewResult フラグを立てる
HandSignDetector.Update()              ← メインスレッドで毎フレーム処理
    ↓ IsFingerBent() で各指の曲げ角度を計算
    ↓ DetectSign() で手印を識別
Text.text を更新（UI描画）
```

---

## 4. 設計判断の記録

### 4-1. Plugin側への変更を最小限に抑えた

`HandLandmarkerRunner.cs` はMediaPipeUnityPluginのサンプルコード。手印判定ロジックを直接書き込むと責務が混在し、α版での置き換えが困難になる。そのため Plugin 側への変更は `static event` の追加のみに留め、ゲーム固有のロジックはすべて `HandSignDetector.cs` に分離した。

### 4-2. 繋ぎに `static event` / `Action` を採用した

接続方式の候補として以下の3つを検討した。

| 方式 | 採用 | 理由 |
|------|------|------|
| `static event` / `Action` | ✅ | Plugin側インスタンス参照不要。Pure C#への橋渡しに自然 |
| `UnityEvent` | ❌ | Inspector依存でMonoBehaviour縛りが強まる |
| 直接参照 | ❌ | Plugin側がMUDRAコードに依存し責務が逆転する |

### 4-3. スレッド問題への対処（volatile フラグ方式）

MediaPipeのコールバックはバックグラウンドスレッドから呼ばれる。`Text.text` はメインスレッドでしか更新できないため、直接UIを操作するとUnityExceptionが発生する。

**対処:** コールバック内では結果を `_pendingResult` に保存するだけに留め、`Update()` でフラグを確認してからUI更新を行うことでスレッドを分離した。

```csharp
// バックグラウンドスレッド（受け取るだけ）
private void OnLandmarkDetected(HandLandmarkerResult result)
{
    _pendingResult = result;
    _hasNewResult = true;  // volatile フラグ
}

// メインスレッド（UI更新）
private void Update()
{
    if (!_hasNewResult) return;
    _hasNewResult = false;
    ProcessResult(_pendingResult);
}
```

### 4-4. 親指を判定から除外した

親指は他の4指と曲げの軸が異なり、同一ロジックでは誤判定が多発する。Open / Fist / Point の識別は親指なしでも十分成立するため、P2スコープでは除外した。

---

## 5. 技術的負債（α版で対処）

| 負債 | α版での対処方針 |
|------|----------------|
| `HandLandmarkerRunner.cs` に直接追記している | `MediaPipeHandLandmarkProvider`（MonoBehaviour）として切り出す |
| 判定ロジックが `HandSignDetector` に混在 | `HandTrackingService`（Pure C#）に移植し `IHandLandmarkProvider` 経由で接続 |
| スレッド間受け渡しが `volatile` の簡易実装 | `UniTask.SwitchToMainThread()` に置き換え |
| 親指の曲げ判定が未実装 | 軸補正ロジックを追加し全手印に対応 |
| 安定判定（確定ラッチ）が未実装 | 一定フレーム同一手印が続いた場合のみ確定とする安定化処理を追加 |
| 閾値 `BentThreshold = 45f` がハードコード | キャリブレーション画面でユーザー調整可能にする |

---

## 6. P3への引継ぎ

**P3完了条件:** 1術発動の最小デモ（手印シーケンスを入力して術エフェクトが出る）

### 前提として把握しておくべきこと

- 手印データは `HandLandmarkerResult.handLandmarks[0].landmarks` から `List<NormalizedLandmark>` として取得できる
- ランドマーク座標は正規化済み（0.0〜1.0）。角度計算は `Vector3.Angle()` で対応可能
- MediaPipeコールバックはバックグラウンドスレッドで呼ばれる。UI操作は必ずメインスレッドで行うこと

### P3で新たに必要になるもの

- 安定判定（同一手印をNフレーム継続検出で確定）
- 手印シーケンスの管理（Open → Fist → Point のような入力列を保持）
- 術発動トリガーと簡易エフェクト表示
