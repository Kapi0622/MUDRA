# 📋 開発ログ：β版 B3（全ステージ実装）

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** β版 B3
> **期間:** 2026/09/20〜10/02
> **ステータス:** ✅ 完了

---

## 1. マイルストーン概要

| 項目 | 内容 |
|------|------|
| **完了条件** | 4ステージ全て（道中セクション＋ボス戦）でバトルが開始〜決着まで動作する |
| **開発方針** | 進行基盤（コード）→ ステージジャンプ（デバッグ）→ 敵・ステージデータ（SO一括生成）の順 |
| **位置づけ** | ボス1体との単発バトルを、HP・コンボを引き継ぐ複数セクションのステージ進行に拡張する |

---

## 2. 実装ファイル一覧

### Step 1: セクション進行基盤 + 回復（`ee2751c`）

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `Model/SectionProgressManager.cs` | 新規 | セクション進行の本体。`OnBattleEnd` を購読し、次セクション / ステージクリア / 敗北を判断 |
| `Model/SectionPhase.cs` | 新規 | InBattle / Transitioning / StageCleared / GameOver |
| `Data/StageData.cs` | 新規 | `StageData`（SO）+ `StageSection`（struct） |
| `Model/StatusEffect/HotEffect.cs` | 新規 | 継続回復。`DotEffect` と同形 |
| `Model/BattleModel.cs` | 改修 | `SetEnemy` / `ApplyHeal` / `OnEnemyChanged`、`BossMaxHp` 可変化 |
| `Model/EnemyStateManager.cs` | 改修 | `SetEnemy` |
| `Model/StatusEffect/StatusEffectManager.cs` | 改修 | `ClearEnemyEffects`（HoT以外を除去） |
| `Model/StatusEffect/StatusEffectFactory.cs` | 改修 | `HealOverTime` の生成 |
| `Data/SpellEnums.cs` / `Data/SpellData.cs` / `Model/Strategy/DamageResult.cs` | 改修 | `HealOverTime`（末尾追加）/ `healPower` / `PerTickHeal` |
| `Presenter/BattleInitializer.cs` | 改修 | `Awake` を検証・生成・注入・購読・開始に分割。`_enemyData` → `_allStages` |
| `Presenter/BattlePresenter.cs` | 改修 | `OnEnemyChanged` でボスHPバー再初期化。引数名がフィールドを隠していた不具合も修正 |

### Step 2: ステージジャンプ（`ee2751c`）

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `Model/SectionProgressManager.cs` | 改修 | `StartStage` を再入可能化、`DebugJumpToSection` |
| `Model/BattleModel.cs` | 改修 | `DebugResetPlayerState` |
| `Debug/DebugMenuView.cs` | 改修 | ステージ切替・セクション指定・次へ・ボスへ飛ぶ |

### Step 3: 敵・ステージデータ（`bf45ea2`）

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `Editor/B3ContentGenerator.cs` | 新規 | 攻撃23・敵15・ステージ4 のSOを一括生成。メニュー `MUDRA/Generate B3 Content` |

定義値は `docs/B3_enemy_stage_content.md`。

---

## 3. 設計判断の記録

### 3-1. セクション進行は `SectionProgressManager` に集約

`BattleModel` は「現在の敵1体との決着」までを担い、`OnBattleEnd` で通知するだけ。次へ進むかクリアかは SPM が判断する。肥大化気味の `BattleModel` にセクションの概念を持ち込まないため。SPM は Model 参照を直接持ち、差し替えまで自分で行う（Presenter に手続きを載せない）。

### 3-2. Model を作り直さず、敵データだけ差し替える

キックオフ時点では「EnemyStateManager をセクションごとに再生成」案だったが、`SetEnemy` による差し替えに変更した。`EnemyStateManager` の状態は `_enemyData` / `_patternIndex` / `_loopCts` の3つだけで、リセットすれば新規生成と等価になる。再生成すると `StatusEffectFactory` のデリゲート、`BattlePresenter` の購読、`DebugMenuView` の参照をすべて張り直す必要があった。

### 3-3. 遷移を非同期にする

`OnBattleEnd` は `CheckBattleEnd` から同期発火し、`ApplyDotDamage` 経由では `StatusEffectManager.Tick` の走査中に呼ばれる。その場で効果のクリアや敵の差し替えを行うとリストを壊すため、`await` を1回挟んでから状態を変える。仕様書 §7-3 の遷移演出の尺（現状 1.5秒の仮値）とも兼ねる。

### 3-4. 引き継ぎルール

| 対象 | 扱い |
|------|------|
| プレイヤーHP | 引き継ぐ（セクション間の自動回復なし） |
| コンボ | 引き継ぐ |
| 敵に付いた StatusEffect | 遷移時に除去 |
| HoT | **例外的に持ち越す**。回復術の直後に敵を倒すと回復が消える理不尽を避けるため |
| 遷移中の入力 | 受け付けるがダメージは入らない（`_isBattleActive` のガードがそのまま効く） |

### 3-5. 回復は通常の術として扱う

専用の印は作らない。`statusEffect = HealOverTime` と `healPower` で指定し、`basePower = 0` なら純粋回復、両方 > 0 ならドレインになる。回復量は弱点・速度・コンボの倍率を受けない（それらは「敵に与えるダメージ」の倍率のため）ので、Strategy ではなく `BattleModel` が `SpellData` から直接算出する。

