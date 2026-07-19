# MUDRA

**「手印を組んで術を放て」** ── Webカメラで読み取った手のポーズが、そのまま魔法の詠唱になるアクションゲーム。

NARUTOや呪術廻戦の「印を結んで術を発動する」あの感覚を、コントローラーではなく自分の手そのもので体験できないか。そんな原体験から生まれたプロジェクトです。

## デモ動画
https://youtu.be/OXeevxjCwSA

## 遊び方

1. Webカメラの前で**詠唱印**を順番に組む（例：パー → グー）
2. 各印を約0.5秒キープすると「印確定」の演出とともにロックされる
3. シーケンスが揃ったら**発動印**を組んで術を放つ
4. 間違えたら**解除印**でキャンセルしてやり直せる
5. ボスの攻撃タイミングに合わせて**防御印**を組めばダメージを軽減できる
6. 道中の雑魚敵を突破し、ボスのHPを削りきればステージクリア

ボスは一定間隔で自動攻撃してくるリアルタイム制。「被弾覚悟で詠唱を続行するか、防御印で大技を凌ぐか」の判断がゲームプレイの核になります。


## 技術スタック

| カテゴリ | 技術 | 用途 |
|----------|------|------|
| エンジン | Unity 6 | ゲーム全体 |
| 手認識 | MediaPipeUnityPlugin | Webカメラからの手ランドマーク検出（21点） |
| リアクティブ | R3 | Model → Presenter 間のデータバインディング |
| 非同期 | UniTask | タイマー駆動・非同期ループ |
| トゥイーン | LitMotion | UI演出 |
| ドット絵 | Aseprite | キャラクター・背景素材制作 |
| IDE | JetBrains Rider | |
| ターゲット | Windows スタンドアロン | MediaPipeUnityPluginがWebGL未対応のため |


## アーキテクチャ

MVPアーキテクチャ（Model-View-Presenter）を採用し、UIとゲームロジックを明確に分離しています。

```
Input Layer      MediaPipeHandLandmarkProvider → HandTrackingService
                              │
Model Layer      SpellSequenceModel / BattleModel / PlayerStateManager / EnemyStateManager
                              │  R3 (ReactiveProperty / Observable)
Presenter Layer  BattlePresenter / HandSignPresenter / EnemyPresenter
                              │
View Layer       HpBarView / SpellTelopView / SequenceGuideView / EnemyView / SpellEffectView
                              │
Data Layer       SpellData(SO) / EnemyData(SO) / EnemyAttackData(SO) / StageData(SO)
```

**Model層はすべてPure C#**（MonoBehaviour禁止）で構成し、Unity APIへの依存を排除しています。


## 工夫した点・見てほしい点

### 1. Prototypeからα版へのアーキテクチャ再構築

Prototypeフェーズ（P1〜P3）では「最短で動くもの」を優先し、static eventやハードコードのシーケンス定義など意図的に技術的負債を積みました。α版A1でPull型への転換（`IHandLandmarkProvider`導入）、R3への移行（`event Action` → `Subject<T>`/`ReactiveProperty<T>`）、ScriptableObjectへのデータ外出しを一括で清算しています。

「負債の存在を認識した上で意図的に積む → マイルストーン区切りで計画的に返済する」プロセスをdev_logに記録しています。

### 2. Stateパターンの適材適所な使い分け

- **PlayerStateManager → Stateパターン（IPlayerState）**: 同じ入力（Fist）をIdle中は防御、Chanting中は詠唱印として処理する文脈判定が必要なため、各状態をクラスに分離
- **EnemyStateManager → enum + switch + UniTask非同期ループ**: 全状態が「タイマー経過 → 次の状態へ」の共通パターン。Stateクラスに分離しても中身がほぼ空になるため、シンプルな構成を選択

### 3. Strategyパターンによるダメージ計算の拡張設計

`IDamageCalculator`インターフェースを定義し、術の`DamageType`（SingleHit / MultiHit / DamageOverTime）に応じて計算アルゴリズムを差し替え。新方式の追加は`IDamageCalculator`実装クラスを1つ足すだけで完結します。

### 4. StatusEffect基盤の共通ライフサイクル管理

`IStatusEffect`インターフェース（OnApply / OnTick / OnExpire）で時限効果のライフサイクルを共通化。StunはEnemyStateManagerに、DoTはBattleModelにそれぞれ作用を委譲する構造です。同種効果の重複は無視する方式を採用し、将来のSlow等の追加時もインターフェース実装を足すだけで対応可能です。

### 5. タイミングガード方式

当初の「Idle中にFist保持で防御状態遷移」を、専用Guard印の確定で0.5秒の受付窓が開くタイミングガード方式に変更しました。`GuardWindowManager`をPlayerPhaseと独立させたことで、詠唱中でもガードが割り込み可能（シーケンスに影響しない）になり、攻撃予告を見てタイミングを合わせる攻略感を実現しています。

