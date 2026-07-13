# 📋 開発ログ：α版 A4（UI・演出の基礎）

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** α版 A4
> **期間:** 2026/07/14〜07/15
> **ステータス:** ✅ 完了（SequenceGuideのレイアウト調整は次セッションで継続）

---

## 1. マイルストーン概要

| 項目 | 内容 |
|------|------|
| **完了条件** | HPバー・印シーケンスガイド・術名テロップ・印確定エフェクトが表示される |
| **開発方針** | Step 1でModel層の変更（SpellCastResult導入）を先行させ、その上にUI Viewを積む。手戻り最小の着手順序を徹底する |
| **作業シーン** | MediaPipeサンプルシーンを整理し、BattleCanvasを新設して使用 |

---

## 2. 実装ファイル一覧

### Step 0: シーン整理・Canvas新設

| 対象 | 作業内容 |
|------|----------|
| Main Canvas > Container Panel | 非アクティブ化。Header/MenuButton等を温存 |
| Footer / Graph Config / ImageSource | Container Panel外に移動し、開発用設定パネルとして維持 |
| Body > Annotatable Screen | Container Panel外に移動し、画面右下にリサイズ（カメラプレビュー小窓化） |
| SpellText（`_debugSignText`） | 削除。Step 3のSequenceGuideに役割を移行 |
| BattleCanvas（新規） | Sort Order: 1、Screen Space - Overlay、CanvasScaler: Scale With Screen Size (1920×1080) |
| PlayerHpBar / BossHpBar | BattleCanvas配下に空GameObjectとして仮配置（Step 2の土台） |

### Step 1: SpellCastResult導入 + SpeedBonus計測

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `SpellCastResult.cs` | 新規作成 | readonly struct。IsSuccess / Spell / SpeedBonus の3フィールド。ComboCountはBattleModelの責務として含めない |
| `SpellSequenceModel.cs` | 改修 | `Func<float> getTime`をDI、詠唱時間計測（`_chantStartTime`）、`CalculateSpeedBonus`追加、`OnSpellCast`型を`SpellData`→`SpellCastResult`に変更、暴発を`OnSpellCast(IsSuccess=false)`に統合、Cancel時の`_matchCandidates`リセット漏れ修正 |
| `SequenceResetReason` | 改修 | `Misfire`値を削除。Cancel専用に整理 |
| `BattleModel.cs` | 改修 | `ApplySpellDamage(SpellCastResult)`にシグネチャ変更。成功/暴発の分岐を内部に集約。`ApplyMisfireDamage()`をprivateに変更 |
| `BattleInitializer.cs` | 改修 | `SpellSequenceModel`コンストラクタに`() => Time.time`を注入（1行追加） |
| `BattlePresenter.cs` | 改修 | `OnSpellCast`購読の型合わせ、`OnSequenceReset`のMisfire分岐を削除 |
| `SpellSequenceRunner.cs` | 改修 | `HandleSpellCast`をSpellCastResult対応に変更、`_playerStateManager.HandleSpellCast()`を成功時のみに制限（`.Where(result => result.IsSuccess)`） |

### Step 2: HPバーUI

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `HpBarView.cs` | 新規作成 | 2層遅延ダメージバー（CurrentBar即時反映 + DelayedBar遅延追従）。LitMotionの`.Bind()`でfillAmountをトゥイーン。Player/Boss共通クラス |
| `BattlePresenter.cs` | 改修 | `HpBarView`×2を受け取り、`PlayerHp`/`BossHp`の購読でSetHpを呼ぶ。`Skip(1)`で初回通知スキップ |
| `BattleInitializer.cs` | 改修 | `[SerializeField] HpBarView`×2を追加、`BattlePresenter.Initialize`に引数追加 |

### Step 3: シーケンスガイド + 印確定エフェクト

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `SequenceGuideView.cs` | 新規作成 | Prefabベースで候補行を動的生成。MaxDisplayCount上限、確定スロットのScalePunchエフェクト、MotionHandleの安全なキャンセル管理 |
| `SpellSequenceModel.cs` | 改修 | `MatchCandidates`/`InputCount`プロパティ（read-only公開）と`OnSignAdded`イベントを追加 |
| `SpellSequenceRunner.cs` | 改修 | `_debugSignText`削除、`_sequenceGuideView`追加、`OnSignAdded`購読でガイド更新+確定エフェクト発火、発動/リセット時にClear |
| CandidateRow.prefab | 新規作成 | 術1行分のUI。HorizontalLayoutGroup + SpellNameText + SlotContainer |
| SignSlot.prefab | 新規作成 | 印1つ分のスロット。Image（背景）+ TextMeshProUGUI（日本語フォント対応） |

