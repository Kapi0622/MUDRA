# 📋 開発ログ：α版 A1（アーキテクチャ構築）

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** α版 A1
> **期間:** 2026/07/08 〜 2026/07/11
> **ステータス:** ✅ 完了

---

## 1. マイルストーン概要

| 項目 | 内容 |
|------|------|
| **完了条件** | State管理・MVP構成・ScriptableObject基盤が動作する |
| **開発方針** | Prototype期の技術的負債解消が主目的。新機能追加ではなくアーキテクチャの正式化に専念 |
| **作業シーン** | `Hand Landmark Detection`（MediaPipeUnityPlugin付属サンプル）を複製・リネームして使用 |

### 対応した技術的負債（dev_log_P3.md 6章からの引き継ぎ）

| # | 負債 | 対応状況 |
|---|------|---------|
| 1 | `HandSignDetector`のロジックが`HandTrackingService`（Pure C#）へ未移植 | ✅ 完了 |
| 2 | `HandLandmarkerRunner.cs`への直接追記 | ✅ `MediaPipeHandLandmarkProvider`として切り出し完了 |
| 3 | スレッド間受け渡しが`volatile`の簡易実装 | ✅ `UniTask.SwitchToMainThread()`へ置き換え完了 |
| 4 | `TargetSequence`のハードコード | ✅ `SpellData`（ScriptableObject）へ移行完了 |
| 5 | `SpellSequenceTracker`が単一シーケンス専用 | ✅ 複数候補対応（`matchCandidates`方式）へ拡張完了 |
| 6 | 親指の曲げ判定が未実装 | 🔜 **A3へ延期**（スコープ調整、後述） |
| 7 | `PlayerStateManager`未実装との統合 | ✅ 完了（Idle/Chanting/Releasingの3状態） |
| 8 | R3への通知方式の置き換え | ✅ 完了 |

親指判定（項目6）はA1着手時のスコープ協議でA3へ延期を決定。理由は4-1章参照。

---

## 2. 実装ファイル一覧

### Input Layer

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `IHandLandmarkProvider.cs` | 新規作成 | ランドマーク座標提供のインターフェース |
| `MediaPipeHandLandmarkProvider.cs` | 新規作成 | MediaPipeからのランドマーク取得・キャッシュ（MonoBehaviour） |
| `HandTrackingService.cs` | 新規作成 | 曲げ判定・手印識別・安定判定（Pure C#） |
| `HandLandmark.cs` / `FingerState.cs` | 新規作成 | 補助struct |

### Model Layer

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `SpellSequenceModel.cs` | `SpellSequenceTracker`からリネーム＋拡張 | 複数`SpellData`の前方一致判定 |
| `SequenceResetReason.cs` | 既存流用 | リセット理由enum |
| `PlayerStateManager.cs` | 新規作成 | Idle/Chanting/Releasingの状態管理 |
| `PlayerPhase.cs` | 新規作成 | フェーズenum |
| `IPlayerState.cs` / `IdleState.cs` / `ChantingState.cs` / `ReleasingState.cs` | 新規作成 | Stateパターン実装 |

### Presenter Layer

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `SpellSequenceRunner.cs` | 既存流用（拡張） | 各Modelの駆動・配線役。将来`HandSignPresenter`へ発展予定 |

### View Layer

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `SpellEffectView.cs` | 既存流用 | パーティクルエフェクト再生 |

### Data Layer

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `SpellData.cs` | 新規作成 | 術定義データ（ScriptableObject） |
| `SpellEnums.cs` | 新規作成 | `ElementType` / `DamageType` / `AttackRangeType` / `StatusEffectType` |

### Debug

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `TempHandTrackingRunner.cs` | 既存流用 | Input Layer単体の動作確認用 |

---

## 3. アーキテクチャ変遷

### 3-1. データフロー（P3版 → A1版）

