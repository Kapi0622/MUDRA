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
5. ボスの攻撃をかいくぐりながら詠唱を続け、HPを削りきれば勝利

ボスは一定間隔で自動攻撃してくるリアルタイム制。「被弾覚悟で詠唱を続行するか、防御印で大技を凌ぐか」の判断がゲームプレイの核になります。


## 技術スタック

| カテゴリ | 技術 | 用途 |
|----------|------|------|
| エンジン | Unity 6 | ゲーム全体 |
| 手認識 | MediaPipeUnityPlugin | Webカメラからの手ランドマーク検出 |
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
Presenter Layer  BattlePresenter / SpellSequenceRunner / EnemyPresenter
                              │
View Layer       HpBarView / SpellTelopView / SequenceGuideView / EnemyView / SpellEffectView
                              │
Data Layer       SpellData(SO) / EnemyData(SO) / EnemyAttackData(SO) / StageData(SO)
```

**Model層はすべてPure C#**（MonoBehaviour禁止）で構成し、Unity APIへの依存を排除しています。


## 工夫した点・見てほしい点

### 1. Prototypeからα版へのアーキテクチャ再構築

Prototypeフェーズ（P1〜P3）では「最短で動くもの」を優先し、static eventによるPush型通知やハードコードのシーケンス定義など、意図的に技術的負債を積みました。α版A1で以下をすべて清算しています。

- **Pull型への転換**: `IHandLandmarkProvider`インターフェースを導入し、Provider内にキャッシュ → ServiceがフレームごとにPullする構造へ。スレッド分離をProvider内部に閉じ込め、呼び出し側は同期的にアクセスできるようにした
- **R3への移行**: `event Action`による通知を`Subject<T>`/`ReactiveProperty<T>`に置き換え。「発生を伝えるだけの通知」と「現在値を保持する状態」で使い分けを徹底
- **ScriptableObjectへのデータ外出し**: ハードコードだったシーケンス定義を`SpellData`（SO）に移行し、Inspectorから術を追加できる構成に

「負債の存在を認識した上で意図的に積む → マイルストーン区切りで計画的に返済する」というプロセスをdev_logに記録しており、各判断の理由を追跡できます。

### 2. Stateパターンの適材適所な使い分け

`PlayerStateManager`と`EnemyStateManager`で異なるアプローチを選択しています。

- **PlayerStateManager → Stateパターン（IPlayerState）**: Idle中のFist保持（防御）とChanting中のFist入力（詠唱印）のように、「同じ入力を状態ごとに異なるロジックで処理する」文脈判定の複雑さがあるため、各状態をクラスに分離
- **EnemyStateManager → enum + switch + UniTask非同期ループ**: 全状態が「タイマー経過 → 次の状態へ」の共通パターン。Stateクラスに分離しても中身がほぼ空になるため、シンプルな構成を選択

「パターンの適用は複雑さへの対応であり、すべてに一律適用するものではない」という判断基準を意識しています。

### 3. Strategyパターンによるダメージ計算の拡張設計

`IDamageCalculator`インターフェースを定義し、術の`DamageType`に応じて計算アルゴリズムを差し替える構造です。

```
SpellData.damageType == SingleHit  → SingleHitCalculator
SpellData.damageType == MultiHit   → MultiHitCalculator
SpellData.damageType == DoT        → DamageOverTimeCalculator（計算ロジックのみ。tick駆動はA5）
```

新しいダメージ計算方式が必要になった場合、`IDamageCalculator`を実装したクラスを1つ追加するだけで対応できます。

### 4. 複数術の前方一致フィルタ（matchCandidates方式）

`SpellSequenceModel`では、登録された全`SpellData`を初期候補リストとして保持し、詠唱印が1つ追加されるたびに「その位置のシーケンス値が一致しない候補」を除外して絞り込みます。`Release`時には「入力履歴と長さが完全一致する候補」を探すことで術を特定します。

毎回全シーケンスをフル走査する方式と比較して、入力が進むほど候補数が減るため効率的に動作します。

### 5. Model生成の一元管理（BattleInitializer）

`BattleInitializer`が全Modelの生成と各Presenterへの注入を担い、「Presenter-in-Presenter」のアンチパターン（PresenterがModel取得のために別Presenterに依存する構図）を回避しています。「生成した者が後始末する」原則に従い、全ModelのDisposeもここに集約しています。

### 6. 手印認識のルールベース設計と将来の拡張パス

現フェーズの手印分類は「各指の関節角度 → 曲げ/伸び判定 → 5本指パターンで識別」のルールベースで実装しています。MLモデルを使わない理由は、現在の手印種類数（7種）ではルールベースで十分に対応でき、学習データ収集のコストに見合わないためです。

親指は他4指と関節構造が異なるため、専用の閾値（`_thumbBentThreshold = 20f`）を分離して導入しています。`ThumbAngleDebugger`（一時検証スクリプト）で実測した結果、CMC-MCP-IP角度のバラつきがMCP-IP-TIP角度の半分以下であることを確認し、パターンAを採用しました。5指の曲げ/伸びの組み合わせで全7種が衝突なく識別可能であることをテーブルで検証済みです。

将来的に手印の種類が増加した場合に備え、`IHandLandmarkProvider`インターフェースによる差し替え設計を維持しています。移行パスとしては「自前データ収集 → PyTorchで学習 → ONNXエクスポート → Unity Inference Engineで推論」を想定しています。

### 7. SpellCastResultによるストリーム統合

A2まで成功と暴発を別ストリーム（`OnSpellCast` / `OnSequenceReset`）で通知していた構造を、A4で`SpellCastResult`（readonly struct: `IsSuccess` / `Spell` / `SpeedBonus`）に統合しました。`ComboCount`は`BattleModel`の管轄であるためこのstructには含めず、責務の境界を明確にしています。SpeedBonus計測は`Func<float> getTime`をコンストラクタDIで受け取る方式とし、テスト時にfake clockを注入できる設計です。

### 8. LitMotionを活用したUI演出

A4で実装した3つのViewコンポーネントはいずれもLitMotionのトゥイーンを活用しています。

- **HpBarView（2層遅延ダメージバー）**: 即時反映バーと遅延追従バーの2層構成。LitMotionの`.Bind()` APIで遅延バーのトゥイーンを制御し、連続ダメージ時は前のトゥイーンをキャンセルして最新値に追従させる
- **SequenceGuideView（印シーケンスガイド）**: Prefabベースの動的インスタンス化で候補術を表示。印確定時にScalePunchアニメーションで視覚フィードバック。`MaxDisplayCount = 4`で表示上限を設定
- **SpellTelopView（術名テロップ）**: `CanvasGroup`のalphaをLitMotionでフェードイン/フェードアウト。将来テロップに背景画像やアイコンを追加しても配下の全要素をまとめてフェードできる拡張性を確保


## 改善したい点・未完成な点・既知のバグ

### 未実装の機能（A5以降のスコープ）

- **Guarding状態**: `PlayerStateManager`にIdle中のFist保持による防御モードが未実装（`isGuarding`がfalse固定）。Fist文脈判定（Idle中は防御、Chanting中は詠唱印として処理する）が必要
- **StatusEffect管理基盤**: Stun・Slow・DoTの共通ライフサイクル管理が未実装。`EnemyStateManager`のStunned状態はenum定義のみで駆動処理が未接続
- **DoTのtick駆動**: `DamageOverTimeCalculator`は計算ロジックのみ実装済み。tick駆動（毎秒ダメージ適用）の「適用スケジュール管理」はStatusEffect基盤と合わせてA5で実装予定。`DamageResult`への`perTickDamage`/`tickCount`フィールド追加も未対応
- **火炎弾のDamageType**: DoTtick駆動基盤が未完成のため暫定的にSingleHitで動作中。A5でDamageOverTimeに切り替え予定
- **コンボ倍率の上限値**: 計算式（`1.0 + comboCount × 0.1`）は組み込み済みだが天井が未設定
- **MultiHitの時間差ダメージ表示**: `DamageResult.PerHitDamage`は追加済みだが、View側の時間差演出は未着手。HPバーのトゥイーン基盤が構築済みのため拡張は容易
- **EnemyViewの攻撃予告演出**: Charging状態の可視化が未実装
- **チュートリアル / カメラキャリブレーション**: β版スコープ
- **SE / BGM**: 未実装
- **SpellDataの演出系フィールド**: effectPrefab・castSE等が全アセットでnull。β版のビジュアル作業フェーズで対応予定

### 既知の技術的負債

- `SpellSequenceRunner`は歴史的経緯でPresenter層に存在するが、命名が実態（HandSignPresenterに近い役割）と乖離している。リネーム・責務整理が必要
- `SingleHitCalculator`等のCalculatorを`ResolveCalculator`内で毎回newしている。ステートレスなので実害はないが、種類が増えたらFactoryへの抽出を検討
- SpeedBonus閾値定数（`SpeedBonusTimePerSign = 1.0f`）がconst。バランス調整が本格化した段階でSO化を検討

### 制約事項

- **WebGL非対応**: MediaPipeUnityPluginがWebGLをサポートしていないため、Windowsスタンドアロン専用。将来的にはMediaPipe.js + Jslib/WebSocket経由でのWebGL対応パスを検討中
- **オクルージョン問題**: 単眼Webカメラでは手と手が重なる手印で誤認識が避けられないため、NARUTOや呪術廻戦の手印をそのまま再現する方針は取らず、オクルージョンが発生しないMUDRAオリジナルの手印を設計


## フォルダ構成

```
Assets/MUDRA/Scripts/
├── Input/          # IHandLandmarkProvider, MediaPipeHandLandmarkProvider, HandTrackingService
├── Model/          # SpellSequenceModel, BattleModel, EnemyStateManager, GameStateManager
│   ├── State/      # PlayerStateManager, IPlayerState, IdleState, ChantingState, ReleasingState
│   └── Strategy/   # IDamageCalculator, SingleHitCalculator, MultiHitCalculator, DamageOverTimeCalculator
├── Presenter/      # BattlePresenter, BattleInitializer, SpellSequenceRunner
├── View/           # HpBarView, SpellTelopView, SequenceGuideView, EnemyView, SpellEffectView
├── Data/           # SpellData(SO), EnemyData(SO), EnemyAttackData(SO), SpellEnums, SpellCastResult
└── Debug/          # TempHandTrackingRunner（Input Layer単体確認用）
```


## 開発ドキュメント

各マイルストーンの設計判断・技術的負債の記録は`docs/`配下のdev_logに残しています。

| ドキュメント | 内容 |
|------------|------|
| `docs/specification_MUDRA.md` | ゲーム仕様書（クラス設計・状態遷移・バトルループ等） |
| `docs/proposal_MUDRA.md` | ゲーム企画書 |
| `docs/dev_log_P2.md` | Prototype P2: MediaPipe接続・3種の手印判定 |
| `docs/dev_log_P3.md` | Prototype P3: 術発動の最小デモ |
| `docs/dev_log_A1.md` | α版 A1: アーキテクチャ再構築・技術的負債8件中7件返済 |
| `docs/dev_log_A2.md` | α版 A2: バトルループ実装・BattleInitializerパターン導入 |
| `docs/dev_log_A3.md` | α版 A3: 親指判定・手印7種・術3種・ダメージ計算Strategy拡充 |
| `docs/dev_log_A4.md` | α版 A4: SpellCastResult導入・HPバー/シーケンスガイド/テロップのUI実装 |


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