### Step 4: 術名テロップ + 暴発フィードバック

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `SpellTelopView.cs` | 新規作成 | CanvasGroupのalphaフェードとScale演出で術名/暴発テキストを表示。1つのTextMeshProUGUIを成功/暴発で共用 |
| `SpellSequenceRunner.cs` | 改修 | `_spellTelopView`追加。HandleSpellCast内で成功時にShowSpellName、暴発時にShowMisfireを呼ぶ |

---

## 3. 設計判断の記録

### 3-1. SpeedBonusの時間計測: Func\<float\> DIの採用

`SpellSequenceModel`はPure C#であり`UnityEngine.Time`への直接依存を避ける必要がある。候補として「インターフェース（`ITimeProvider`）」と「関数注入（`Func<float>`）」を比較し、後者を採用。

- 差し替えたいものが「現在時刻を返す」という単一の関数のみであり、インターフェースは過剰
- `Func<float>`は追加ファイルなしで導入でき、テスト時は`() => fakeTime`で偽装可能
- 本番では`BattleInitializer`が`() => Time.time`を渡すため、UnityEngine依存はMonoBehaviour層に閉じ込められる

Tick方式（毎フレーム積算）はタイムアウト廃止により不要。詠唱開始とReleaseの2瞬間だけ時刻を取得すればよいため、イベント駆動のFunc方式が最適。

### 3-2. SpellCastResultからComboCountを削除

仕様書6-4のstruct定義にはComboCountがあったが、コンボは`BattleModel`の管轄であり`SpellSequenceModel`は知るべきでないと判断。`BattleModel.ApplySpellDamage(SpellCastResult)`が自身の`ComboCount`を直接参照する形で責務を分離。View側でコンボ数を表示する場合は`BattleModel.ComboCount`（ReactiveProperty）を購読すればよい。

### 3-3. 暴発通知の一本化

タイムアウト仕様の廃止（ゲームプレイ上の判断）により、`SequenceResetReason`は実質Cancelのみとなった。暴発は「発動を試みた結果の失敗」であり「発動に至らずリセットされた」とは意味が異なるため、`OnSpellCast(IsSuccess=false)`に統合。`OnSequenceReset`はCancel専用の演出フックとして残す。

### 3-4. HPバーの2層遅延ダメージバー方式

格闘ゲームの定番パターンを採用。CurrentBar（即時反映、0.2秒トゥイーン）とDelayedBar（0.5秒待機後に0.6秒で追従）の2層構成。LitMotionの`.Bind(x => image.fillAmount = x)`で実現。`BindToFillAmount`等の専用拡張メソッドはLitMotionに存在しないため、汎用の`.Bind()`を使用。

連続ダメージ時は`TryCancelMotion`で実行中のMotionHandleをキャンセルしてから新しいモーションを作成し、バーの途中停止や二重アニメーションを防止。

### 3-5. シーケンスガイドのPrefab方式採用

当初はコード内で`new GameObject` + `AddComponent`チェーンで動的生成していたが、以下の理由でPrefab方式に移行。

- フォント・色・余白の調整をコード修正なしにInspectorで行える
- β版でテキスト→Sprite差し替え時にPrefab内部の変更のみで対応可能
- ポートフォリオとしてのコード品質（動的生成の連打はプロトタイプ品質）

Destroy/再生成方式は術3種・最大印数2の現状では負荷は皆無。術10種以上になった場合はオブジェクトプールへの切り替えを検討。

### 3-6. OnSignAdded vs OnSignConfirmed の命名判断

「手印の確定（安定判定を通過した）」は`HandTrackingService.OnHandSignRecognized`の責務、「シーケンスに印が追加された（候補絞り込み完了後）」は`SpellSequenceModel.OnSignAdded`の責務。シーケンスガイドの更新タイミングは後者（候補絞り込み後の状態）が必要なため、Model側に追加。

### 3-7. テロップのCanvasGroup方式

TextMeshProUGUIの`color.a`を直接操作する代わりに`CanvasGroup.alpha`でフェードする方式を採用。将来テロップに背景画像やアイコンを追加した場合も、配下の全要素をまとめてフェードできるため拡張に強い。術発動と暴発は排他的なので1つのTextMeshProUGUIを共用し、テキストと色のみ切り替える。

### 3-8. Canvas構成の設計

