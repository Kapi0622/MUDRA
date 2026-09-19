# 🗺️ アーキテクチャ概観：MUDRA

> **ドキュメント種別:** アーキテクチャ復習ドキュメント
> **作成日:** 2026/09/18
> **対象コミット:** `8f0e81e`（feat:B2 片手印同士を組み合わせた両手印の実装とテスト）
> **対象コード:** `Assets/_MUDRA/Scripts`（45ファイル / 約3,070行）
> **ステータス:** 現状スナップショット

---

## 0. このドキュメントの位置づけ

`docs/` には3種類のドキュメントがある。役割が違うので混ぜて読まないこと。

| ドキュメント | 役割 | 時制 |
|---|---|---|
| `specification_MUDRA.md`（v1.3） | **設計の正典。** 仕様駆動開発の基準であり、これから作るものも含む | 未来を含む |
| `devlog/dev_log_*.md` | 各マイルストーン完了時点の実装記録と設計判断の理由 | 当時の過去 |
| **本書（`architecture/`）** | **いま動いているコードの断面図。** どこに何があるかを思い出すための地図 | 現在 |

仕様書に書いてあるが未実装のものは [§8 仕様書とのギャップ](#8-仕様書とのギャップ簡易) にまとめてある。

- [01_hand_input.md](01_hand_input.md) — 手印認識パイプライン（Input層）の詳解
- [02_battle_system.md](02_battle_system.md) — Model / Presenter / View / Data の詳解

---

## 1. 技術スタック

出典: `MUDRA/Packages/manifest.json`, `MUDRA/Assets/packages.config`

| 技術 | 用途 |
|---|---|
| Unity 6 + URP（2D） | 実行環境。ビジュアル方針は2Dで確定（B1） |
| MediaPipeUnityPlugin | Webカメラからの手ランドマーク検出（21点 × 最大2手） |
| **R3**（NuGetForUnity経由） | Model → Presenter の通知。`ReactiveProperty` / `Subject` / `Observable` |
| **UniTask** | 非同期タイマー全般。敵行動ループ、硬直タイマー、スレッド切替 |
| **LitMotion** | View側のトゥイーン演出（HPバー、テロップ、確定エフェクト） |
| TextMeshPro | UIテキスト（日本語フォントは NotoSansJP SDF） |

DIコンテナは**使っていない**。手書きの Composition Root（`BattleInitializer`）で全部組み立てる。

---

## 2. フォルダ構成

プロジェクト固有のコードは `Assets/_MUDRA/` 配下のみ。`Assets/` 直下のそれ以外（`MediaPipeUnity/`, `TextMesh Pro/`, `Packages/`）はサードパーティ。

```
Assets/_MUDRA/
├── Scenes/InGame.unity          … 唯一のシーン
├── ScriptableObject/            … 術・敵の定義アセット
├── Prefabs/ Particle/ Sprites/ Fonts/
└── Scripts/
    ├── Data/                    … ScriptableObject定義とenum
    │   SpellData / EnemyData / EnemyAttackData / EnemyAction / SpellEnums
    ├── Input/                   … 手印認識（namespace MUDRA.HandTracking）
    │   IHandLandmarkProvider / MediaPipeHandLandmarkProvider
    │   HandTrackingService(484行・中核) / HandLandmark / FingerState / HandSignEnum
    │   └── HandTracking/TempHandTrackingRunner … 旧ドライバ（本番経路ではない）
    ├── Model/                   … Pure C#。Unity非依存のゲームロジック
    │   SpellSequenceModel / BattleModel / SpellCastResult
    │   PlayerStateManager+PlayerPhase / EnemyStateManager+EnemyPhase
    │   ├── State/               … プレイヤーのStateパターン（Idle/Chanting/Releasing）
    │   ├── StatusEffect/        … 時限効果（DoT/Stun）とガード受付窓
    │   └── Strategy/            … ダメージ計算3種
    ├── Presenter/               … Model購読 → View呼び出しの配線のみ
    │   BattleInitializer(起動) / HandSignPresenter / BattlePresenter
    ├── View/                    … 表示専用MonoBehaviour。Modelを知らない
    │   HpBarView / SequenceGuideView / SpellTelopView / SpellEffectView
    └── Debug/                   … #if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugKeyboardInput / DebugMenuView / ThumbAngleDebugger / HandTrackingActiveTest
```

`.asmdef` は無く、全コードが `Assembly-CSharp` にコンパイルされる。**自動テストは存在しない**（`Debug/HandTrackingActiveTest.cs` はテストではなく毎フレームのログ出力）。

---

## 3. レイヤーと依存方向

```mermaid
flowchart LR
    DATA["Data<br/>ScriptableObject<br/>全層から参照される"]
    INPUT["Input<br/>手印認識<br/>MUDRA.HandTracking"]
    MODEL["Model<br/>Pure C# + R3<br/>Unity API 非依存"]
    PRES["Presenter<br/>MonoBehaviour<br/>購読の配線のみ"]
    VIEW["View<br/>MonoBehaviour<br/>表示専用・Modelを知らない"]

    PRES -->|"毎フレーム Tick を駆動"| INPUT
    INPUT -->|"確定した HandSign"| MODEL
    PRES -->|"メソッド呼び出し"| MODEL
    MODEL -.->|"R3 Observable で通知"| PRES
    PRES -->|"表示指示"| VIEW
    DATA -.-> INPUT
    DATA -.-> MODEL
    DATA -.-> PRES
```

| レイヤー | 規約 |
|---|---|
| **Input** | 唯一のMonoBehaviourは `MediaPipeHandLandmarkProvider`。`HandTrackingService` はPure C# |
| **Model** | **MonoBehaviour非継承・Unity API不使用**。テスト容易性のための方針（A1） |
| **Presenter** | ロジックを持たない。購読の配線と、確定した値をViewのメソッドに渡すだけ |
| **View** | Modelを参照しない。`SetHp(int,int)` のような命令的メソッドを公開するだけの受け身 |

**通知方向は常に Model → Presenter（R3購読）→ View（メソッド呼び出し）。** 逆流はしない。

---

## 4. 起動シーケンス

`BattleInitializer.Awake()`（`Presenter/BattleInitializer.cs:39`）が Composition Root。シーンに1つだけ置く。

1. **Model 7種を生成**
   `HandTrackingService(_provider)` / `SpellSequenceModel(_allSpells, () => Time.time)` /
   `PlayerStateManager()` / `EnemyStateManager(_enemyData)` / `BattleModel(_playerMaxHp, _enemyData)` /
   `GuardWindowManager()` / `StatusEffectManager()`
2. **循環依存の解消** — `StatusEffectFactory` に `BattleModel.ApplyDotDamage` / `EnemyStateManager.ApplyStun` / `EndStun` を **`Action` として**渡し、`BattleModel.SetStatusEffectDependencies()` で後から注入する。コンストラクタでは BattleModel ↔ EnemyStateManager が相互に必要になってしまうため（A5）
3. **Presenter へ注入** — `HandSignPresenter.Initialize(...)` / `BattlePresenter.Initialize(...)`
4. **デバッグメニュー注入** — `FindFirstObjectByType<DebugMenuView>()` に `Inject(...)`（`#if UNITY_EDITOR || DEVELOPMENT_BUILD`）
5. **バトル開始** — `_enemyStateManager.StartLoop()`
6. **終了時の後片付けを購読** — `OnBattleEnd` → `StatusEffectManager.ClearAll()` → `EnemyStateManager.StopLoop()`（この順序は意図的。Stun解除が走っても最後に必ず止まるようにする）

`OnDestroy()` で `CompositeDisposable` と全Modelを `Dispose()` する。シングルトン・static は**プロジェクトコードには存在しない**（唯一の例外はMediaPipe側のstatic event、[01](01_hand_input.md) 参照）。

---

## 5. データフロー全体図

```mermaid
flowchart TD
    CAM["Webカメラ"]
    MP["MediaPipe HandLandmarkerRunner<br/>バックグラウンドスレッド<br/>プラグインを改変して event を追加"]
    PROV["MediaPipeHandLandmarkProvider<br/>UniTask.SwitchToMainThread<br/>2手 × 21点をキャッシュ"]
    TICK["HandSignPresenter.Update<br/>→ HandTrackingService.Tick"]
    CONF{"確定した HandSign"}
    REL["SpellSequenceModel.Release"]
    CAN["SpellSequenceModel.Cancel"]
    GRD["GuardWindowManager.Activate"]
    ADD["SpellSequenceModel.AddSign<br/>前方一致で候補を絞る"]
    GUIDE["SequenceGuideView<br/>候補リスト更新 + 確定エフェクト"]
    CAST["OnSpellCast<br/>成功・暴発を1つの型に統合"]
    HSP2["HandSignPresenter<br/>術エフェクト / テロップ / ガイドClear<br/>PlayerStateManager 遷移"]
    BP["BattlePresenter"]
    BM["BattleModel.ApplySpellDamage<br/>IDamageCalculator で計算"]
    HPB["BossHp 減少 / コンボ++ / StatusEffect 付与"]
    ESM["EnemyStateManager<br/>Charging → Attacking → Idle"]
    EDMG["BattleModel.ApplyEnemyDamage<br/>PlayerHp 減少"]
    CHK{"どちらかの HP が 0 ?"}
    BAR["HpBarView.SetHp<br/>2層バーのアニメーション"]
    END["BattleInitializer<br/>StatusEffectManager.ClearAll<br/>EnemyStateManager.StopLoop"]

    CAM --> MP
    MP -->|"static event OnLandmarkDetected"| PROV
    TICK -->|"毎フレーム pull"| PROV
    TICK --> CONF
    CONF -->|"Release 印"| REL
    CONF -->|"Cancel 印"| CAN
    CONF -->|"Guard 印"| GRD
    CONF -->|"それ以外 = 詠唱印"| ADD
    ADD -->|"OnSignAdded"| GUIDE
    CAN -->|"OnSequenceReset"| GUIDE
    REL --> CAST
    CAST --> HSP2
    CAST --> BP
    BP --> BM
    BM --> HPB
    ESM -->|"OnAttackExecuted"| BP
    BP --> EDMG
    GRD -.->|"IsGuarding で軽減"| EDMG
    HPB -->|"ReactiveProperty 通知"| BAR
    EDMG -->|"ReactiveProperty 通知"| BAR
    HPB --> CHK
    EDMG --> CHK
    CHK -->|"Yes → OnBattleEnd"| END
```

---

## 6. 毎フレーム駆動されているもの

Tick/Update で回るものと、UniTaskタイマーで動くものが明確に分かれている。

| 駆動元 | 呼ぶもの |
|---|---|
| `HandSignPresenter.Update()` | `HandTrackingService.Tick()` — 認識パイプライン全体 |
| `BattlePresenter.Update()` | `StatusEffectManager.Tick(deltaTime)` — DoT/Stunの時間経過 |
| `BattlePresenter.Update()` | `GuardWindowManager.Tick(deltaTime)` — ガード受付窓の残り時間 |
| UniTaskループ | `EnemyStateManager.RunLoopAsync` — 敵の行動サイクル |
| UniTaskタイマー | `ReleasingState` — 発動後250msの硬直 |

コルーチンと `async void` は使っていない。非同期は全てUniTask（`UniTaskVoid` + `.Forget()`、キャンセルは `CancellationTokenSource`）。

---

## 7. シーンとアセット

### InGame.unity（唯一のシーン）

| GameObject | 載っているもの |
|---|---|
| `Solution` | MediaPipe の `HandLandmarkerRunner`（カメラ入力〜推論） |
| Provider用 | `MediaPipeHandLandmarkProvider` + `TempHandTrackingRunner` + `HandTrackingActiveTest` |
| Presenter用 | `BattleInitializer` / `HandSignPresenter` / `BattlePresenter` |
| Canvas配下 | `HpBarView`×2（プレイヤー/ボス）/ `SequenceGuideView` / `SpellTelopView` / `SpellEffectView` |
| Debug用 | `DebugMenuView` / `DebugKeyboardInput` |

`BattleInitializer` の設定値: 術6種すべて登録 / `_enemyData` = ClayDoll / `_playerMaxHp` = 100。

### 術アセット（`ScriptableObject/Player/`）

| アセット | 術名 | シーケンス | 属性 | 威力 | 計算方式 | 副次効果 |
|---|---|---|---|---|---|---|
| WindBlade | 風刃 | 合 | Wind | 30 | SingleHit | — |
| WaterCrow | 水爪 | 刃 → 握 | Water | 30 | SingleHit | — |
| IceWind | 氷風 | 開 → 指 → 握 | Wind | 50 | SingleHit | — |
| Lightning | 雷連撃 | 指 → 刃 | Thunder | 50 | MultiHit ×5 | Stun 3秒 |
| Fireball | 火炎弾 | 掌 → 握 | Fire | 50 | DoT | DamageOverTime 3秒 |
| CrossBlade | 双刃 | 双（両手チョキ） | Earth | 40 | SingleHit | — |

### 敵アセット（`ScriptableObject/Enemy/`）

`ClayDoll`（泥人形）: MaxHP 200 / 弱点 Wind ×1.5 / 行動パターン = 泥塊投げ(DMG10, 溜め1.5s, 間隔3s) ×3 → 地鳴り(DMG25, 溜め2s, 間隔5s) の4手を巡回。大技(`isHeavy`)は現状どれも未設定。

---

## 8. 仕様書とのギャップ（簡易）

仕様書 v1.3 に設計はあるがコードに存在しないもの。次に作るものの目次として使う。

| 未実装のもの | 仕様書の該当箇所 | 影響 |
|---|---|---|
| `GameStateManager` / `GamePhase` | §4-1 | 画面遷移が無い。InGame直起動のみ（B5） |
| `StageData` / セクション進行 | §2, §7-3 | ボス1体との単発バトルのみ。道中セクション無し（B3） |
| `EnemyPresenter` / `EnemyView` | §2 | 敵の見た目・攻撃演出が無く、`Debug.Log` のみ（B4） |
| キャリブレーション / チュートリアル | §4-1 | しきい値は C# の const 固定（B6） |
| サウンド（`castSE` / `attackSE`） | §3-2 | SpellData/EnemyAttackDataにフィールドはあるが未使用（B7） |
| `StatusEffectType.Slow` | §3-1 | enumにあるが実装クラスが無い |

逆に**仕様書に無く実装が先行している**もの: `HandSign.DoubleScissors`（B2 Step3 の10本指パターン第1号）。仕様書 v1.4 での追記対象。

---

## 9. 開発再開ガイド

### とりあえず動かす

1. `Assets/_MUDRA/Scenes/InGame.unity` を開いて Play
2. Webカメラに手をかざす。36フレーム（約0.6秒）同じ形を保つと印が確定する

### カメラ無しでテストする（推奨）

`HandSignPresenter` の `_debugKeyboardInput` に シーン上の `DebugKeyboardInput` を割り当てる。**現在は未割り当て = 通常のカメラモード**。割り当てると `HandTrackingService.Tick()` は回らなくなり、キー入力が直接 `HandleSignConfirmed` に流れる。

| キー | 印 |
|---|---|
| `1` `2` `3` `4` `5` | 開 / 握 / 指 / 刃 / 掌 |
| `U` | 合（Union） |
| `Space` | 発動印 |
| `Backspace` | 解除印 |
| `G` | ガード |
| `R` | シーンリロード（バトルリセット） |

`F1` でデバッグメニュー（OnGUIオーバーレイ）— HP増減、StatusEffect強制付与、無敵モード、FPS表示。

### 新しい術を追加する

1. Project ビューで右クリック → `Create > MUDRA > SpellData`
2. `sequence` に詠唱印を並べる（発動印 `Release` は**含めない**）
3. `BattleInitializer` の `_allSpells` に登録する

### 新しい印を追加する

1. `Input/HandSignEnum.cs` の **末尾に** 追加（後述の理由で既存値の並べ替えは禁止）
2. `HandTrackingService` の `SingleHandPatterns` または `TwoHandPatterns` に**1行追加**するだけで判定が通る
3. `View/SequenceGuideView.cs` の `GetSignDisplayName()` に表示用の漢字を追加（漏れると「？」になる）
4. キーボードテストしたいなら `Debug/DebugKeyboardInput.cs` にキーを追加

> **重要:** `HandSign` の整数値は `SpellData` アセットに**シリアライズ済み**（例: `sequence: 0600000001000000` = Palm, Fist）。既存の値を変更・並べ替えすると全アセットの意味が壊れる。追加は必ず末尾に。

---

## 10. コードを読むときの前提知識

| 慣習 | 内容 |
|---|---|
| **名前空間** | `MUDRA.HandTracking`（Input）/ `MUDRA.Data`（SpellData, SpellEnums）/ `MUDRA.Debugging`（ThumbAngleDebugger）。**それ以外はグローバル名前空間**（Model, Presenter, View, `HandSign`, `EnemyData` 等）。統一されていないので `using` が無くても探せば見つかる |
| **R3の使い分け** | イベント = `Subject<T>`、状態 = `ReactiveProperty<T>`。公開時は `Observable<T>` / `ReadOnlyReactiveProperty<T>` に絞る。購読は `CompositeDisposable _disposables` に集約し `OnDestroy` で破棄 |
| **命名** | private field は `_camelCase`、定数はクラス冒頭に `private const` として名前付きで置く（マジックナンバーを本文に埋めない） |
| **コメント** | ほぼ全ての型・publicメンバに日本語のXMLドキュメントコメント。**設計理由とマイルストーン番号（A1〜B2）が書き込まれている**ので、迷ったらまずソースのコメントを読むのが早い |
| **デバッグ分離** | `#if UNITY_EDITOR || DEVELOPMENT_BUILD` でファイル単位（`DebugKeyboardInput.cs`, `DebugMenuView.cs`）またはメンバ単位（`BattleModel.IsInvincible` 等）に囲う |
