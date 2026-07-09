# 📋 開発ログ：α版 A2（バトルループ実装）

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** α版 A2
> **期間:** 2026/07/10
> **ステータス:** ✅ 完了

---

## 1. マイルストーン概要

| 項目 | 内容 |
|------|------|
| **完了条件** | ボスが自動攻撃し、プレイヤーが術で反撃でき、HP増減でバトルが決着する |
| **開発方針** | A1で確立したアーキテクチャの上にバトルループのModel層＋配線を構築 |
| **作業シーン** | A1から引き続き、MediaPipeサンプルシーンの複製を使用 |

---

## 2. 実装ファイル一覧

### Data Layer

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `EnemyAttackData.cs` | 新規作成 | 攻撃1種分のテンプレートデータ（ScriptableObject） |
| `EnemyAction.cs` | 新規作成 | 行動パターン1要素分のシリアライズ可能struct |
| `EnemyData.cs` | 新規作成 | ボス1体分の定義データ（ScriptableObject） |

### Model Layer

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `EnemyPhase.cs` | 新規作成 | 敵の行動フェーズenum（Idle/Charging/Attacking/Stunned） |
| `EnemyStateManager.cs` | 新規作成 | 敵の行動ループ管理（UniTask非同期ループ） |
| `BattleModel.cs` | 新規作成 | HP管理・ダメージ適用・勝敗判定 |

### Strategy

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `IDamageCalculator.cs` | 新規作成 | ダメージ計算Strategyのインターフェース |
| `DamageResult.cs` | 新規作成 | ダメージ計算結果struct |
| `SingleHitCalculator.cs` | 新規作成 | 単発高火力のダメージ計算実装 |

### Presenter Layer

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `BattlePresenter.cs` | 新規作成 | バトル状態の購読・配線、デバッグ表示 |
| `BattleInitializer.cs` | 新規作成 | 全Model生成・各Presenterへの注入 |
| `SpellSequenceRunner.cs` | 改修 | Initialize化、SerializeField移動（自前のnewを廃止） |

### ScriptableObjectアセット（Inspector設定）

| アセット | 設定値 |
|----------|--------|
| 泥塊投げ（EnemyAttackData） | damage: 10, chargeTime: 1.5 |
| 地鳴り（EnemyAttackData） | damage: 25, chargeTime: 2.0 |
| 泥人形（EnemyData） | maxHp: 200, weakElement: Wind, weakMultiplier: 1.5, actionPattern: [泥塊投げ×3(各3s後) → 地鳴り(5s後)] |
| 既存SpellData | basePower: 30, element: Wind, damageType: SingleHit（仮値） |

---

## 3. アーキテクチャ変遷

### Model生成の責務移動

**A1版:**
```
SpellSequenceRunner（Awakeで全ModelをnewしてPresenter兼務）
    ├→ HandTrackingService（new）
    ├→ SpellSequenceModel（new）
    └→ PlayerStateManager（new）
```

**A2版:**
```
BattleInitializer（全Model生成・注入の一元管理）
    │
    ├→ SpellSequenceRunner.Initialize()
    │       受け取り: HandTrackingService, SpellSequenceModel, PlayerStateManager
    │
    └→ BattlePresenter.Initialize()
            受け取り: BattleModel, EnemyStateManager, SpellSequenceModel
```

Presenter-in-Presenterのアンチパターン（PresenterがModel取得のために別のPresenterに依存する構図）を回避するため、Model生成を`BattleInitializer`に集約した。「生成した者が後始末する」原則により、全ModelのDisposeも`BattleInitializer.OnDestroy`に集約。

---

## 4. 設計判断の記録

### 4-1. 攻撃データのScriptableObject分離

`SpellData`と対称的な構造として`EnemyAttackData`（SO）を導入し、`EnemyData`がその参照を保持する2層構成とした。攻撃の使い回し（例：複数ボスが同じ通常攻撃を持つ）と、将来の攻撃種類追加への拡張性を確保する狙い。SO-in-SOの2層は許容範囲と判断。

### 4-2. 行動パターン方式の採用

「通常攻撃タイマーと大技タイマーの2本を並走させる」方式も検討したが、以下の理由でパターン方式を採用した。

- 2タイマー方式は同時発火時の優先度処理が必要になる
- パターン方式は「配列のインデックスを1つ進めるだけ」でループ制御がシンプル
- Inspector上で「通常3回→大技1回」のような行動サイクルを直感的に定義できる
- 各行動の後に個別の待機時間（`intervalAfter`）を持たせることで攻撃の緩急も表現可能

### 4-3. EnemyStateManagerはenum + switch

`PlayerStateManager`でStateパターンを採用した判断基準は「Fist文脈判定のように状態ごとに同じ入力を異なるロジックで処理する複雑さ」だった。`EnemyStateManager`は全状態が「タイマー経過→次へ」の共通パターンであり、各Stateクラスに分離しても中身がほぼ空になるため、enum + switchを選択。将来ボスの行動が複雑化した時点でStateパターンに昇格させる前提。

### 4-4. UniTask非同期ループで行動駆動