MediaPipe系UI（Main Canvas, Order 0）とバトルUI（BattleCanvas, Order 1）を分離。BattleCanvasはScreen Space - Overlayで独立した座標系のため、Main CanvasがScreen Space - Camera（Reference Resolution 2436×1125）でも干渉しない。開発用の設定パネル（Footer/Graph Config/ImageSource）はMain Canvas側に残し、バトルUIの背面に隠れる形で温存。

---

## 4. 統合テスト結果

全21項目中20項目パス。1項目は動作確認済みだがレイアウト調整が残存。

### HPバー（Step 2）

| # | テスト項目 | 結果 |
|---|-----------|------|
| 1 | 開始時にPlayer/BossのHPバーが満タン表示 | ✅ |
| 2 | ボスの攻撃でPlayerHPバーが即時減少し、DelayedBarが遅れて追従する | ✅ |
| 3 | 術発動でBossHPバーが即時減少し、DelayedBarが遅れて追従する | ✅ |
| 4 | 暴発でPlayerHPバーが減少する（MaxHpの5%） | ✅ |
| 5 | 連続ダメージでバーが途中で止まらず最新値に追従する | ✅ |
| 6 | HPが0になった側のバーがゼロまで到達する | ✅ |
| 7 | HpTextの数値がバーの動きと一致する | ✅ |

### シーケンスガイド（Step 3）

| # | テスト項目 | 結果 |
|---|-----------|------|
| 8 | 詠唱印を入力するとマッチ候補が表示される | ✅ |
| 9 | 入力が進むと候補が絞り込まれて消える | ✅ |
| 10 | 確定した印のスロットが色変化+ScalePunchする | ✅ |
| 11 | Release成功でガイドがクリアされる | ✅ |
| 12 | Cancelでガイドがクリアされる | ✅ |
| 13 | 暴発でガイドがクリアされる | ✅ |

### 術名テロップ + 暴発フィードバック（Step 4）

| # | テスト項目 | 結果 |
|---|-----------|------|
| 14 | 術発動成功時に術名が画面中央にフェードイン→フェードアウトする | ✅ |
| 15 | 暴発時に「暴発」が赤色で表示される | ✅ |
| 16 | 連続発動で前のテロップがキャンセルされ新しいテロップに切り替わる | ✅ |

### SpeedBonus（Step 1）

| # | テスト項目 | 結果 |
|---|-----------|------|
| 17 | 素早い詠唱でダメージが1.5倍になる | ✅ |
| 18 | ゆっくり詠唱で等倍ダメージ | ✅ |

### 全体結合

| # | テスト項目 | 結果 |
|---|-----------|------|
| 19 | 勝利時にバトル終了ログが出てボスの行動が停止する | ✅ |
| 20 | 敗北時にバトル終了ログが出てボスの行動が停止する | ✅ |
| 21 | バトル終了後に術発動してもHP変化が起きない | ✅ |

---

## 5. バグ修正の記録

### 5-1. Cancel時の_matchCandidatesリセット漏れ

既存の`Cancel()`は`_inputHistory`のみクリアしていたが`_matchCandidates`が絞り込まれたままだった。Cancel後の新しい詠唱で候補が漏れる可能性があったため、`ResetState()`呼び出しに統一。

### 5-2. PlayConfirmEffectのNullReferenceException

LitMotionのトゥイーン実行中に次の`UpdateGuide`→`Clear()`でGameObjectがDestroyされ、ラムダ内で`_lastConfirmedSlot`がnullになりNullReferenceが発生。対処として`_confirmEffectHandle`を保持しClear時にキャンセル、加えてBind内でnullチェックを追加。

### 5-3. 日本語フォント未設定による文字化け

コード内で`AddComponent<TextMeshProUGUI>()`すると、デフォルトフォント（LiberationSans）が適用され日本語が四角に表示される。Prefab方式に切り替え、日本語フォントアセットをPrefab側で設定する形に修正。

---

## 6. 技術的負債・残存課題

| 負債・課題                                           | 対処方針                                                   |
| ----------------------------------------------- | ------------------------------------------------------ |
| `SpellSequenceRunner`の`HandSignPresenter`へのリネーム | A1から継続。Step 5で予定していたが次セッションに持ち越し                       |
| MultiHitの時間差ダメージ表示（PerHitDamage使用）              | A4ストレッチ扱いで未着手。HPバーのトゥイーン基盤は構築済みのため、時間差トゥイーンを重ねるだけで実装可能 |
| `EnemyView`の攻撃予告演出（Charging状態の可視化）              | A4ストレッチ扱いで未着手                                          |
| SpellDataの演出系フィールド（effectPrefab/castSE）が未設定     | β版のビジュアル作業フェーズで対応                                      |
| `ResolveCalculator`が毎回newしている                   | A2から継続。ステートレスなので実害なし                                   |
| 火炎弾のDamageTypeがSingleHit（暫定）                    | A5でDoTtick駆動基盤が完成した時点でDamageOverTimeに切り替え              |
| SpeedBonus定数がconst（バランス調整時にSO化の検討余地あり）          | β版B3でバランス調整が本格化した段階で判断                                 |

