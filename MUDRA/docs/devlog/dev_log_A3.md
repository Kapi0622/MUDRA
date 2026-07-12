# 📋 開発ログ：α版 A3（手印拡充・術3種実装）

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** α版 A3
> **期間:** 2026/07/12
> **ステータス:** ✅ 完了

---

## 1. マイルストーン概要

| 項目 | 内容 |
|------|------|
| **完了条件** | 5種以上の手印が動作し、属性の異なる術が3種以上発動できる |
| **開発方針** | 手印入力の幅を広げ、ダメージ計算Strategyを拡充し、複数の術が実戦テンポで動作する状態を作る |
| **作業シーン** | A2から引き続き、MediaPipeサンプルシーンの複製を使用 |

---

## 2. 実装ファイル一覧

### Step 0: 仕様書齟齬の修正

| 対象 | 修正内容 |
|------|----------|
| `specification_MUDRA.md` Data Layerクラス一覧 | `EnemyAttackData`（ScriptableObject）・`EnemyAction`（Serializable struct）を追加 |
| `specification_MUDRA.md` `EnemyData`フィールド定義 | フラットなフィールド構成を廃止し、`EnemyAttackData`/`EnemyAction`のコード定義とパターン方式（`actionPattern`配列）に更新 |
| `specification_MUDRA.md` `EnemyStateManager`の状態 | Idle/Attacking/Stunnedの3状態 → Idle/Charging/Attacking/Stunnedの4状態に更新。enum+switch採用の判断理由を追記 |
| `specification_MUDRA.md` Presenter Layerクラス一覧 | `BattleInitializer`を追加 |

### Step 1: 親指判定・手印拡充

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `ThumbAngleDebugger.cs` | 新規作成→検証後削除 | 親指のCMC-MCP-IP / MCP-IP-TIP角度を実測する一時検証スクリプト |
| `HandTrackingService.cs` | 改修 | 4指判定→5指判定に拡張。親指専用閾値・7種の手印パターンマッチングを追加 |

### Step 2: ダメージ計算Strategy拡充

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `MultiHitCalculator.cs` | 新規作成 | 多段ヒットのダメージ計算（PerHitDamage算出） |
| `DamageOverTimeCalculator.cs` | 新規作成 | DoTの計算部分のみ（tick駆動はA5）。A3時点ではTotalDamageに初撃分のみ格納 |
| `DamageResult.cs` | 改修 | `PerHitDamage`フィールド追加（View演出用の1ヒットあたりダメージ） |
| `BattleModel.cs` | 改修 | `ResolveCalculator`のMultiHit/DoT分岐コメント解除、デフォルト分岐を`ArgumentOutOfRangeException`に変更 |

### Step 3: SpellDataアセット・配線

| アセット | 設定値 |
|----------|--------|
| 風刃（SpellData） | element: Wind, sequence: [Open], basePower: 30, damageType: SingleHit, hitCount: 1 |
| 雷連撃（SpellData） | element: Thunder, sequence: [Point, Scissors], basePower: 40, damageType: MultiHit, hitCount: 5 |
| 火炎弾（SpellData） | element: Fire, sequence: [Palm, Fist], basePower: 35, damageType: SingleHit, hitCount: 1 |

---

## 3. 設計判断の記録

### 3-1. 親指判定: パターンA（CMC-MCP-IP）の採用

`ThumbAngleDebugger`で実測した結果、2つの角度パターンを比較した。

| ポーズ | パターンA (CMC-MCP-IP) | パターンB (MCP-IP-TIP) |
|--------|----------------------|----------------------|
| Open | 9.9〜11.3° (幅1.4°) | 2.5〜9.8° (幅7.3°) |
| Fist | 25.6〜27.5° (幅1.9°) | 29.4〜39.4° (幅10.0°) |
| Palm | 36.6〜39.8° (幅3.2°) | 34.6〜41.2° (幅6.6°) |

パターンAはバラつきがBの半分以下で安定しており、Open最大値（11.3°）とFist最小値（25.6°）の間に14.3°のマージンがある。閾値`_thumbBentThreshold = 20f`を採用。

### 3-2. 5指パターンマッチングへの拡張

A2までの4指（人差し指〜小指）判定では、OpenとPalmの区別がつかなかった。親指の曲げ状態（`thumbBent`）を加えた5つのboolで全7種の手印を一意に識別する方式に拡張。

| HandSign | 親指 | 人差し | 中指 | 薬指 | 小指 |
|----------|------|--------|------|------|------|
| Open | O | O | O | O | O |
| Fist | X | X | X | X | X |
| Point | X | O | X | X | X |
| Scissors | X | O | O | X | X |
| Palm | X | O | O | O | O |
| Release | X | O | X | X | O |
| Cancel | X | X | X | X | O |

全パターンが衝突なく識別可能であることを確認済み。

### 3-3. IsFingerBentの閾値分離

親指は他4指と関節構造・動き方の軸が異なるため、`_bentThreshold`（他4指用、45°）とは別に`_thumbBentThreshold`（親指用、20°）を導入。`IsFingerBent`メソッドにnullable optionalの`threshold`パラメータを追加し、呼び出し側で明示的に渡す形にした。既存の4指呼び出しはデフォルト値（null → `_bentThreshold`にフォールバック）で影響なし。

### 3-4. MultiHitのView分割方式

MultiHitのダメージ適用について、Model側で時間差ループを回す案とView側で演出分割する案を比較し、View分割方式を採用。

- Model側に演出テンポ（hit間隔）の関心を持ち込まない
- HPバーの自然な減少はLitMotionのトゥイーンで担保できる
- `DamageResult`に`PerHitDamage`を1フィールド追加するだけで、Presenter→Viewが`HitCount`回分の演出を制御できる