### 6. 手印認識のルールベース設計と将来の拡張パス

現フェーズの手印分類は「各指の関節角度 → 曲げ/伸び判定 → 5本指パターンで識別」のルールベース。親指は他4指と関節構造が異なるため専用閾値を分離し、`ThumbAngleDebugger`による実測データを根拠に採用しました。全8種が衝突なく一意に識別可能です。

将来的に種類が増加した場合は、`IHandLandmarkProvider`の差し替え設計により「自前データ収集 → PyTorch → ONNX → Unity Inference Engine」への移行パスを確保しています。

### 7. Model生成の一元管理（BattleInitializer）

`BattleInitializer`が全Modelの生成と各Presenterへの注入を担い、Presenter間の依存を排除しています。StatusEffectFactoryの循環依存はポストコンストラクション注入（`SetStatusEffectDependencies`）で解決し、「生成した者が後始末する」原則に従い全ModelのDisposeもここに集約しています。


## 現在のステータス

α版完了、β版（B1〜B9）を進行中。

| フェーズ | 内容 | 状態 |
|---------|------|------|
| Prototype P1〜P3 | MediaPipe接続、手印判定、術発動デモ | ✅ 完了 |
| α版 A1〜A5 | アーキテクチャ再構築、バトルループ、ダメージ計算、UI、Guard・StatusEffect | ✅ 完了 |
| β版 B1 | 開発基盤整備（デバッグ機能、キーボードシミュレーション） | ✅ 完了 |
| β版 B2 | 両手印対応（Union） | 🔧 進行中 |
| β版 B3〜B9 | 全ステージ実装、バトル演出、ゲームフロー、チュートリアル、サウンド、バランス調整 | 📋 計画済み |


## フォルダ構成

```
Assets/MUDRA/Scripts/
├── Input/          # IHandLandmarkProvider, MediaPipeHandLandmarkProvider, HandTrackingService
├── Model/          # SpellSequenceModel, BattleModel, EnemyStateManager, GuardWindowManager
│   ├── State/      # PlayerStateManager, IPlayerState, IdleState, ChantingState, ReleasingState
│   ├── Strategy/   # IDamageCalculator, SingleHit/MultiHit/DamageOverTimeCalculator
│   └── StatusEffect/ # IStatusEffect, DotEffect, StunEffect, StatusEffectManager, StatusEffectFactory
├── Presenter/      # BattlePresenter, BattleInitializer, HandSignPresenter, EnemyPresenter
├── View/           # HpBarView, SpellTelopView, SequenceGuideView, EnemyView, SpellEffectView
├── Data/           # SpellData(SO), EnemyData(SO), EnemyAttackData(SO), StageData(SO), SpellEnums
└── Debug/          # DebugKeyboardInput, DebugMenuView
```


## 開発ドキュメント

各マイルストーンの設計判断・技術的負債の記録は`docs/`配下のdev_logに残しています。

| ドキュメント | 内容 |
|------------|------|
| `docs/specification_MUDRA.md` | ゲーム仕様書 v1.3（クラス設計・状態遷移・バトルループ等） |
| `docs/proposal_MUDRA.md` | ゲーム企画書 |
| `docs/dev_log_P2.md` | Prototype P2: MediaPipe接続・3種の手印判定 |
| `docs/dev_log_P3.md` | Prototype P3: 術発動の最小デモ |
| `docs/dev_log_A1.md` | α版 A1: アーキテクチャ再構築・技術的負債返済 |
| `docs/dev_log_A2.md` | α版 A2: バトルループ・BattleInitializerパターン導入 |
| `docs/dev_log_A3.md` | α版 A3: 親指判定・手印7種・術3種・ダメージ計算Strategy |
| `docs/dev_log_A4.md` | α版 A4: SpellCastResult・HPバー/シーケンスガイド/テロップUI |
| `docs/dev_log_A5.md` | α版 A5: Guard・StatusEffect基盤（Stun/DoT） |
| `docs/dev_log_B1.md` | β版 B1: 開発基盤整備・β版スケジュール再構成 |


## セットアップ

### 前提条件

- Unity 6.4
- Webカメラ付きWindows PC

### 依存パッケージ

| パッケージ | 導入方法 |
|-----------|---------|
| MediaPipeUnityPlugin | `.unitypackage`を手動インポート |
| R3 | UPM git URL |
| UniTask | UPM git URL |
| LitMotion | UPM git URL |

### 手順

1. このリポジトリをクローン
2. Unity Hubからプロジェクトを開く
3. 上記の依存パッケージを導入
4. バトルシーンを開いてPlay


## ライセンス

<!-- プロジェクトに合わせて記載 -->


## 作者

<!-- GitHub・ポートフォリオサイト等のリンク -->
