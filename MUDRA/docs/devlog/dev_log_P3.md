# 📋 開発ログ：Prototype P3

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** Prototype P3
> **期間:** 2026/07/04 〜 2026/07/08
> **ステータス:** ✅ 完了

---

## 1. マイルストーン概要

| 項目 | 内容 |
|------|------|
| **完了条件** | 1印 + 発動印でパーティクルエフェクトが再生される。解除印でキャンセルできる |
| **開発方針** | P2と同様、最短経路で実装。アーキテクチャ整備はα版に委ねる。ただし可読性維持のため責務ごとにファイルは分離 |
| **作業シーン** | `Hand Landmark Detection`（MediaPipeUnityPlugin 付属サンプル） |

---

## 2. 実装ファイル一覧

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `HandSignDetector.cs` | 既存ファイルに追記 | 手印識別（Release/Cancel追加）＋安定判定＋確定通知 |
| `SpellSequenceTracker.cs` | 新規作成 | 入力履歴の管理・シーケンス一致判定（Pure C#） |
| `SpellSequenceRunner.cs` | 新規作成 | HandSignDetectorとSpellSequenceTrackerの配線・振り分け役（MonoBehaviour） |
| `SpellEffectView.cs` | 新規作成 | パーティクルエフェクトの再生専用View |

---

## 3. データフロー

```
HandLandmarkerRunner.OnLandmarkDetected（P2から継承）
    ↓
HandSignDetector.ProcessResult()
    ↓ isHandDetected（bool）と currentSign（HandSign?）を算出
    ├─ UpdateText()          … デバッグ表示（No Hand / Unknown / 各手印）
    └─ JudgeStability()      … 安定判定（「無」は判定対象外）
            ↓ 確定
        OnSignConfirmed（HandSign を通知）
            ↓
SpellSequenceRunner.HandleSignConfirmed()
    ↓ Release / Cancel / その他（詠唱印）に振り分け
    ├─ Release → SpellSequenceTracker.Release()
    │       ├─ 一致 → OnSpellCast → SpellEffectView.PlayEffect()
    │       └─ 不一致・空Queue → OnSequenceReset(Misfire)
    ├─ Cancel  → SpellSequenceTracker.Cancel() → OnSequenceReset(Cancel)
    └─ 詠唱印  → SpellSequenceTracker.AddSign()（inputHistoryに追加）
```

---

## 4. 設計判断の記録

### 4-1. 安定判定は「手印が変わるまで再確定させない」方式を採用

同一手印を保持し続けるだけでNフレームごとに繰り返し確定してしまうと、単に長くポーズを取るだけの入力になり不自然。`_isConfirmed`フラグを追加し、手印が変化するまで再確定させない仕様にした。これに伴い、同一詠唱印を連続要求していた雷撃のシーケンスを変更（仕様書側を更新）。

### 4-2.「無」（手なし／未知の形）は安定判定から一切除外

`JudgeStability`の入口で`currentSign == null`を早期returnし、比較・カウントの対象に含めない。これにより「手を画面外に出しても、それまでの入力履歴を保持したまま戻って続行できる」という挙動が自然に実現される。

### 4-3. `isHandDetected`と`HandSign?`を分離

安定判定側は「無」を一律無視できればよいが、デバッグ表示側は「手が検出されていない」と「手はあるが未知の形」を区別したい要求があった。両方を成立させるため、`ProcessResult`で`isHandDetected`（bool）を別途算出し、`UpdateText`にのみ追加で渡す構成にした。

### 4-4. シーケンス管理をPure C#（`SpellSequenceTracker`）＋配線役MonoBehaviour（`SpellSequenceRunner`）の2クラスに分離

`HandSignDetector`に直接シーケンス管理を追加すると責務が混在し、行数も肥大化する。プロト中でもメンテナンス性を優先し、Model層はPure C#で構成する方針（企画書のアーキテクチャ方針）を先取りする形で分離した。Pure C#クラスは`[SerializeField]`で参照を持てないため、橋渡し役の薄いMonoBehaviourを別途用意した。