### 3-6. `StartStage` の再入対応

ステージジャンプで `StartStage` を複数回呼ぶと `OnBattleEnd` が二重購読になり、決着1回でセクションが飛ぶ。購読をコンストラクタへ移した。あわせて、ステージ切替直後に敵データ未設定で `EnterSection` が中断すると index が古いまま残る不具合も修正した。

### 3-7. SOは Editor 拡張で一括生成

YAML 直書きは GUID 参照が壊れやすいので、定義表（データ）と処理を分けた生成スクリプトにした。

- **`isHeavy` は攻撃テーブルから自動設定。** 行動ごとに書かせないことで「同じ攻撃が大技になったりならなかったりする」事故を構造的に防ぐ
- **行動パターンは依頼書の表記に近い1行文字列**（`"MudThrow(3.0)x3 > Tremor(5.0)"`）。目視で依頼書と突き合わせやすく、CSV の1セルにも収まる
- 定義表に誤記があれば何も書き込まずに中断。生成後はディスクから読み直して完了条件④⑤⑥を検証する
- **演出系フィールド（sprite / effectPrefab / SE / BGM / 背景）には書き込まない。** 既存の泥人形スプライトと Stage1 のボス背景を消さないため

---

## 4. 動作確認結果

| 確認項目 | 結果 |
|----------|------|
| セクション遷移・HP/コンボ引き継ぎ・ステージクリア・敗北 | ✅ |
| DoT の tick で敵撃破（リスト走査中の遷移） | ✅ |
| HoT（回復術） | ✅ |
| ステージジャンプ（二重購読なし・決着後の復帰・遷移中のジャンプ） | ✅ |
| 生成メニュー（再実行で重複なし、検証④⑤⑥OK） | ✅ |
| 4ステージ通し（無敵モード使用） | ✅ |

---

## 5. アセット変更一覧

| 旧名 | 新名 | 主な変更 |
|------|------|----------|
| `MudballToss` | `EA_MudThrow` | damage 10 → 4 |
| `Earthrumblings` | `EA_Tremor` | damage 25 → 10。大技扱い（`isHeavy = true`）に変更 |
| `ClayDoll` | `ED_MudGolem` | maxHp 200 → 800。スプライトは維持 |
| `Stage1` | `SD_Stage01_SealedVillage` | セクション 泥人形×2 → 泥ころ / 枯れ木霊 / 泥人形。ボス背景は維持 |

いずれも `AssetDatabase.RenameAsset` で GUID を維持。新規は攻撃21・敵14・ステージ3。表示名の【ボス】（派生）は表の注記と判断し `enemyName` から外した。

---

## 6. 技術的負債・保留事項

| 負債 | 対処方針 |
|------|----------|
| `isHeavy` が `EnemyAction` 側にある | 一貫性は生成スクリプトで担保している。`EnemyAttackData` 側へ移すかを検討 |
| ボスへの Stun 連続付与でパターンを止め続けられる | B8でスタン耐性の要否を判断（B2から継続） |
| 敵HPはプレイヤーDPS ≒ 20 の仮定で逆算 | 実測DPSを取ってB8で掛け直す |
| 被ダメは `PlayerMaxHp = 100` 前提（ノーガードで1ステージあたりHPの7割〜1.2倍） | B8で `PlayerMaxHp` と合わせて調整 |
| 数値が生成スクリプト内の定義表にある | B8で外部スプレッドシート管理 + CSV 取り込みを検討。表と処理は分離済み |
| `ClearEnemyEffects` が `Type == HealOverTime` で分岐している | プレイヤー側の効果が増えたら `IStatusEffect` に適用先を持たせる |
| `HotEffect.TickInterval` と `BattleModel.HealTickInterval` の二重定義 | DoT と同じ状況。実害なし |
| 遷移待機 `TransitionDuration = 1.5f` は仮値 | B4の遷移演出で決める |
| 演出系フィールド・`isBoss`・`OnSectionStarted` が未使用 | B4で `EnemyView` / 背景切替に接続 |

---

## 7. 仕様書更新（v1.4 → v1.5、反映済み）

| セクション | 更新内容 |
|------------|----------|
| 2-1 クラス一覧 | `SectionProgressManager` / `HotEffect` を追加。`BattleModel` / `EnemyStateManager` / `StatusEffectManager` の責務を更新 |
| 3-1 / 3-2 | `HealOverTime`、`SpellData.healPower`、`StageData` の menuName 修正、**アセット命名規則**を追記 |
| 10-2 未決定の仕様 | 雑魚の種類数・コンボ引き継ぎ・StatusEffect 引き継ぎ・回復手段を確定に更新 |
| 11 B3内訳 | 「雑魚11種（ユニーク8 + 派生3）・攻撃23種」を反映。ドット絵はB4以降へ |

あわせて `docs/architecture/` の 00・02 を B3 時点に更新した（02 に §11 SectionProgressManager を新設）。

---

## 8. B4への引継ぎ

- 敵の見た目・背景・攻撃演出の受け口は `SectionProgressManager.OnSectionStarted`（`StageSection` に `enemyData` と `isBoss`）。Presenter で購読して View に渡す部分から作る
- 敵の数値を変えるときは `Editor/B3ContentGenerator.cs` の定義表を直して再実行する。アセットを直接変えると次の再実行で上書きされる
