# B3 敵・ステージデータ作成依頼（4ステージ構成）

## 1. 依頼概要

MUDRAのB3「全ステージ実装」のうち、敵・攻撃・ステージのScriptableObjectアセットを作成する。
コードの新規ロジック追加は不要で、既存の `EnemyAttackData` / `EnemyData` / `StageData` のアセット作成が主作業となる。

- 対象: 攻撃データ23種（既存2種の値変更を含む）、敵データ15種（雑魚ユニーク8種 + 派生3種 + ボス4種）、ステージデータ4種
- 数値はすべて初期値。B8（バランス調整）で見直す前提
- スプライトは未用意のため、`sprite` / `effectPrefab` / `se` / `bgm` / 背景系フィールドは空のままでよい

## 2. 作業前に確認してほしいこと

1. `docs/specification_MUDRA.md` の「3-2. ScriptableObject定義」と、実際の `EnemyAttackData` / `EnemyData` / `EnemyAction` / `StageSection` / `StageData` のコードを読み、フィールド名・型が本書の想定と一致しているか確認すること。差異があれば作業前に報告すること
2. 既存アセット（泥人形・泥塊投げ・地鳴り、および既存のStageData）の保存場所と現在のファイル名を確認すること。保存場所は既存に合わせ、ファイル名は本書の命名規則（3節）に揃える
3. 既存の術SO（風刃・雷連撃・火炎弾・地属性術・水属性術・光属性術）は変更しない。ファイル名の変更も今回は対象外

## 3. 命名規則

### 3-1. 方針

- **ファイル名は英語（ASCII）に統一する。** 日本語はファイル名に使わない
- 日本語名は `attackName` / `enemyName` / `stageName` の表示用フィールドに設定する
- ファイル名は `種別プレフィックス_英語名`（PascalCase）とする

| 種別 | プレフィックス | 例 |
|---|---|---|
| EnemyAttackData | `EA_` | `EA_MudSplash` |
| EnemyData | `ED_` | `ED_MudBlob` |
| StageData | `SD_` | `SD_Stage01_SealedVillage` |

- 河童・鵺・火車のように英語圏でも固有名詞として通じる妖怪名はローマ字表記とする
- 既存アセットに別のプレフィックス規則がすでにある場合は、作業前に報告し、どちらに揃えるか確認を取ること

### 3-2. 既存アセットのリネーム

既存の泥人形・泥塊投げ・地鳴り（および既存StageDataを流用する場合はそれも）を本書のファイル名へリネームする。

- **必ず `.meta` のGUIDを維持する方法で行うこと。** Unityエディタ上でのリネーム、または `AssetDatabase.RenameAsset` を使う。`.meta` を伴わないファイル単体のリネームや、削除して作り直す方法は禁止（既存の参照が切れるため）
- リネーム後、既存の参照（BattleInitializerやテスト用シーン等）が切れていないことを確認する

## 4. 作成方法の方針

アセットは手作業のYAML直書きではなく、**Editor拡張スクリプトによる一括生成**を推奨する。
理由は、.assetファイルはスクリプトのGUID参照を含むため直書きは壊れやすく、またSO同士の参照（敵→攻撃、ステージ→敵）を安全に張れるため。

- 生成スクリプトは `Assets/Scripts/Editor/` 配下（既存のEditorフォルダがあればそちら）に置く
- メニュー例: `MUDRA/Generate B3 Content`
- 同名アセットが既に存在する場合は新規作成せず値を上書きする（再実行しても重複しないこと）。既存アセットの判定はファイル名で行う
- 生成後は `EditorUtility.SetDirty` と `AssetDatabase.SaveAssets` で確実に保存する
- 生成スクリプトはコンテンツ作成用の一時ツールとして扱い、ランタイムコードからは参照しない
- 定義値はスクリプト内で表形式のデータとしてまとめて持ち、処理部分と分離すること（将来、バランス調整用スプレッドシートからCSVで取り込む形へ拡張しやすくするため）

> 生成方法についてより良い案があれば、実装前に提案してほしい。

## 5. 共通ルール

- **isHeavyの一貫性:** 下表で「大技」とした攻撃は、どの敵の行動パターンで使う場合も必ず `EnemyAction.isHeavy = true` とする。それ以外は `false`。同じ攻撃が行動によって大技になったりならなかったりすると、プレイヤーが予告演出から区別できなくなるため
- **weakMultiplier:** 影龍のみ 2.0、それ以外はデフォルトの 1.5
- **行動パターン表記:** `攻撃名(n)` は「その攻撃を使い、`intervalAfter = n` 秒待つ」1要素を表す。`×3` は同じ要素を3回並べることを意味する
- **派生種:** 元の敵と同じ攻撃アセットを使い、HPと `intervalAfter` だけを変えている。攻撃アセットは新規作成しない

