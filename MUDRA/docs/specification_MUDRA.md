# 📘 仕様書：「MUDRA」

> **ドキュメント種別:** ゲーム仕様書 **作成日:** 2026/06/28 **最終更新:** 2026/07/12（A3完了時点の実装と同期） **ステータス:** v1.1 **関連:** [企画書モック v0.2](https://claude.ai/chat/game_design_mock_%E5%8D%B0%E8%A1%93%E3%83%90%E3%83%88%E3%83%AB.md) **開発方針:** 仕様駆動開発。本仕様書を実装の拠り所とし、自分の手でコードに落とす。実装中に仕様との齟齬が発生した場合は仕様書を更新し「生きたドキュメント」として運用する。

---

```table-of-contents
title: ## 目次
maxLevel: 2
```

---

## 1. アーキテクチャ概要

### 1-1. レイヤー構成図

```
┌───────────────────────────────────────────────────────────┐
│  Input Layer                                              │
│  ┌─────────────────────┐    ┌──────────────────────────┐  │
│  │ HandLandmarkProvider │───→│ HandTrackingService      │  │
│  │ (MediaPipe Plugin)   │    │ (角度計算・手印識別)       │  │
│  │ MonoBehaviour        │    │ Pure C#                   │  │
│  └─────────────────────┘    └───────────┬──────────────┘  │
└─────────────────────────────────────────┼─────────────────┘
                                          │ HandSign(enum)
┌─────────────────────────────────────────┼─────────────────┐
│  Model Layer (Pure C#)                  ▼                  │
│  ┌──────────────────────┐    ┌──────────────────────────┐  │
│  │ SpellSequenceModel   │    │ BattleModel              │  │
│  │ (キュー管理・術照合)   │───→│ (HP・ダメージ計算)       │  │
│  └──────────────────────┘    └──────────────────────────┘  │
│                                         │                  │
│  ┌──────────────────────┐    ┌──────────────────────────┐  │
│  │ PlayerStateManager   │    │ EnemyStateManager        │  │
│  │ (プレイヤーフェーズ)   │    │ (敵フェーズ)             │  │
│  └──────────────────────┘    └──────────────────────────┘  │
│                                                            │
│  ┌──────────────────────┐                                  │
│  │ GameStateManager     │                                  │
│  │ (ゲーム全体フロー)    │                                  │
│  └──────────────────────┘                                  │
└────────────────────────────────────────┬───────────────────┘
                                         │
┌────────────────────────────────────────┼───────────────────┐
│  Presenter Layer                       │                   │
│  ┌──────────────────────┐    ┌─────────┴────────────────┐  │
│  │ BattlePresenter      │    │ EnemyPresenter           │  │
│  │ HandSignPresenter    │    │ BattleInitializer        │  │
│  │                      │    │ (全Model生成・注入)       │  │
│  └──────────┬───────────┘    └─────────┬────────────────┘  │
└─────────────┼──────────────────────────┼───────────────────┘
              │                          │
┌─────────────┼──────────────────────────┼───────────────────┐
│  View Layer (MonoBehaviour)            │                   │
│  ┌──────────┴───────────┐    ┌─────────┴────────────────┐  │
│  │ BattleView           │    │ EnemyView                │  │
│  │ HandSignView         │    │                          │  │
│  └──────────────────────┘    └──────────────────────────┘  │
└────────────────────────────────────────────────────────────┘
              │
┌─────────────┼──────────────────────────────────────────────┐
│  Data Layer │                                              │
│  ┌──────────┴───────────┐    ┌──────────────────────────┐  │
│  │ SpellData (SO)       │    │ EnemyData (SO)           │  │
│  │ StageData (SO)       │    │ EnemyAttackData (SO)     │  │
│  └──────────────────────┘    └──────────────────────────┘  │
└────────────────────────────────────────────────────────────┘
```

### 1-2. レイヤー責務一覧

|レイヤー|責務|依存ルール|
|---|---|---|
|**Input Layer**|MediaPipeからのランドマーク座標取得、手印識別|Model Layerへ一方向に通知|
|**Model Layer**|ゲームロジック・状態管理。Pure C#で構成|他レイヤーに依存しない|
|**Presenter Layer**|ModelとViewの仲介。ロジック・データを持たない|Model, View両方を参照|
|**View Layer**|UI描画・エフェクト再生・SE再生。MonoBehaviour|Presenterからの指示のみ受ける|
|**Data Layer**|ScriptableObjectによる静的データ保持|他レイヤーから参照される|

### 1-3. 通信方式

Model → Presenter の通知には **R3（ReactiveProperty / Observable）** を使用する。 Presenterは ModelのReactivePropertyをSubscribeし、Viewのメソッドを呼び出す。

```
Model: ReactiveProperty<int> CurrentHp
  ↓ Subscribe
Presenter: CurrentHp.Subscribe(hp => view.UpdateHpBar(hp))
  ↓ メソッド呼び出し
View: UpdateHpBar(int hp)
```

---

## 2. クラス設計

### 2-1. クラス一覧

#### Input Layer

|クラス名|責務|区分|
|---|---|---|
|`IHandLandmarkProvider`|ランドマーク座標を提供するインターフェース|Interface|
|`MediaPipeHandLandmarkProvider`|MediaPipeUnityPluginを使用してランドマーク座標を毎フレーム出力する|MonoBehaviour|
|`HandTrackingService`|ランドマーク座標から指の関節角度を算出し、手印（HandSign enum）を識別する|Pure C#|

> **設計意図:** `IHandLandmarkProvider` をインターフェースとして定義することで、 将来的にWebGL対応やモック差し替えが必要になった場合にも実装を切り替えられる。 テスト時にはモック実装を注入して手印入力をシミュレートすることもできる。

#### Model Layer

|クラス名|責務|区分|
|---|---|---|
|`SpellSequenceModel`|プレイヤーの手印入力をQueueで管理し、SpellDataのsequenceと前方一致で照合する。発動印・解除印のトリガー処理を行う|Pure C#|
|`BattleModel`|プレイヤーHP・ボスHP・コンボカウント等のバトルデータを保持し、ダメージ計算Strategyを呼び出す|Pure C#|
|`GameStateManager`|ゲーム全体のフェーズ（Title / InGame / Result）を管理する|Pure C#|
|`PlayerStateManager`|プレイヤーの行動フェーズ（Idle / Chanting / Releasing / Guarding）を管理する|Pure C#|
|`EnemyStateManager`|敵の行動フェーズ（Idle / Charging / Attacking / Stunned）を管理する。全状態が「タイマー経過→次へ」の共通パターンのため、`async UniTaskVoid`ループ + enum/switchで実装する（Stateパターンは不採用）|Pure C#|

#### Strategy

|クラス名|責務|区分|
|---|---|---|
|`IDamageCalculator`|ダメージ計算のインターフェース|Interface|
|`SingleHitCalculator`|単発高火力のダメージ計算|Pure C#|
|`MultiHitCalculator`|多段ヒットのダメージ計算|Pure C#|
|`DamageOverTimeCalculator`|継続ダメージ（DoT）のダメージ計算|Pure C#|

> **拡張方針:** 新しいダメージ計算方式が必要になった場合は、 `IDamageCalculator` を実装した新クラスを追加するだけで対応できる。

#### Presenter Layer

|クラス名|責務|区分|
|---|---|---|
|`BattlePresenter`|BattleModel ↔ BattleView の仲介|MonoBehaviour|
|`EnemyPresenter`|EnemyStateManager ↔ EnemyView の仲介|MonoBehaviour|
|`HandSignPresenter`|SpellSequenceModel ↔ HandSignView の仲介（印の表示・確定演出のトリガー）|MonoBehaviour|
|`BattleInitializer`|全ModelとPresenterの生成・注入を一元管理する。Presenter-in-Presenter依存を避けるため、各Modelの生成責務をここに集約する。Script Execution OrderをPresenter群より先行させる|MonoBehaviour|

#### View Layer

|クラス名|責務|区分|
|---|---|---|
|`BattleView`|HPバー・術名テロップ・発動エフェクト・リザルト画面など、バトル画面のUI描画全般|MonoBehaviour|
|`EnemyView`|ボスキャラの表示・攻撃予告演出・被弾リアクション|MonoBehaviour|
|`HandSignView`|カメラプレビュー表示・ランドマークオーバーレイ・印シーケンスガイド・印確定エフェクト|MonoBehaviour|

#### Data Layer

|クラス名|責務|区分|
|---|---|---|
|`SpellData`|術の定義データ（シーケンス・威力・属性・演出素材）|ScriptableObject|
|`EnemyData`|敵の定義データ（HP・弱点属性・行動パターン）|ScriptableObject|
|`EnemyAttackData`|攻撃1種分のテンプレートデータ（ダメージ・チャージ時間・演出）|ScriptableObject|
|`EnemyAction`|行動パターン1要素分のデータ（`EnemyAttackData`参照・大技フラグ・行動後の待機時間）|Serializable struct|
|`StageData`|ステージ定義データ（登場ボス・BGM）|ScriptableObject|

### 2-2. インターフェース定義

```csharp
// --- Input Layer ---

/// <summary>
/// ランドマーク座標を提供するインターフェース。
/// PC版はMediaPipeUnityPlugin実装、将来的にモック等に差し替え可能。
/// </summary>
public interface IHandLandmarkProvider
{
    /// <summary>
    /// 検出された手のランドマーク座標一覧を返す。
    /// 検出なしの場合は空のリストを返す。
    /// </summary>
    IReadOnlyList<HandLandmark> GetCurrentLandmarks();

    /// <summary>
    /// 現在検出されている手の数を返す。
    /// </summary>
    int DetectedHandCount { get; }
}

// --- Strategy ---

/// <summary>
/// ダメージ計算のStrategy。
/// 術の種類ごとに異なるアルゴリズムを実装する。
/// </summary>
public interface IDamageCalculator
{
    /// <summary>
    /// ダメージを計算して返す。
    /// </summary>
    /// <param name="spellData">発動した術のデータ</param>
    /// <param name="enemyData">対象の敵データ</param>
    /// <param name="speedBonus">速度ボーナス倍率</param>
    /// <param name="comboCount">コンボ数</param>
    /// <returns>計算結果のダメージ情報</returns>
    DamageResult Calculate(SpellData spellData, EnemyData enemyData, float speedBonus, int comboCount);
}
```

---

## 3. データ定義

### 3-1. enum一覧

```csharp
/// <summary>
/// 手印の種類。
/// 詠唱印（Open/Fist/Point/Scissors/Palm）と特殊印（Release, Cancel）に大別される。
/// SpellData.sequence等でシリアライズ済みのため、明示的な整数値で固定する。
/// 値の変更・並び替えは既存アセットのデータ破壊につながるため禁止。
/// 新規の印は必ず末尾に新しい値で追加する（Copilotレビュー対応 / A3）。
/// </summary>
public enum HandSign
{
    Open = 0,       // 壱印「開」パー（全指開き）
    Fist = 1,       // 弐印「握」グー（全指閉じ）
    Point = 2,      // 参印「指」人差し指のみ伸ばす
    Release = 3,    // 発動印「射」親指曲げ+人差し指+小指
    Cancel = 4,     // 解除印「散」小指のみ伸ばす
    Scissors = 5,   // 肆印「刃」チョキ（人差し指+中指）
    Palm = 6,       // 伍印「掌」親指だけ折る
}
```

> **未検出の表現:** enumに`None`は持たず、`HandTrackingService`内部で`HandSign?`（nullable）として扱う。 手が検出されていない・未知のパターンは`null`で表現し、R3ストリームには流さない。
> 
> **Union（陸印「合」両手合わせ）:** 実装保留中。導入する場合は`Union = 7`として末尾に追加する。 両手検出（`DetectedHandCount == 2` かつ両手首座標の距離が閾値以下）が判定条件となる想定。

```csharp
/// <summary>
/// 属性。ボスの弱点属性との相性計算に使用する。
/// </summary>
public enum ElementType
{
    Wind,       // 風（壱印「開」）
    Earth,      // 地（弐印「握」）
    Thunder,    // 雷（参印「指」）
    Water,      // 水（肆印「刃」）
    Fire,       // 火（伍印「掌」）
    Light,      // 光（陸印「合」）
}

/// <summary>
/// 攻撃範囲タイプ。
/// ボス戦特化のため現状は2種。
/// </summary>
public enum AttackRangeType
{
    Single,     // 単体狙い撃ち
    Area,       // 全体範囲
}

/// <summary>
/// 副次効果の種類。
/// </summary>
public enum StatusEffectType
{
    None,
    Slow,               // スロウ（敵の行動間隔を延長）
    Stun,               // スタン（敵の行動を一時停止）
    DamageOverTime,     // DoT（継続ダメージ）
}

/// <summary>
/// ゲーム全体のフェーズ。
/// </summary>
public enum GamePhase
{
    Title,
    Calibration,
    Tutorial,
    StageSelect,
    InGame,
    Result,
}

/// <summary>
/// プレイヤーの行動フェーズ。
/// </summary>
public enum PlayerPhase
{
    Idle,       // 待機（印を組んでいない）
    Chanting,   // 詠唱中（シーケンス入力中）
    Releasing,  // 発動中（発動印確定→術エフェクト再生中）
    Guarding,   // 防御中（防御印を構えている）
}

/// <summary>
/// 敵の行動フェーズ。
/// </summary>
public enum EnemyPhase
{
    Idle,       // 待機
    Charging,   // 攻撃予告中（予告演出再生中）
    Attacking,  // 攻撃中
    Stunned,    // スタン状態
}

/// <summary>
/// ダメージ計算方式。SpellDataからStrategyを選択するために使用する。
/// </summary>
public enum DamageType
{
    SingleHit,          // 単発高火力
    MultiHit,           // 多段ヒット
    DamageOverTime,     // 継続ダメージ
}
```

### 3-2. ScriptableObject定義

```csharp
/// <summary>
/// 術の定義データ。
/// Inspector上で新しい術を追加する際はこのアセットを作成するだけで完結する。
/// コードの変更は不要。
/// </summary>
[CreateAssetMenu(fileName = "NewSpell", menuName = "InJutsushi/SpellData")]
public class SpellData : ScriptableObject
{
    [Header("基本情報")]
    [Tooltip("術の表示名")]
    public string spellName;

    [Tooltip("属性")]
    public ElementType element;

    [Tooltip("UIアイコン")]
    public Sprite icon;

    [Tooltip("術の説明文（図鑑用）")]
    [TextArea(2, 4)]
    public string description;

    [Header("シーケンス定義")]
    [Tooltip("詠唱印の配列。この順番で手印を入力し、最後に発動印で発動する")]
    public HandSign[] sequence;

    [Header("戦闘パラメータ")]
    [Tooltip("基礎威力")]
    public float basePower;

    [Tooltip("ダメージ計算方式（Strategyの選択に使用）")]
    public DamageType damageType;

    [Tooltip("ヒット数（MultiHit時に使用）")]
    public int hitCount = 1;

    [Tooltip("攻撃範囲")]
    public AttackRangeType rangeType;

    [Header("副次効果")]
    [Tooltip("付与する副次効果")]
    public StatusEffectType statusEffect;

    [Tooltip("副次効果の持続時間（秒）")]
    public float statusEffectDuration;

    [Header("演出")]
    [Tooltip("術エフェクトのPrefab")]
    public GameObject effectPrefab;

    [Tooltip("発動時のSE")]
    public AudioClip castSE;

    [Tooltip("カットイン演出のSprite（術名テロップ）")]
    public Sprite cutInSprite;
}

/// <summary>
/// 攻撃1種分のテンプレートデータ。
/// SpellDataと対称的な構造として分離しており、複数ボスでの使い回しを想定する。
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyAttack", menuName = "InJutsushi/EnemyAttackData")]
public class EnemyAttackData : ScriptableObject
{
    [Header("基本情報")]
    public string attackName;

    [Header("戦闘パラメータ")]
    [Tooltip("ダメージ量")]
    public int damage;

    [Tooltip("攻撃予告の演出時間（秒）")]
    public float chargeTime = 1.5f;

    [Header("演出")]
    public GameObject effectPrefab;
    public AudioClip se;
}

/// <summary>
/// 行動パターン1要素分のデータ。
/// EnemyDataのactionPattern配列を構成する。
/// </summary>
[Serializable]
public struct EnemyAction
{
    [Tooltip("使用する攻撃データ")]
    public EnemyAttackData attackData;

    [Tooltip("大技（防御軽減率が異なる）かどうか")]
    public bool isHeavy;

    [Tooltip("この行動が完了してから次の行動に移るまでの待機時間（秒）")]
    public float intervalAfter;
}

/// <summary>
/// 敵（ボス）の定義データ。
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "InJutsushi/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("基本情報")]
    public string enemyName;
    public Sprite sprite;

    [Header("戦闘パラメータ")]
    public int maxHp;
    public ElementType weakElement;
    public float weakMultiplier = 1.5f;

    [Header("行動パラメータ")]
    [Tooltip("行動パターン。配列の先頭から順にインデックスを1つずつ進めながら巡回する")]
    public EnemyAction[] actionPattern;
}

/// <summary>
/// ステージの定義データ。
/// </summary>
[CreateAssetMenu(fileName = "NewStage", menuName = "InJutsushi/StageData")]
public class StageData : ScriptableObject
{
    public string stageName;
    public int stageNumber;
    public EnemyData bossData;
    public AudioClip bgm;
    public Sprite backgroundSprite;
}
```

### 3-3. 補助データ構造

```csharp
/// <summary>
/// ランドマーク座標1点分のデータ。
/// MediaPipeの21点それぞれに対応する。
/// </summary>
public struct HandLandmark
{
    public Vector3 Position;
    public int Index; // 0〜20
}

/// <summary>
/// ダメージ計算結果。
/// Strategyからの戻り値として使用する。
/// </summary>
public struct DamageResult
{
    public int TotalDamage;
    public int PerHitDamage;        // 1ヒットあたりのダメージ（MultiHitのView側時間差表示用 / A3追加）
    public int HitCount;
    public bool IsWeakness;         // 弱点属性かどうか
    public bool HasSpeedBonus;      // 速度ボーナスが適用されたか
    public StatusEffectType AppliedEffect;
    public float EffectDuration;
}
```

> **指の曲げ状態について:** 当初`FingerState`構造体を定義していたが、実装では `HandTrackingService`内のローカルbool（5指分）で完結するため構造体は不採用とした。
> 
> **A5での拡張予定:** DoTのtick駆動基盤の実装時に、`perTickDamage`・`tickCount`相当の フィールドを`DamageResult`へ追加する（dev_log_A3 6章参照）。

---

## 4. 状態管理

### 4-1. 状態遷移：GameStateManager

ゲーム全体のフロー制御を担う。

```
                    ┌─────────────┐
                    │   Title     │
                    └──────┬──────┘
                           │ スタート
                    ┌──────▼──────┐
                    │ Calibration │
                    └──────┬──────┘
                           │ 完了
                    ┌──────▼──────┐
              ┌─────│  Tutorial   │（初回のみ）
              │     └──────┬──────┘
              │ スキップ     │ 完了
              │     ┌──────▼──────┐
              └────→│ StageSelect │◄──────┐
                    └──────┬──────┘       │
                           │ 選択         │
                    ┌──────▼──────┐       │
                    │   InGame    │       │ 続行
                    └──────┬──────┘       │
                           │ 決着         │
                    ┌──────▼──────┐       │
                    │   Result    │───────┘
                    └─────────────┘
                           │ タイトルへ
                           ▼
                        Title
```

### 4-2. 状態遷移：PlayerStateManager

プレイヤーの行動フェーズを管理する。InGameフェーズ中のみ稼働する。

```
            ┌──────────┐
     ┌─────→│   Idle   │◄──────────────────────────┐
     │      └────┬─────┘                            │
     │           │ 詠唱印を検出                       │
     │      ┌────▼──────┐                           │
     │      │ Chanting  │───── 解除印 ─────────────→│
     │      └────┬──────┘                           │
     │           │ 発動印を検出                       │
     │      ┌────▼──────┐                           │
     │      │ Releasing │── 固定硬直(250ms)経過 ─────→│
     │      └───────────┘                           │
     │                                              │
     │      ┌───────────┐                           │
     └──────│ Guarding  │── 防御印を解除 ───────────→│
            └───────────┘
                 ▲
                 │ Idle中に防御印を検出
                 └─────── Idle から遷移
```

**遷移条件の詳細:**

|From|To|条件|
|---|---|---|
|Idle|Chanting|詠唱印（Open, Fist, Point, Scissors, Palm）を検出|
|Idle|Guarding|防御印（Fist）を一定時間保持で防御モード移行|
|Chanting|Releasing|発動印（Release）を確定|
|Chanting|Idle|解除印（Cancel）を確定 / 制限時間切れ|
|Releasing|Idle|発動後の固定硬直時間（250ms、`UniTask.Delay`）が経過。VFXの再生時間とは独立させ、ゲームテンポを固定する|
|Guarding|Idle|防御印を解除（手を開くなど）|

> **注意:** Fist（グー）は詠唱印と防御印を兼ねるため、文脈で判断する。 Idle状態でFistを保持 → Guarding。Chanting中にFistが来る → シーケンスの一部。

### 4-3. 状態遷移：EnemyStateManager

敵の行動フェーズを管理する。InGameフェーズ中のみ稼働する。

```
         ┌──────────┐
    ┌───→│   Idle   │◄──────────────────┐
    │    └────┬─────┘                   │
    │         │ 攻撃間隔の経過            │
    │    ┌────▼──────┐                  │
    │    │ Charging  │                  │
    │    │ (予告演出)  │                  │
    │    └────┬──────┘                  │
    │         │ chargeTime 経過          │
    │    ┌────▼──────┐                  │
    │    │ Attacking │── 攻撃処理完了 ──→│
    │    └───────────┘                  │
    │                                   │
    │    ┌───────────┐                  │
    │    │  Stunned  │── 効果時間経過 ──→│
    │    └───────────┘
    │         ▲
    └─────────┘ スタン付与時
```

---

## 5. 手印判定システム

### 5-1. HandLandmarkProvider の仕様

MediaPipeUnityPlugin を使用して、Webカメラ映像からリアルタイムに手のランドマーク座標（21点 × 最大2手）を取得する。

```
MediaPipe 21点ランドマーク配置:

        8   12  16  20     ← 各指先（TIP）
        |   |   |   |
        7   11  15  19     ← 第一関節（DIP）
    4   |   |   |   |
    |   6   10  14  18     ← 第二関節（PIP）
    3   |   |   |   |
    |   5   9   13  17     ← 付け根（MCP）
    2    \  |   |  /
    |     \ |   | /
    1      \|   |/
    |       ────
    0 ← 手首（WRIST）

インデックス対応:
  0: 手首
  1-4: 親指 (CMC, MCP, IP, TIP)
  5-8: 人差し指 (MCP, PIP, DIP, TIP)
  9-12: 中指 (MCP, PIP, DIP, TIP)
  13-16: 薬指 (MCP, PIP, DIP, TIP)
  17-20: 小指 (MCP, PIP, DIP, TIP)
```

### 5-2. HandTrackingService の判定ロジック

#### Step 1: 各指の曲げ判定

**人差し指〜小指（4指）:** MCP→PIP→DIPの3点で判定する。

```
入力: 指のMCP(5), PIP(6), DIP(7) の座標（人差し指の例）

処理:
  ベクトルA = PIP - MCP
  ベクトルB = DIP - PIP
  角度 = Vector3.Angle(A, B)

  if (角度 > 曲げ閾値45°):
      曲がっている
  else:
      伸びている
```

**親指:** 関節構造が異なるため（CMC/MCP/IP/TIPの4点構成）、CMC(1)→MCP(2)→IP(3)の3点と専用閾値で判定する。

```
入力: 親指のCMC(1), MCP(2), IP(3) の座標

処理:
  ベクトルA = MCP - CMC
  ベクトルB = IP - MCP
  角度 = Vector3.Angle(A, B)

  if (角度 > 親指用曲げ閾値20°):
      曲がっている
  else:
      伸びている
```

> **親指判定の検証記録（A3）:** CMC-MCP-IP角度（Pattern A）とMCP-IP-TIP角度（Pattern B）を 実測比較した結果、Pattern Aの方がバラつきが半分以下で安定。 Open時 約10° / Fist時 約26° / Palm時 約38° の実測値から、閾値20°で14°以上のマージンを確保。 測定データの詳細は dev_log_A3 3-1節を参照。

5本指すべての曲げ判定を実行し、5つのbool値を得る。

#### Step 2: 手印の識別

5本指の曲げ状態パターンで手印を識別する。

```
指の曲げ状態:  [親指, 人差し, 中指, 薬指, 小指]
            伸=O, 曲=X

Open    (パー) : [O, O, O, O, O]  全指伸び
Fist    (グー) : [X, X, X, X, X]  全指曲げ
Point   (指差) : [X, O, X, X, X]  人差し指のみ伸び
Scissors(チョキ): [X, O, O, X, X]  人差し指+中指伸び
Palm    (掌)  : [X, O, O, O, O]  親指のみ曲げ
Release (発動) : [X, O, X, X, O]  親指曲げ+人差し指+小指伸び
Cancel  (解除) : [X, X, X, X, O]  小指のみ伸び
```

全7パターンは互いに衝突せず一意に識別できる（A3統合テストで確認済み）。 どのパターンにも一致しない場合は`null`（未知）として扱い、安定判定の対象外とする。

> **OpenとPalmの区別:** 親指の1ビットだけで決まるため、親指判定の精度がこの2印の 信頼性を直接左右する。Pattern Aの実測マージン（14°以上）で実用上問題ないことを確認済み。
> 
> **Union（両手合わせ）の判定（保留中）:** `IHandLandmarkProvider.DetectedHandCount == 2` かつ、両手の手首座標の距離が閾値以下。

#### Step 3: 手印の確定（安定判定）

```
if (識別結果がnull):     // 手なし・未知パターン
    何もせずreturn       // 状態は保持（カウンタもリセットしない）

if (識別結果 != 前回の識別結果):
    前回の識別結果を更新
    安定カウンタ = 1
    確定フラグ = false
    return

if (確定フラグ == true):  // すでに確定済みの印
    return               // 別の印に変わるまで再発火しない（ラッチ）

安定カウンタ++
if (安定カウンタ >= 確定フレーム数):
    確定フラグ = true
    手印確定イベント（OnHandSignRecognized）を発火
```

**設計ポイント:**

- **ラッチ機構:** 一度確定した印は、別の印に変わるまで再確定しない。同じ印を保持し続けても確定イベントは1回だけ発火する
- **null時の早期return:** 手が一瞬フレームアウトしても安定判定の状態を保持する。手の再出現時に同じ印が二重発火しない
- **確定フレーム数:** 24フレーム（60FPS想定で0.4秒）を採用（プロトタイプ検証で確定）

### 5-3. 発動印・解除印の判定

発動印と解除印は詠唱印と同じパイプラインで識別される。 `HandTrackingService` は識別結果を `HandSign` enum として返すのみで、 「それが発動印か解除印か」の意味付けは `SpellSequenceModel` が行う。

---

## 6. シーケンス管理システム

### 6-1. SpellSequenceModel の仕様

#### 保持するデータ

```csharp
// 登録されている全術データ（コンストラクタで注入）
private readonly IReadOnlyList<SpellData> _allSpells;

// プレイヤーの入力履歴
private readonly List<HandSign> _inputHistory;

// 現在マッチ候補として残っている術のリスト
// AddSignのたびに前方一致で絞り込む（matchCandidates方式）
private List<SpellData> _matchCandidates;
```

> **未実装（導入タイミング要検討）:** 詠唱開始からの経過時間（`totalChantTime`、速度ボーナス算出用）は未実装。 `totalChantTime`は`SpellCastResult`導入（A4予定）と合わせて実装する。

#### 公開メソッド

```csharp
// 詠唱印を1つ積む（Release・Cancelはここに渡さない）
// 入力履歴が空→非空になった時にOnChantStartedを発火
public void AddSign(HandSign sign);

// 発動印を受けたときに呼ぶ
// matchCandidatesから入力数と同じ長さの術を探し、
// 見つかればOnSpellCast、なければOnSequenceReset(Misfire)を発火
public void Release();

// 解除印を受けたときに呼ぶ。入力履歴をクリアしOnSequenceReset(Cancel)を発火
public void Cancel();
```

#### R3 通知用

```csharp
// 術が発動した通知（A4でSpellCastResultへの移行を予定）
public Observable<SpellData> OnSpellCast;

// シーケンスがリセットされた通知（Cancel / Misfire）
public Observable<SequenceResetReason> OnSequenceReset;

// 詠唱が開始された通知（Idle→Chanting遷移のトリガー）
public Observable<Unit> OnChantStarted;
```

> **印確定通知の所在:** 印1つ1つの確定通知（`OnHandSignRecognized: Observable<HandSign>`）は `HandTrackingService`が担う。`SpellSequenceModel`はシーケンス全体の照合結果のみを通知する。

### 6-2. シーケンス照合フロー

```
HandTrackingServiceから HandSign を受信
        │
        ▼
    ┌─────────┐
    │ Cancel? │──Yes──→ inputQueueをクリア → OnSequenceReset発火 → Idle
    └────┬────┘
         │ No
         ▼
    ┌──────────┐
    │ Release? │──Yes──→ マッチ判定へ（6-3）
    └────┬─────┘
         │ No（詠唱印）
         ▼
    入力履歴（_inputHistory）に追加
    ※履歴が空→非空になった時はOnChantStarted発火
         │
         ▼
    matchCandidatesを前方一致で絞り込み
         │
    ┌────┴────┐
    │候補あり？│──No──→ 不正シーケンス（候補なし）として保持
    └────┬────┘         ※ただし暴発はRelease時まで確定しない
         │ Yes
         ▼
    絞り込み後のmatchCandidatesを保持して次の印を待つ
```

> **未実装:** 印間タイムアウト（2.0秒で`OnSequenceReset`発火→Idle）は未実装。 導入タイミングは要検討（sequenceTimerと合わせて実装する）。

### 6-3. 発動判定フロー（Release受信時）

```
Release（発動印）を受信
        │
        ▼
    matchCandidatesから「sequence長 == 入力履歴数」の術を探す
    （AddSign時点で前方一致は担保済みのため、長さの一致確認だけで完全一致となる）
        │
    ┌───┴───┐
    │一致あり│
    └───┬───┘
        │ Yes → OnSpellCast(SpellData)発火
        │        入力履歴・matchCandidatesをリセット → Releasing
        │
        │ No → OnSequenceReset(Misfire)発火
        │       入力履歴・matchCandidatesをリセット → Idle
        ▼
```

> **A4での変更予定:** `SpellCastResult`導入時に、成功/暴発を`OnSpellCast(SpellCastResult)`の 単一ストリームに統合し、速度ボーナス算出（totalChantTime基準）を組み込む。

### 6-4. SpellCastResult（A4導入予定）

A2で導入を延期した（dev_log_A2 4-6節参照）。速度ボーナス計測の実装と合わせてA4で導入する。

```csharp
public struct SpellCastResult
{
    public bool IsSuccess;       // 成功 or 暴発
    public SpellData Spell;      // 成功時: 発動した術 / 暴発時: null
    public float SpeedBonus;     // 速度ボーナス倍率（1.0 or 1.5）
    public int ComboCount;       // 現在のコンボ数
}
```

---

## 7. バトルシステム

### 7-1. BattleModel の仕様

#### 保持するデータ

外部公開は`ReadOnlyReactiveProperty` / `Observable`で行い、書き換えはBattleModel内部に閉じる （発火通知は`Subject<T>`、現在値ステートは`ReactiveProperty<T>`の使い分け原則に従う）。

```csharp
// プレイヤー関連
public ReadOnlyReactiveProperty<int> PlayerHp;
public int PlayerMaxHp;
public ReadOnlyReactiveProperty<int> ComboCount;

// ボス関連
public ReadOnlyReactiveProperty<int> BossHp;
public int BossMaxHp;

// バトル状態
public ReadOnlyReactiveProperty<bool> IsBattleActive;

// 勝敗通知（true = プレイヤー勝利、false = 敗北）
public Observable<bool> OnBattleEnd;
```

```csharp
// 内部定数
private const float NormalGuardRate = 0.5f;    // 通常攻撃の防御軽減率
private const float HeavyGuardRate = 0.3f;     // 大技の防御軽減率
private const float MisfireDamageRate = 0.05f; // 暴発セルフダメージ（MaxHp比）
```

#### 主要メソッド

```csharp
/// <summary>
/// 術の発動成功時にボスへダメージを与える。
/// DamageTypeに応じたStrategyをResolveCalculatorで選択して計算する。
/// speedBonusはA4で詠唱時間計測を実装するまで1.0固定。
/// A4でSpellCastResultを受け取る形への変更を予定。
/// </summary>
void ApplySpellDamage(SpellData spellData, float speedBonus = 1.0f);

/// <summary>
/// 暴発時のセルフダメージ（MaxHp×5%）を適用し、コンボをリセットする。
/// </summary>
void ApplyMisfireDamage();

/// <summary>
/// ボスの攻撃ダメージをプレイヤーに適用する。
/// EnemyActionのisHeavyとattackData.damageを参照する。
/// isGuardingがtrueの場合は軽減率を適用する（A5でGuarding実装までfalse固定）。
/// </summary>
void ApplyEnemyDamage(EnemyAction action, bool isGuarding);

/// <summary>
/// 勝敗判定。HP 0以下で決着し、OnBattleEndを発火する。
/// 各ダメージ適用メソッドの末尾から呼ばれる内部メソッド（private）。
/// </summary>
private void CheckBattleEnd();
```

### 7-2. ダメージ計算 Strategy

`SpellData.damageType` に基づいて `IDamageCalculator` の実装を選択する。

```csharp
// Strategyの選択（BattleModel内部 or Factory）
IDamageCalculator calculator = spellData.damageType switch
{
    DamageType.SingleHit => new SingleHitCalculator(),
    DamageType.MultiHit => new MultiHitCalculator(),
    DamageType.DamageOverTime => new DamageOverTimeCalculator(),
    _ => throw new ArgumentOutOfRangeException()
};

DamageResult result = calculator.Calculate(spellData, enemyData, speedBonus, comboCount);
```

#### 各Strategyの計算方式

**SingleHitCalculator（単発高火力）**

```
最終ダメージ = basePower × 弱点倍率 × 速度ボーナス × コンボ倍率
```

**MultiHitCalculator（多段ヒット）**

```
1ヒットあたりのダメージ = (basePower / hitCount) × 弱点倍率 × 速度ボーナス × コンボ倍率
合計ダメージ = 1ヒットあたり × hitCount
```

> **端数処理:** 1ヒットあたりをintに丸めてからhitCount倍するため、同じbasePowerの SingleHitと比べて合計値が微妙に低くなる場合がある。これは仕様として許容する。
> 
> **View分割方式:** HPの減算はModel側で一括適用し、「ダダダと当たる」演出は `DamageResult.PerHitDamage`と`HitCount`を使ってView側で時間差表示する（A4で実装）。 Model層に演出テンポの関心を持ち込まないための設計判断（dev_log_A3 3-4節参照）。

**DamageOverTimeCalculator（継続ダメージ）**

```
初撃ダメージ = basePower × 0.3 × 弱点倍率 × 速度ボーナス × コンボ倍率
tick1回分ダメージ = basePower × 0.7 / tick数 × 弱点倍率
tick数 = statusEffectDuration ÷ tick間隔
tick間隔 = 1.0秒（固定）
```

> **倍率の適用範囲:** 速度ボーナスとコンボ倍率は初撃のみに適用する。 tickダメージは「一度付与したら固定値で刻む」設計とし、弱点倍率のみを適用する。
> 
> **A3/A5のスコープ分割:** Calculatorは「初撃・tick1回分がいくらか」の計算のみを担う。 「毎秒tickダメージを適用し続ける」スケジュール管理はStatusEffect基盤（A5）の責務。 A3時点では`TotalDamage`に初撃分のみを格納し、tick駆動は未接続（dev_log_A3 3-5節参照）。

#### 共通倍率

|要素|値|
|---|---|
|弱点倍率|`EnemyData.weakMultiplier`（デフォルト 1.5）|
|非弱点倍率|1.0|
|速度ボーナス|制限時間の50%以内で全印完了: 1.5 / それ以外: 1.0|
|コンボ倍率|1.0 + (comboCount × 0.1)。上限は要プロト検証|
|防御時の軽減率|通常攻撃: 0.5倍 / 大技: 0.3倍|
|暴発セルフダメージ|PlayerMaxHp × 0.05（固定割合）|

### 7-3. バトル進行フロー

```
InGame開始
    │
    ├─ PlayerStateManager → Idle
    ├─ EnemyStateManager → Idle
    ├─ BattleModel初期化（HP設定）
    │
    ▼
 ┌──────── バトルループ ────────┐
 │                              │
 │  【ボス側】                   │
 │   Idle → (攻撃間隔経過)       │
 │     → Charging（予告演出）     │
 │     → Attacking（ダメージ処理）│
 │     → Idle                   │
 │                              │
 │  【プレイヤー側】              │
 │   並行して自由に手印入力       │
 │   被弾してもシーケンスは継続   │
 │                              │
 │  【勝敗判定】                  │
 │   BossHp <= 0 → 勝利          │
 │   PlayerHp <= 0 → 敗北        │
 └──────────────────────────────┘
    │
    ▼
 Result画面
```

> **重要:** ボスの行動とプレイヤーの印入力は**並行して進む**。 ターン制のように交互に行動するのではなく、ボスは一定間隔で自動的に攻撃してくる。 プレイヤーはその中で「攻撃を受けながら詠唱を続けるか」「防御印を挟むか」を判断する。

---

## 8. MVP構成

### 8-1. Battle MVP

プレイヤーHP・ボスHP・術名テロップ・コンボ表示など、バトル画面の主要UIを担当。

|役割|クラス|担当|
|---|---|---|
|Model|`BattleModel`|HP管理・ダメージ計算・勝敗判定|
|View|`BattleView`|HPバー・術名テロップ・コンボ数・リザルト画面|
|Presenter|`BattlePresenter`|ModelのReactivePropertyをSubscribeしてViewを更新|

```csharp
// BattlePresenter の購読イメージ（現行）
battleModel.PlayerHp.Subscribe(hp => battleView.UpdatePlayerHpBar(hp));
battleModel.BossHp.Subscribe(hp => battleView.UpdateBossHpBar(hp));
battleModel.ComboCount.Subscribe(count => battleView.UpdateComboDisplay(count));
battleModel.OnBattleEnd.Subscribe(isWin => battleView.ShowResult(isWin));

// 成功と暴発は別ストリーム（A4でSpellCastResultの単一ストリームに統合予定）
spellSequenceModel.OnSpellCast.Subscribe(spell => {
    battleView.PlaySpellEffect(spell);
    battleView.ShowCutIn(spell);
});
spellSequenceModel.OnSequenceReset.Subscribe(reason => {
    if (reason == SequenceResetReason.Misfire)
        battleView.PlayMisfireEffect();
});
```

### 8-2. HandSign MVP

カメラプレビュー・ランドマーク描画・印シーケンスガイド・印確定演出を担当。

|役割|クラス|担当|
|---|---|---|
|Model|`SpellSequenceModel`|入力履歴・照合状態|
|View|`HandSignView`|カメラプレビュー・ランドマーク描画・シーケンスガイドUI・印確定エフェクト|
|Presenter|`HandSignPresenter`|印の確定通知をViewに伝達|

> **現状のクラス名:** Presenterの実体は`SpellSequenceRunner`（A1からの名残）。 `HandTrackingService.Tick()`の毎フレーム駆動も担っている。 `HandSignPresenter`へのリネーム・整理はA4のUI実装時に対応予定。

```csharp
// HandSignPresenter の購読イメージ
// 印1つ1つの確定はHandTrackingServiceから、シーケンス全体の状態はSpellSequenceModelから受ける
handTrackingService.OnHandSignRecognized.Subscribe(sign => handSignView.ShowSignConfirmed(sign));
spellSequenceModel.OnSequenceReset.Subscribe(reason => handSignView.ResetSequenceDisplay(reason));
spellSequenceModel.OnChantStarted.Subscribe(_ => handSignView.StartSequenceDisplay());
```

### 8-3. Enemy MVP

ボスの表示・攻撃予告演出・被弾リアクションを担当。

|役割|クラス|担当|
|---|---|---|
|Model|`EnemyStateManager`|ボスの行動フェーズ管理|
|View|`EnemyView`|ボスアニメーション・攻撃予告演出・被弾リアクション|
|Presenter|`EnemyPresenter`|Stateの変化をViewの演出に変換|

---

## 9. 技術選定

### 9-1. 使用技術一覧

|カテゴリ|技術|バージョン|用途|
|---|---|---|---|
|**エンジン**|Unity|6系最新安定版|ゲームエンジン|
|**言語**|C#|Unity対応版|全実装|
|**手認識**|MediaPipeUnityPlugin|最新版|Webカメラからの手ランドマーク検出|
|**リアクティブ**|R3|最新版|Model→Presenter間のデータバインディング|
|**非同期**|UniTask|最新版|非同期処理・タイマー管理|
|**アニメーション**|LitMotion|最新版|UI演出・トゥイーンアニメーション|
|**IDE**|JetBrains Rider|最新版|コードエディタ|
|**ドット絵制作**|Aseprite|最新版|キャラクター・背景素材制作|
|**バージョン管理**|Git / GitHub|-|ソースコード管理|

### 9-2. プラットフォーム

|項目|内容|
|---|---|
|**ターゲット**|Windows（x86_64）スタンドアロン|
|**WebGL**|**非対応**（MediaPipeUnityPluginがWebGL未対応のため）|
|**その他**|将来的にAndroid対応の可能性は残すが、現スコープ外|

### 9-3. WebGL非対応の経緯と判断記録

MediaPipeUnityPlugin の Supported Platforms 表において、 全Editor環境（Linux, Intel Mac, M1 Mac, Windows）いずれも WebGL 列にチェックがないことを確認。 これにより WebGL ビルドでの手ランドマーク取得が不可能と判断し、スコープから除外した。

将来的に WebGL 対応を目指す場合は、以下のアプローチが検討候補となる。

- MediaPipe.js（JavaScript版）をブラウザ側で動かし、JslibまたはWebSocket経由でUnity WebGLにランドマーク座標を送信する
- `IHandLandmarkProvider` インターフェースを介して実装を差し替える設計は維持しているため、アーキテクチャの変更は最小限で済む

### 9-4. エフェクト方針

|項目|方針|理由|
|---|---|---|
|**術エフェクト**|Particle System（Shuriken）|安定性・ドキュメントの充実|
|**UI演出**|LitMotion|軽量かつUniTask統合あり|
|**VFX Graph**|不使用|将来WebGL対応を再検討する場合に制約となるため|
||||

### 9-5. 手印設計方針と認識技術の将来展望

#### 手印設計の方針

MediaPipeUnityPluginはWebカメラ（単眼カメラ）からの2D画像をもとに 3D座標を推定しているため、手と手が重なる手印では**オクルージョン**が発生する。 物理的に隠れた指の座標はMediaPipeが推測で補完するため、誤認識が避けられない。

この制約を踏まえ、NARUTOや呪術廻戦の手印をそのまま再現することは目指さない。 **オクルージョンが発生しない手印の中からMUDRAオリジナルの手印を設計する**方針とする。 手先の形の繊細な認識を活かしたカッコいい手印は、両手を重ねなくても表現できると判断した。

#### 現フェーズの認識アプローチ

手印の分類（ランドマーク座標 → 手印の種類）は**ルールベース**で実装する。

```
各指の曲げ角度を算出  
↓  
伸びている / 曲がっている（2値）に分類  
↓  
5本指のパターンで手印を識別
```

MLモデルを使わない理由は以下の通り。

- 現在の手印種類数ではルールベースで十分に対応できる
- 学習データの収集・モデル学習のコストがプロトタイプフェーズに見合わない
- `IHandLandmarkProvider` インターフェースを介した設計により、 将来の差し替えはアーキテクチャの変更なしに行える

#### 将来的な拡張パス

手印の種類が増加しルールベースの管理が限界を迎えた場合、 または指の曲げ具合の多段階認識が必要になった場合は、以下の移行を検討する。

```
自前データ収集 → PyTorchで学習 → ONNXエクスポート → Unity Sentisで推論
```

- **ONNX（Open Neural Network Exchange）**: フレームワーク間のモデル交換フォーマット
- **Unity Sentis（現: Inference Engine）**: UnityのネイティブMLモデル推論エンジン（ONNX形式をサポート）

現時点ではONNXは圏外だが、`IHandLandmarkProvider` の差し替え設計を維持することで 移行コストを最小限に抑えられる。

---

## 10. 未決定事項・プロトタイプ検証項目

### 10-1. プロトタイプで検証すべき項目

|項目|検証内容|判断基準|
|---|---|---|
|**手印の識別精度**|3種の手印が安定して区別できるか|同一手印の連続認識成功率 90% 以上|
|**手印の確定フレーム数**|0.4秒 / 0.5秒 / 0.6秒で体感を比較|「反応が遅い」と感じないギリギリの長さ|
|**発動印・解除印の手形**|何の手形が誤判定なく快適に出せるか|詠唱印との混同が発生しないこと|
|**ボスの攻撃間隔**|何秒間隔が「忙しすぎず暇すぎない」か|2印コンボを1回完走できる間隔が最低ライン|
|**防御印（Fist）の文脈判定**|Idle中のFist保持と詠唱中のFist入力を区別できるか|意図しない防御モード移行が起きないこと|
|**暴発セルフダメージの量**|MaxHpの5%で適切か|ミスが怖すぎず、かつ無視できない程度|

### 10-2. 未決定の仕様

| 項目                    | 現状                                          | 決定タイミング            |
| --------------------- | ------------------------------------------- | ------------------ |
| **手印の最終種類数**          | 7種（詠唱印5種 + Release/Cancel）実装済み。Union（両手）は保留 | A4完了時の判断ポイントで増減を判断 |
| **コンボ倍率の上限**          | 未設定                                         | α版のバランス調整で確定       |
| **プレイヤーMaxHp**        | 未設定                                         | α版で確定              |
| **各術の具体的数値**          | A3で仮値設定済み（風刃30 / 雷連撃40 / 火炎弾35）             | β版で調整              |
| **ビジュアル：2D or HD-2D** | HD-2D候補だが工数次第                               | Prototype完了時に判断    |

---

## 11. マイルストーン

### 前提条件

- 1日あたり実働4時間以上
- AIアシストを活用（ただし実装は自分の手で行う）
- 開始日: 2026/06/28

---

### Prototype（技術検証）　〜 2026/07/08（10日間）

目標: **手印を組むと画面上で術エフェクトが出る最小デモ**

|工程|内容|期限|完了条件|
|---|---|---|---|
|**P1**|MediaPipe + Unity 接続|07/01（3日）|Webカメラ映像にランドマークがオーバーレイ描画される|
|**P2**|手印判定デモ|07/04（3日）|3種の手印（Open, Fist, Point）が画面にテキスト表示される|
|**P3**|術発動の最小デモ|07/08（4日）|1印 + 発動印でパーティクルエフェクトが再生される。解除印でキャンセルできる|

**P3完了時の判断ポイント:**

- 手印の認識精度は実用レベルか？
- 確定フレーム数の体感はどうか？
- ビジュアルを2Dで進めるかHD-2Dにするか判断する

---

### α版（遊べる最小構成）　〜 2026/07/25（17日間）

目標: **1体のボスと最後まで戦えるバトルが成立する**

|工程|内容|期限|完了条件|
|---|---|---|---|
|**A1**|アーキテクチャ構築|07/11（3日）|State管理・MVP構成・ScriptableObject基盤が動作する|
|**A2**|バトルループ実装|07/16（5日）|ボスが自動攻撃し、プレイヤーが術で反撃でき、HP増減でバトルが決着する|
|**A3**|全手印・全術実装|07/19（3日）|5種以上の手印が動作し、属性の異なる術が3種以上発動できる|
|**A4**|UI・演出の基礎|07/22（3日）|HPバー・印シーケンスガイド・術名テロップ・印確定エフェクトが表示される|
|**A5**|Guarding・StatusEffect実装|07/25（3日）|Fist文脈判定によるGuarding状態が機能し、Stun付与でボスのStunned状態が実際に駆動し、DoTのtick処理が毎秒ダメージを与え続ける|

> **進捗実績:** A3は07/12に完了（予定07/19から1週間前倒し）。A5はA3のスコープから Guarding / StatusEffect / DoTのtick駆動を切り出して新設した工程（dev_log_A3 5章参照）。

**A5完了時の判断ポイント:**

- バトルのテンポ感は面白いか？
- 手印の種類を増やすか据え置くか？
- ダメージバランスの方向性確認

---

### β版（完成版）　〜 2026/08/05（14日間）

目標: **人に遊んでもらえる完成品**

|工程|内容|期限|完了条件|
|---|---|---|---|
|**B1**|全ステージ実装|07/28（3日）|4ステージ分のボス戦が一通りプレイ可能|
|**B2**|チュートリアル・キャリブレーション|07/31（3日）|初見プレイヤーが操作を理解できるフローが整備されている|
|**B3**|バランス調整|08/03（3日）|全ステージのクリア難易度が適切。弱点属性一辺倒にならない|
|**B4**|バグフィックス・最終調整|08/05（2日）|クリティカルなバグがない。SE・BGMが実装されている|

---

### 全体スケジュール概観

```
06/28                  07/08          07/25              08/05
  │── Prototype(10日) ──│── α版(17日) ───│─── β版(11日) ────│
  P1   P2   P3          A1 A2 A3 A4 A5   B1   B2  B3  B4
  接続 判定 発動         基盤 戦 術 UI 防  全面 導入 調整 仕上
```

**合計: 約38日間（5.5週間）**

---

_本仕様書は開発の進行に伴い随時更新する。実装中に発見した仕様の矛盾や改善点は都度反映し、生きたドキュメントとして維持する。_