`PlayerStateManager`がPresenter駆動（`Tick()`毎フレーム呼び出し）なのに対し、`EnemyStateManager`は「時間経過で自動的にパターンを巡回する」性質のため、`async UniTaskVoid`によるループを採用。`CancellationTokenSource`でバトル終了時にループを即座に停止できる。

### 4-5. 防御軽減率はBattleModelの定数

仕様書の共通倍率表に「通常攻撃: 0.5倍 / 大技: 0.3倍」と定義されており、現状はゲーム全体で固定値。ボスごとの差別化が必要になった時点でデータ側（`EnemyData`または`EnemyAttackData`）に移行する。

### 4-6. SpellCastResult導入をA4に延期

仕様書6-4に定義があるが、A2時点では既存の2ストリーム（`OnSpellCast: Observable<SpellData>`と`OnSequenceReset: Observable<SequenceResetReason>`）で`BattleModel`への接続が成立する。`SpellCastResult`のフィールドのうち`SpeedBonus`は詠唱時間計測、`ComboCount`は`BattleModel`側の管轄であり、いずれもA2スコープ外。SpeedBonus計測の実装と合わせてA4で導入する方が中身の伴った形になる。

### 4-7. BattleInitializerによるModel生成の集約

当初は`SpellSequenceRunner`のModelをpublicプロパティで公開し`BattlePresenter`から参照する案もあったが、Presenter-in-Presenterの依存が発生するため不採用。`BattleInitializer`が全Modelを生成し、各Presenterの`Initialize()`メソッドに注入する構成とした。Script Execution Orderで`BattleInitializer`を-100に設定し、Presenterより先にAwakeを実行させている。

---

## 5. 統合テスト結果

全6項目パス。

| # | テスト項目 | 結果 |
|---|-----------|------|
| ① | ボス自動攻撃ループ（Charging→Attacking→Idle→繰り返し） | ✅ |
| ② | 術発動 → ボスHP減少（弱点倍率の適用を含む） | ✅ |
| ③ | 暴発 → セルフダメージ（MaxHp×5%）+ コンボリセット | ✅ |
| ④ | コンボ蓄積（連続成功でコンボ倍率が増加） | ✅ |
| ⑤ | 勝利判定（BossHp≤0で攻撃ループ停止） | ✅ |
| ⑥ | 敗北判定（PlayerHp≤0で攻撃ループ停止） | ✅ |

---

## 6. 技術的負債・保留事項

| 負債 | 対処方針 |
|------|----------|
| `BattlePresenter`のデバッグ表示がDebug.Logのみ | A4でUI実装時に正式なViewクラスに差し替え |
| `isGuarding`がfalse固定 | A3でGuarding状態実装時に`PlayerStateManager.CurrentPhase`を参照する形に差し替え |
| `ResolveCalculator`が毎回newしている | ステートレスなので問題ないが、Calculator種類が増えたらFactory抽出を検討 |
| `SpellSequenceRunner`の将来的なリネーム | `HandSignPresenter`への整理が必要（A1から継続） |
| MediaPipeサンプルシーンの既存UI（上下バー・全画面カメラプレビュー） | A4冒頭で整理予定。A2〜A3中に実害が出た場合はInteractable無効化で暫定対処 |

---

## 7. A3への引継ぎ

**A3完了条件:** 5種以上の手印が動作し、属性の異なる術が3種以上発動できる

### 前提として把握しておくべきこと

- バトルループの全経路（手印→術発動→ボスHP減少 / ボス攻撃→プレイヤーHP減少 / 勝敗判定）はA2で疎通確認済み
- `BattleModel`は`ApplySpellDamage(SpellData)`で術ダメージを受け付ける。`DamageType`に応じたCalculator分岐は`ResolveCalculator`で対応
- `EnemyStateManager`のStunned状態はenumの値のみ定義済み。ループ内の処理は未実装
- コンボ倍率（`1.0 + comboCount × 0.1`）は計算に組み込み済みだが、上限値は未設定

### A3で新たに必要になるもの

- `MultiHitCalculator` / `DamageOverTimeCalculator` の実装
- `PlayerStateManager`へのGuarding状態追加（Fist文脈判定を含む）
- StatusEffect付与ロジック（Stun → `EnemyStateManager`のStunned状態を駆動）
- 親指判定の実装（A1から延期されている技術的負債）
- 新しい手印種類（Scissors、Palm）の判定ロジック追加
- 属性の異なる術のSpellDataアセット作成（最低3種）

---

## 8. 仕様書との齟齬（要更新）

| 箇所 | 現状の仕様書 | 実装の実態 | 更新内容 |
|------|------------|-----------|---------|
| クラス一覧（Data Layer） | `EnemyData`のみ記載 | `EnemyAttackData`、`EnemyAction`を新設 | 2クラスを追加 |
| `EnemyData`のフィールド定義 | `normalAttackInterval`等のフラットなフィールド構成 | `actionPattern`（`EnemyAction`配列）によるパターン方式 | フィールド定義をパターン方式に更新 |
| `EnemyStateManager`の状態 | Idle / Attacking / Stunned の3状態 | Idle / Charging / Attacking / Stunned の4状態 | Charging状態を追加 |
| クラス一覧（Presenter Layer） | `BattlePresenter`、`EnemyPresenter`、`HandSignPresenter` | `BattleInitializer`を新設 | Presenter Layerに追加 |