実際のView演出実装はA4（UI・演出の基礎）で対応する。

### 3-5. DoTCalculatorのスコープ限定

DoTの処理を「計算」と「適用スケジュール管理」に分解した結果、後者はStatusEffect管理（Stunの「一定時間行動を止める」と同じライフサイクル）と同じ基盤で実装すべきと判断。A3ではCalculatorの計算ロジックのみ実装し、tick駆動基盤はGuarding・StatusEffectと合わせてA5で実装する。

A3時点の`DamageOverTimeCalculator`は初撃ダメージ（basePower × 0.3 × 弱点倍率）のみを`TotalDamage`に格納し、`perTickDamage`と`tickCount`は算出するが`DamageResult`には未反映（A5でフィールド追加予定）。

### 3-6. DoTのtickダメージに速度・コンボ倍率を乗せない設計

DoTの設計意図は「一度付与したら固定値で刻む」こと。速度ボーナスとコンボ倍率は初撃のみに適用し、tickダメージには弱点倍率のみを適用する。

---

## 4. 統合テスト結果

全17項目パス。

### 手印認識

| # | テスト項目 | 結果 |
|---|-----------|------|
| ① | Open（全指伸び）が認識される | ✅ |
| ② | Fist（全指曲げ）が認識される | ✅ |
| ③ | Point（人差し指のみ）が認識される | ✅ |
| ④ | Scissors（人差し指+中指）が認識される | ✅ |
| ⑤ | Palm（親指のみ曲げ）が認識される | ✅ |
| ⑥ | Release（親指曲げ+人差し指+小指）が認識される | ✅ |
| ⑦ | Cancel（小指のみ）が認識される | ✅ |
| ⑧ | OpenとPalmが混同しない | ✅ |

### 術発動

| # | テスト項目 | 結果 |
|---|-----------|------|
| ⑨ | 風刃: Open → Release でボスHP減少 | ✅ |
| ⑩ | 雷連撃: Point → Scissors → Release でボスHP減少 | ✅ |
| ⑪ | 火炎弾: Palm → Fist → Release でボスHP減少 | ✅ |
| ⑫ | 不正シーケンス → Release で暴発 | ✅ |
| ⑬ | Cancel でシーケンスリセット | ✅ |

### ダメージ計算

| # | テスト項目 | 結果 |
|---|-----------|------|
| ⑭ | 弱点属性で倍率が乗る（風刃 vs 泥人形） | ✅ |
| ⑮ | 非弱点属性で等倍（火炎弾 vs 泥人形） | ✅ |
| ⑯ | コンボ倍率が蓄積する | ✅ |
| ⑰ | MultiHitのHitCount・PerHitDamageがDamageResultに反映 | ✅ |

---

## 5. スコープ調整の記録

### A3スコープから除外しA5（新設）に延期した機能

| 機能 | 延期理由 |
|------|----------|
| Guarding状態（Fist文脈判定） | A3の完了条件に不要。StatusEffect基盤と合わせて実装した方が整合性が高い |
| StatusEffect付与ロジック / Stunned状態駆動 | DoTのtick駆動と同じライフサイクル管理基盤が必要 |
| DoTのtick駆動 | 「計算」と「適用スケジュール管理」を分離し、後者はStatusEffect基盤と同じ問題のためA5で一括実装 |

### マイルストーン再配置

```
A3(〜07/19): 手印拡充 + Calculator + 術3種 ← 完了
A4(〜07/22): SpellCastResult導入 + UI・演出の基礎
A5(新設):    Guarding + StatusEffect + DoTtick駆動
β版(〜08/05): 全ステージ・チュートリアル・バランス調整・最終ポリッシュ
```

---

## 6. 技術的負債・保留事項

| 負債 | 対処方針 |
|------|----------|
| `DamageOverTimeCalculator`のperTickDamage/tickCountが`DamageResult`に未反映 | A5でStatusEffect基盤と合わせて`DamageResult`にフィールド追加 |
| `ResolveCalculator`が毎回newしている | A2から継続。ステートレスなので実害なし。Calculator種類が増えたらFactory抽出を検討 |
| `SpellSequenceRunner`の将来的なリネーム | A1から継続。`HandSignPresenter`への整理が必要 |
| MultiHitのView側演出分割 | A4でUI実装時に`PerHitDamage`を使った時間差表示を実装 |
| 火炎弾のDamageTypeがSingleHit（暫定） | A5でDoTtick駆動基盤が完成した時点でDamageOverTimeに切り替え |
| 演出系フィールド（effectPrefab, castSE等）が全SpellDataでnull | A4で埋める |
| `BattlePresenter`のデバッグ表示がDebug.Logのみ | A2から継続。A4でUI実装時に正式なViewクラスに差し替え |

---

## 7. A4への引継ぎ

**A4完了条件:** SpellCastResult導入 + UI・演出の基礎が動作する

### 前提として把握しておくべきこと

- 手印7種（Open/Fist/Point/Scissors/Palm/Release/Cancel）はすべて動作確認済み
- 術3種（風刃/雷連撃/火炎弾）が実戦テンポで発動し、ダメージ計算が正しく動作する
- `DamageResult.PerHitDamage`が追加済みのため、MultiHitのView演出分割に対応可能
- `SpellCastResult`はA2から延期されている（dev_log_A2 4-6節参照）。SpeedBonus計測の実装と合わせてA4で導入予定

### A4で新たに必要になるもの

- `SpellCastResult`の導入（SpeedBonus計測を含む）
- HPバー（プレイヤー・ボス）のUI実装
- 術名テロップ・コンボ表示
- MultiHitの時間差ダメージ表示演出
- `BattleView`/`EnemyView`の正式実装（Debug.Logからの移行）
- MediaPipeサンプルシーンの既存UI整理