**P3版:**
```
HandLandmarkerRunner（static event）
    ↓
HandSignDetector（識別＋安定判定＋通知を1クラスに同居）
    ↓ event Action
SpellSequenceRunner → SpellSequenceTracker（単一シーケンス）
    ↓
SpellEffectView
```

**A1版:**
```
HandLandmarkerRunner
    ↓
MediaPipeHandLandmarkProvider（Pull型キャッシュ、MonoBehaviour）
    ↓ IHandLandmarkProvider経由
HandTrackingService（識別＋安定判定、Pure C#）
    ↓ Observable<HandSign>
SpellSequenceRunner（駆動役）
    ├→ SpellSequenceModel（複数候補対応、Pure C#）
    │       ├→ SpellEffectView
    │       └→ PlayerStateManager（Idle/Chanting/Releasing）
    └→ デバッグ用テキスト表示
```

識別ロジックと通知の責務を分離し、Push型（event）からPull型（Provider）＋R3（Observable）の構成に整理した。

### 3-2. フォルダ構成

仕様書1-2のレイヤー構成に対応させ、`Assets/MUDRA/Scripts/`配下を`Input` / `Model`（`State`サブフォルダ含む） / `Presenter` / `View` / `Data` / `Debug`に再編した。

---

## 4. 設計判断の記録

### 4-1. 親指判定をA1からA3へ延期

親指は他4指と曲げ軸が異なり判定ロジックが複雑化する。A1のゴールは「アーキテクチャの動作確認」であり、手印の種類数はスコープ外と判断。Open/Fist/Pointの3種のみでアーキテクチャ検証を行う方針とした。

### 4-2. Input LayerはPull型を採用

仕様書2-2の`IHandLandmarkProvider`定義に準拠し、Push型（event通知）ではなくPull型（Provider内キャッシュ→Service側が取得）を採用。スレッド分離をProvider内部に閉じ込められ、呼び出し側は同期的にアクセスできる利点がある。

### 4-3. 識別と安定判定は同一クラスに配置

安定判定（Nフレーム連続で確定）は「識別を確定させる」プロセスの一部と捉え、`HandTrackingService`内に含めた。別クラスに分離すると状態のバケツリレーが発生し複雑化するため。

### 4-4. HandTrackingServiceの駆動源

Pure C#のためMonoBehaviourの`Update`を持てない。Providerが自らServiceを駆動する形は依存の向きが逆転するため避け、Presenter相当（`SpellSequenceRunner`）が`Tick()`を毎フレーム呼ぶ駆動役を担う設計とした。

### 4-5. SpellData導入と複数候補対応を別ステップに分離

1ステップあたりの変更範囲を絞り、動作確認をしやすくするため、「ハードコード配列をSpellData 1件に置き換える」作業と「複数候補対応に拡張する」作業を分けて進めた。

### 4-6. 複数候補の前方一致フィルタ方式（matchCandidates）

全`SpellData`を初期候補とし、詠唱印が1つ追加されるたびに「その位置のシーケンス値が一致するか」で候補を絞り込む方式を採用。`Release`時に「入力履歴と長さが一致する候補」を探すことで完全一致判定を行う。毎回全件走査するより効率的。

### 4-7. SpellSequenceModelとPlayerStateManagerの疎結合設計

状態遷移のトリガー検知は、ポーリングではなくイベント駆動で統一。`SpellSequenceModel`に`OnChantStarted`（詠唱1手目を検知）を新設し、`PlayerStateManager`側は受け取ったイベントに応じて遷移を判断する形にした。

### 4-8. PlayerStateManagerはStateパターンで実装

3状態程度の規模であればenum+switchでも成立するが、A2でGuarding状態が加わり文脈判定（Idle中のFist保持とChanting中のFist入力の区別）が複雑化することを見越し、A1の時点で`IPlayerState`によるStateパターンを採用した。

### 4-9. Releasing状態の遷移条件を変更（仕様書更新）