## 6. 攻撃データ（EnemyAttackData）

| ファイル名 | attackName | damage | chargeTime | 大技 | 備考 |
|---|---|---|---|---|---|
| `EA_MudSplash` | 泥はね | 4 | 1.5 | | |
| `EA_MudThrow` | 泥塊投げ | 4 | 1.5 | | 既存アセット。リネーム、damageを10→4に変更 |
| `EA_Tremor` | 地鳴り | 10 | 2.0 | ✓ | 既存アセット。リネーム、damageを25→10に変更 |
| `EA_BranchWhip` | 枝打ち | 4 | 1.5 | | |
| `EA_RootBind` | 根縛り | 10 | 2.5 | ✓ | |
| `EA_WaterGun` | 水鉄砲 | 3 | 1.0 | | |
| `EA_ClawPinch` | 鋏挟み | 6 | 2.0 | | |
| `EA_ShellCrush` | 甲羅潰し | 14 | 2.5 | ✓ | |
| `EA_WaterBullet` | 水弾 | 3 | 1.2 | | |
| `EA_Whirlpool` | 渦潮 | 10 | 2.0 | ✓ | |
| `EA_Tsunami` | 大津波 | 16 | 2.5 | ✓ | |
| `EA_Ember` | 火の粉 | 3 | 0.8 | | |
| `EA_RunOver` | 轢き | 6 | 1.0 | | |
| `EA_BlazeWheelRush` | 炎輪突進 | 14 | 1.2 | ✓ | |
| `EA_InfernoFist` | 業火拳 | 5 | 1.0 | | |
| `EA_ScorchWave` | 焦熱波 | 14 | 1.2 | ✓ | |
| `EA_FlameBurst` | 爆炎 | 22 | 1.8 | ✓ | |
| `EA_ShadowStab` | 影刺し | 5 | 1.5 | | |
| `EA_ShadowStitch` | 影縫い | 12 | 0.8 | ✓ | |
| `EA_ThunderRoar` | 雷鳴 | 14 | 1.5 | ✓ | |
| `EA_ShadowClaw` | 影爪 | 6 | 1.5 | | |
| `EA_BlackLightning` | 黒雷 | 14 | 1.0 | ✓ | |
| `EA_NetherPrison` | 冥獄 | 28 | 3.0 | ✓ | |

※「大技」列はEnemyAttackDataのフィールドではない。行動パターン設定時の `isHeavy` の値を示す。

## 7. 敵データ（EnemyData）

### ステージ1「封土の里」

| ファイル名 | enemyName | maxHp | weakElement | actionPattern | 設計意図 |
|---|---|---|---|---|---|
| `ED_MudBlob` | 泥ころ | 200 | Wind | 泥はね(3.0) | 単調なリズムで「受けながら詠唱」を覚える |
| `ED_WitheredKodama` | 枯れ木霊 | 250 | Fire | 枝打ち(2.5) → 枝打ち(2.5) → 根縛り(4.0) | 初めての大技。予告が長くガードを試しやすい |
| `ED_MudGolem` | 泥人形【ボス】 | 800 | Wind | 泥塊投げ(3.0)×3 → 地鳴り(5.0) | 既存アセット。リネームと値の更新。大技後の5秒が攻め時 |

### ステージ2「水底の社」

| ファイル名 | enemyName | maxHp | weakElement | actionPattern | 設計意図 |
|---|---|---|---|---|---|
| `ED_Kappa` | 河童 | 300 | Thunder | 水鉄砲(0.8) → 水鉄砲(0.8) → 水鉄砲(4.0) | 3連射と長い休止のリズム |
| `ED_StoneMudBlob` | 岩泥ころ（泥ころ派生） | 300 | Wind | 泥はね(2.0) | 泥ころの間隔短縮版 |
| `ED_CrabMonk` | 蟹坊主 | 380 | Earth | 鋏挟み(2.0) → 甲羅潰し(5.0) | ガードの有無で被ダメが大きく変わる |
| `ED_WaterSerpent` | 水蛇【ボス】 | 1000 | Thunder | 水弾(0.8)×3 → 渦潮(3.0) → 水弾(0.8)×2 → 大津波(5.0) | 連撃中に大技が混ざり、ガードの取捨選択を迫る |

### ステージ3「焔の廃寺」