---

## 7. 仕様書の更新対象（A4完了時点）

以下の箇所が仕様書と実装の間で乖離しているため、仕様書の更新が必要。

| 仕様書の該当箇所 | 更新内容 |
|------|----------|
| 6-1. SpellSequenceModel の保持データ | `Func<float> _getTime`（DI）、`_chantStartTime`（詠唱開始時刻）を追加。`MatchCandidates`/`InputCount`のread-onlyプロパティ、`OnSignAdded`イベントを追記 |
| 6-1. SpellSequenceModel のR3通知用 | `OnSpellCast`の型を`Observable<SpellData>`→`Observable<SpellCastResult>`に変更。「A4でSpellCastResultへの移行を予定」の注記を削除 |
| 6-2. シーケンス照合フロー | 暴発時の経路を`OnSequenceReset(Misfire)`から`OnSpellCast(IsSuccess=false)`に変更 |
| 6-3. 発動判定フロー | 同上。「A4での変更予定」の注記を「A4で実装済み」に更新 |
| 6-4. SpellCastResult | `ComboCount`フィールドを削除。readonly structとして定義を確定。「A4導入予定」→「A4で導入済み」に更新 |
| 6-2. 未実装注記 | 「印間タイムアウト（2.0秒）」の行を削除（ゲームプレイ上の判断で廃止確定） |
| 7-1. BattleModel 主要メソッド | `ApplySpellDamage(SpellData, float)`→`ApplySpellDamage(SpellCastResult)`に変更。`ApplyMisfireDamage()`をprivateに変更。「speedBonusはA4で〜」の注記を削除 |
| 7-2. 共通倍率表 速度ボーナス行 | 「制限時間の50%以内で全印完了: 1.5」→「印数 × 1.0秒以内で全印完了: 1.5」に更新 |
| 8-1. Battle MVP | 購読イメージのコードを`SpellCastResult`対応に更新。`BattleView`→`HpBarView`/`SpellTelopView`に分離した実態を反映 |
| 8-2. HandSign MVP | `OnSignConfirmed`→`OnSignAdded`に更新。`SequenceGuideView`の追加を反映 |
| 2-1. クラス一覧 View Layer | `BattleView`を`HpBarView`/`SpellTelopView`に分割。`HandSignView`に`SequenceGuideView`を追加 |
| 10-2. 未決定の仕様 | 「印間タイムアウト」の行を削除（廃止確定） |

---

## 8. A5への引継ぎ

**A5完了条件:** Fist文脈判定によるGuarding状態が機能し、Stun付与でボスのStunned状態が実際に駆動し、DoTのtick処理が毎秒ダメージを与え続ける

### 前提として把握しておくべきこと

- SpellCastResult（IsSuccess/Spell/SpeedBonus）が導入済み。成功/暴発は`OnSpellCast`の単一ストリームで通知される
- HPバー（2層遅延ダメージバー）、シーケンスガイド（Prefabベース動的生成）、術名テロップ（CanvasGroupフェード）が動作している
- タイムアウト仕様はゲームプレイ上の判断で廃止確定。`SequenceResetReason`はCancel専用
- BattleCanvasはSort Order 1で独立配置。Main Canvas（MediaPipe系）とは非干渉
- `BattleModel.ApplyMisfireDamage()`はprivateに変更済み。外部からは`ApplySpellDamage(SpellCastResult)`経由でのみ呼ばれる
- SpeedBonusの閾値定数（`SpeedBonusTimePerSign = 1.0f`）はconst。バランス調整時にSO化を検討

### A5で新たに必要になるもの

- Fist文脈判定（Idle中のFist保持→Guarding状態遷移）
- `PlayerStateManager`へのGuarding状態追加
- StatusEffect管理基盤（Stun/Slow/DoTの共通ライフサイクル）
- DoTのtick駆動（`DamageResult`へのperTickDamage/tickCountフィールド追加）
- `EnemyStateManager`のStunned状態駆動の実接続

### 残存課題（A4から持ち越し）

- `SpellSequenceRunner`→`HandSignPresenter`へのリネーム
- 仕様書の更新（7章の更新対象リスト参照）