仕様書4-2は当初「エフェクト再生完了」で`Releasing→Idle`と定義していたが、これだとエフェクト尺が伸びるほど硬直時間が延び、演出とゲームプレイのテンポが密結合してしまう。「発動後の固定硬直時間（0.25秒）が経過」で自動的にIdleへ戻る方式に変更し、仕様書を更新した。

### 4-10. R3導入：Subject/ReactivePropertyの使い分け

「発生した瞬間を伝えるだけの通知」（`OnSpellCast`等）は`Subject<T>`、「現在の値を保持し続ける必要がある状態」（`PlayerStateManager`の現在フェーズ）は`ReactiveProperty<T>`を採用。外部公開時はそれぞれ`Observable<T>` / `ReadOnlyReactiveProperty<T>`型で見せ、勝手な発行・書き換えを防ぐ設計とした。

### 4-11. IDisposable対応とCompositeDisposableによる購読管理

`Subject` / `ReactiveProperty`はリソース解放が必要なため、Pure C#クラス（`HandTrackingService` / `SpellSequenceModel` / `PlayerStateManager`）に`IDisposable`を実装。購読側（`SpellSequenceRunner`）は`CompositeDisposable`で全`Subscribe`をまとめ、`OnDestroy`で「購読解除→発行元のDispose」の順に後始末する構成とした。

---

## 5. 実機テストで判明した問題と対処

| 問題 | 原因 | 対処 |
|------|------|------|
| `HandLandmarkerRunner`が解決できない | namespaceの`using`漏れ | `using Mediapipe.Unity.Sample.HandLandmarkDetection;`を追記 |
| `[SerializeField]`のスロットがInspectorに表示されない | `HandTrackingService`（Pure C#）にSerializeFieldを指定していた | MonoBehaviourである`Provider`のみSerializeFieldとし、Serviceはコード内で`new`する構成に修正 |
| `DetectSign`移植時に指の曲げ状態変数が未定義 | 移植元は`ProcessResult`側で変数化していたが、移植先ではその工程が欠けていた | `IsFingerBent`呼び出しをDetectSign冒頭にまとめて追加 |

---

## 6. 技術的負債の解消状況

P3から引き継いだ8項目のうち7項目を解消。親指判定（項目6）のみA3へ延期。

新たに認識した負債・保留事項：

- `DebugHandSignView`相当の手印テキスト表示は、`SpellSequenceRunner`に間借りする形で実装。将来ゲーム本編用の補助UIとして育てる際は独立クラスへの分離を検討
- `SpellSequenceRunner`は将来`HandSignPresenter`へのリネーム・整理が必要
- namespaceとフォルダ構成が完全には一致していない箇所が残る可能性あり（要確認）

---

## 7. α版A2への引継ぎ

**A2完了条件:** ボスが自動攻撃し、プレイヤーが術で反撃でき、HP増減でバトルが決着する

### 前提として把握しておくべきこと

- 手印確定〜術発動〜状態遷移までの経路は `HandTrackingService` → `SpellSequenceModel` → `PlayerStateManager` の3層でR3による疎通が確立済み
- `SpellData`はフル定義済みだが、Inspectorで実際に値を入れているのは`spellName`と`sequence`のみ。`element` / `damageType` / `basePower`等はA2で初めて活用される
- `Releasing`状態は固定硬直時間（0.25秒）で自動的にIdleへ戻る設計。エフェクト実尺との連動はしていない

### A2で新たに必要になるもの

- `BattleModel`（HP管理・ダメージ計算Strategy呼び出し）
- `EnemyStateManager`（Idle/Charging/Attacking/Stunned）
- `IDamageCalculator`実装群（`SingleHitCalculator` / `MultiHitCalculator` / `DamageOverTimeCalculator`）
- `BattlePresenter` / `EnemyPresenter`の新規実装
- `SpellData`の`element` / `damageType` / `basePower`等、未使用フィールドへの実データ投入
- `PlayerStateManager`へのGuarding状態追加（Idle中のFist保持文脈判定を含む）