### 4-5. Release/Cancelの振り分けは`Tracker`側でなく`Runner`側に持たせた

`Tracker`の入口を`AddSign`/`Release`/`Cancel`の3つに分けることで、`Tracker`内部は「詠唱印を積む」「一致判定する」「リセットする」のみに責務を絞れる。特殊印かどうかの判定は`Runner`側の`HandleSignConfirmed`で行う。

### 4-6. リセット理由（`SequenceResetReason.Cancel` / `Misfire`）を早期に分離

将来的な演出・UI分岐（キャンセル時とミス時で異なるフィードバックを出す想定）を見据え、構造がシンプルなうちに列挙型で分けておく判断をした。空Queueでの発動印入力も一律`Misfire`として扱う（特別扱いする条件分岐のコストの方が高いため）。

### 4-7. エフェクト再生を専用View（`SpellEffectView`）に切り出し

将来「単体攻撃は敵の位置に、全体攻撃は画面全体に」出力先を分岐させる予定があるため、「どこに出すか」の判定はViewの責務外とし、Instantiateする処理のみに責務を絞った。後始末はPrefab側`Stop Action = Destroy`設定に加え、コード側でも一定時間後に`Destroy`を呼ぶ保険を追加（両者の利点を両立）。

---

## 5. 実機テストで判明した問題と対処

| 問題 | 原因 | 対処 |
|------|------|------|
| 発動印を確定させても`OnSpellCast`が発火しない | `SpellSequenceRunner`をシーン上のどのGameObjectにもアタッチしていなかった | `HandSignDetector`と同一GameObjectに仮アタッチ |
| 「発動印のログは出るが発動しない」という誤認 | `UpdateText`内の`Debug.Log`は毎フレーム出力される。確定通知（`OnSignConfirmed`）とは独立した別軸のログだった | ログの出どころを`HandleSignConfirmed`側にも仕込んで発火有無を確認する切り分け方法を共有 |

---

## 6. 技術的負債（α版で対処）

| 負債 | α版での対処方針 |
|------|----------------|
| `TargetSequence`が`SpellSequenceRunner`に`static readonly`でハードコード | `SpellData`（ScriptableObject）から注入する形に置き換え |
| `SpellSequenceTracker`が単一シーケンス専用（前方一致・複数候補チェック未対応） | 複数`SpellData`を扱えるよう`matchCandidates`方式に拡張 |
| Release/Cancelの手形が仮決めのまま（親指未対応・閾値未調整） | 親指判定の実装、キャリブレーション画面での閾値調整に対応 |
| `SpellEffectView`が固定Transformにのみ出力 | 単体/範囲攻撃で出力先（敵位置／画面全体）を分岐できる設計に拡張 |
| `SpellSequenceRunner`のアタッチ先が仮のGameObjectのまま | アーキテクチャ整備時に適切な配置（Presenter層）へ移行 |

---

## 7. α版への引継ぎ

**α版完了条件（A1）:** State管理・MVP構成・ScriptableObject基盤が動作すること

### 前提として把握しておくべきこと

- 手印確定の通知経路は `HandSignDetector.OnSignConfirmed` → `SpellSequenceRunner`（振り分け） → `SpellSequenceTracker`（判定） が確立済み
- シーケンス一致判定・リセット理由の分離・エフェクト再生の3つは、責務ごとにファイル分離済みでα版の該当クラスへの移行はスムーズなはず

### α版で新たに必要になるもの

- `SpellData`（ScriptableObject）への移行と、複数術対応のシーケンス照合
- `HandTrackingService`（Pure C#）への判定ロジック移植と`IHandLandmarkProvider`経由の接続
- `PlayerStateManager`（Idle/Chanting/Releasing/Guarding）との統合
- R3による`Model → Presenter`通知への置き換え（現状はC#標準の`event Action`）