| ファイル名 | enemyName | maxHp | weakElement | actionPattern | 設計意図 |
|---|---|---|---|---|---|
| `ED_WillOWisp` | 鬼火 | 250 | Water | 火の粉(1.5) | 予告0.8秒に慣れる。被ダメは小さい |
| `ED_BurningKodama` | 燃え木霊（枯れ木霊派生） | 350 | Water | 枝打ち(1.5) → 枝打ち(1.5) → 根縛り(2.5) | 枯れ木霊の間隔短縮版 |
| `ED_Kasha` | 火車 | 450 | Earth | 轢き(2.0) → 炎輪突進(4.0) | 短予告の大技に反応できるか |
| `ED_FlameDemon` | 炎魔【ボス】 | 1200 | Water | 業火拳(1.5)×2 → 焦熱波(2.0) → 業火拳(1.5) → 爆炎(7.0) | 普段は隙が短い。爆炎後の7秒に高印数術を当てる |

### ステージ4「影の深淵」

| ファイル名 | enemyName | maxHp | weakElement | actionPattern | 設計意図 |
|---|---|---|---|---|---|
| `ED_Shade` | 影法師 | 400 | Light | 影刺し(0.8) → 影刺し(4.0) → 影縫い(3.0) | 間隔が不規則でリズムを崩す |
| `ED_ShadowWillOWisp` | 影鬼火（鬼火派生） | 350 | Light | 火の粉(1.0) | 鬼火の間隔短縮版 |
| `ED_Nue` | 鵺 | 550 | Fire | 水弾(1.5) → 枝打ち(1.5) → 火の粉(1.5) → 雷鳴(4.0) | 過去ステージの攻撃の混成。予告で種類を読む |
| `ED_ShadowDragon` | 影龍【ボス】 | 1600 | Light（weakMultiplier 2.0） | 影爪(1.5)×2 → 黒雷(2.5) → 影爪(1.0) → 影縫い(2.0) → 冥獄(6.0) | 弱点倍率を高めにし、両手印の術を使う動機を強くする |

## 8. ステージデータ（StageData）

| ファイル名 | stageNumber | stageName | sections（先頭から順に。最後がボス） |
|---|---|---|---|
| `SD_Stage01_SealedVillage` | 1 | 封土の里 | MudBlob → WitheredKodama → MudGolem(isBoss) |
| `SD_Stage02_SunkenShrine` | 2 | 水底の社 | Kappa → StoneMudBlob → CrabMonk → WaterSerpent(isBoss) |
| `SD_Stage03_BurningTemple` | 3 | 焔の廃寺 | WillOWisp → BurningKodama → Kasha → FlameDemon(isBoss) |
| `SD_Stage04_ShadowAbyss` | 4 | 影の深淵 | Shade → ShadowWillOWisp → Nue → ShadowDragon(isBoss) |

- sections列は `ED_` プレフィックスを省略して記載している
- ボスセクションのみ `isBoss = true`
- 既存のStageDataアセットがある場合は、`SD_Stage01_SealedVillage` としてリネーム・更新するか新規作成するかを確認してから進めること

## 9. 完了条件

| # | 確認内容 |
|---|---|
| ① | 生成メニューの実行で全アセットが作成され、再実行しても重複しない |
| ② | 全アセットのファイル名が3節の命名規則に従い、日本語を含まない |
| ③ | 既存アセットのリネーム後もGUIDが維持され、既存の参照が切れていない |
| ④ | 全EnemyDataの actionPattern に null 参照がない |
| ⑤ | 大技指定の攻撃を使う行動がすべて isHeavy = true になっている |
| ⑥ | StageData×4 の sections が表の順序どおりで、最後のセクションのみ isBoss = true |
| ⑦ | 各ステージを通しで開始し、セクション遷移→ボス戦→クリアまで進行する（デバッグの無敵モード使用可） |
| ⑧ | 既存の術SOを使った既存の動作に退行がない |

## 10. 作業後に記録してほしいこと

- 作成・変更・リネームしたファイルとアセットの一覧（旧名→新名の対応を含む）
- `specification_MUDRA.md` の更新が必要な箇所の差分案
  - 3-2節: アセット命名規則（プレフィックス・英語ファイル名・表示名フィールドの使い分け）の追記
  - 11章B3内訳: 「4ステージ・雑魚11種（ユニーク8 + 派生3）」の反映
- 以下を `dev_log` の技術的負債・保留事項に追記する案
  - `isHeavy` を `EnemyAttackData` 側へ移すかの検討（現状は運用ルールで一貫性を担保）
  - ボスへのStun連続付与でパターンを止め続けられる可能性（スタン耐性の要否をB8で判断）
  - 敵HPはプレイヤーDPS≒20の仮定で逆算した値。実測DPSを取得してB8で掛け直す
  - PlayerMaxHp = 100 を前提にした被ダメ設計（ノーガードで1ステージあたりHPの7割〜1.2倍程度）
  - バランス調整用の外部スプレッドシート管理と、CSV取り込みによるSO更新の検討（B8）
