# 📋 開発ログ：β版 B4（バトル演出・UI強化）

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** β版 B4
> **期間:** 2026/10/02〜10/05
> **ステータス:** ✅ 完了（未コミット）

---

## 1. マイルストーン概要

| 項目 | 内容 |
|------|------|
| **完了条件** | 全ての戦闘アクションに対応する視覚フィードバックが存在する。ログを見なくても何が起きているか画面だけでわかる |
| **開発方針** | 描画構成の組み替え → Modelの通知追加 → 敵側の演出 → プレイヤー側の演出 → バトルUI → ボス戦専用演出 の順。アートは仮素材で、仕組みを先に完成させる |
| **位置づけ** | B3まで `Debug.Log` にしか出ていなかった敵の行動・被弾・ガード・コンボ・StatusEffectを、すべて画面で読み取れるようにする |
| **進め方** | シーン・アセットの組み立ては Unity MCP（coplaydev/unity-mcp）で行い、Playモードでの確認はユーザーが行った |

---

## 2. 実装ファイル一覧

### Step 0: 描画構成の組み替え（シーン・設定のみ）

| 対象 | 変更 |
|------|------|
| `ProjectSettings/TagManager.asset` | Sorting Layer `Background` < `Enemy` < `Effect` < `CameraPreview` を追加 |
| `InGame.unity` | 背景・敵を UI Image から `BattleField` 配下の SpriteRenderer に移動。カメラを Perspective → Orthographic（size 5）。MediaPipe のカメラ映像 Canvas を `CameraPreview` レイヤーへ |
| `Particle/ParticleSystemTest.prefab` | Sorting Layer を `Effect` に |

### Step 1: Model の通知追加

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `Model/PlayerDamageInfo.cs` | 新規 | 被弾内容（量・ガード有無・大技か・暴発か）。`SpellCastResult` と同じ形の readonly struct |
| `Model/BattleModel.cs` | 改修 | `OnSpellHit`（`DamageResult`）/ `OnDotTick` / `OnPlayerDamaged` / `OnHealed` |
| `Model/StatusEffect/GuardWindowManager.cs` | 改修 | `IsGuarding` を `ReadOnlyReactiveProperty<bool>` 化。`IDisposable` 化。コメントの受付時間を実値（1秒）に修正 |
| `Model/StatusEffect/StatusEffectManager.cs` | 改修 | `OnEffectApplied` / `OnEffectRemoved`（期限切れ・`ClearEnemyEffects`・`ClearAll` の全経路で発火）。`IDisposable` 化 |
| `Model/SectionProgressManager.cs` | 改修 | `OnStageStarted`。`TransitionDuration` 1.5 → 2.5秒。撃破直後に倒した敵の行動ループを止める（§3-4）。1手目までの待機（雑魚1.0秒・ボス3.0秒）を決めて渡す |
| `Model/EnemyStateManager.cs` | 改修 | `SetEnemy` / `StartLoop` が1手目までの待機を受け取る |

### Step 2: 敵側の演出（Enemy MVP）

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `View/EnemyView.cs` | 新規 | 登場・攻撃予告（攻撃名＋ゲージ＋明滅）・攻撃・被弾・DoT被弾・Stun・撃破。ボスの登場（影から出現）と撃破（爆散） |
| `View/BackgroundView.cs` | 新規 | 道中/ボスの背景切替、前進演出（ズーム＋暗転→明転） |
| `View/StageTitleView.cs` | 新規 | ステージ名・ボス名の表示 |
| `Presenter/EnemyPresenter.cs` | 新規 | ステージ進行に沿った表示（敵・背景・ステージ名・決着・ボス演出）の配線 |
| `Presenter/BattleInitializer.cs` | 改修 | `EnemyPresenter` の初期化。`StatusEffectManager` / `GuardWindowManager` の `Dispose` |

### Step 3: プレイヤー側の演出

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `View/ScreenFlashView.cs` | 新規 | 画面全体の色フラッシュ（被弾=赤、ガード=青白、暴発=紫、ボス撃破=白） |
| `View/CameraShakeView.cs` | 新規 | カメラの揺れ（被弾・ガード・ボスの咆哮・ボス撃破） |
| `View/GuardView.cs` | 新規 | ガード受付中の構え枠、ガード成功の「防」 |
| `View/SpellEffectView.cs` | 改修 | 術ごとのエフェクト（`SpellData.effectPrefab`）、敵の攻撃エフェクト。出現位置を敵側/プレイヤー側の2つに |
| `View/SpellTelopView.cs` | 改修 | `ShowSpellName` → `ShowCutIn`（属性色の帯が滑り込むカットイン） |
| `Data/SpellData.cs` | 改修 | `effectOnCaster`（回復術などエフェクトを術者側に出す） |
| `Presenter/BattlePresenter.cs` / `HandSignPresenter.cs` | 改修 | 上記の配線 |

### Step 4: バトルUI

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `View/DamageNumberView.cs` | 新規 | ダメージ数字・回復量。MultiHitの時間差表示、「弱点！」「迅速！」。`ObjectPool` で使い回し |
| `View/ComboView.cs` | 新規 | 「n 連」の表示とコンボ切れの演出 |
| `View/StatusIconView.cs` | 新規 | 継続中の効果のアイコン（蝕・封・癒） |
| `View/BattleResultView.cs` | 新規 | 「討伐」「敗北」の仮の決着表示（B5のリザルト画面までのつなぎ） |

### 追加: ボス戦専用演出・StatusEffect表現の汎用化

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `View/BossPresentationTiming.cs` | 新規 | ボス演出の時間表（const のみ）。複数のViewが同じ表で動く。**B4後の整備で削除**（§3-11） |
| `View/BossEncounterView.cs` | 新規 | 登場時の暗転と「強敵出現」の帯、大技の特別予告（ビネット＋「大技：○○」）、撃破時のヒットストップ |
| `View/EnemyView.cs` ほか | 改修 | ボスの登場・撃破、咆哮の揺れ、白フラッシュ、ボス名・「討伐」の遅延表示。DoTの色を紫に |

### 追加: 演出値の整備（B4後、§3-11）

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `Data/PresentationTimingData.cs` | 新規 | 演出の時間表（SO）。`BossPresentationTiming` と Model の待機定数を置き換え |
| `Data/BattlePaletteData.cs` | 新規 | 複数の View で使う意味の色（SO）。DoT・回復・ガード・弱点・属性色 |
| `View/BattleUiConstants.cs` | 新規 | 調整しない共有値（帯の画面外距離 2400、「大技」） |
| `Model/SectionProgressManager.cs` / `Presenter/BattleInitializer.cs` / `Presenter/EnemyPresenter.cs` | 改修 | 時間表を注入。待機時間は時間表のプロパティから取る |
| `View/` の6本（Enemy / Background / BossEncounter / SpellTelop / DamageNumber / ScreenFlash） | 改修 | 尺・共有色を SO から読む |

---

## 3. 設計判断の記録

### 3-1. 背景と敵は SpriteRenderer、HUD だけ Overlay Canvas

B3までは背景も敵も Overlay Canvas 上の UI Image だった。Overlay はワールドの後に描かれるため、ワールド空間の Particle System（術エフェクト）が必ず UI の裏に隠れる。背景と敵をワールドの SpriteRenderer に移し、Sorting Layer（`Background` < `Enemy` < `Effect`）で重なり順を決めた。HUD は Overlay に残すので常に最前面。カメラの揺れもワールドだけに効き、HUD は揺れない。

背景は元々 MediaPipe サンプル由来の `Main Canvas`（Screen Space - Camera）に載っており、同じ Canvas の右下にカメラ映像がある。Sorting Layer を足すと映像が背景の裏に隠れるため、この Canvas を最前面の `CameraPreview` レイヤーに移した。

### 3-2. 「何が起きたか」の通知を Model に足す

HP の `ReactiveProperty` は「値がいくつになったか」しか伝えない。ダメージ数字・弱点表示・被弾とガード成功の出し分けには「何が起きたか」が要るため、`BattleModel` に出来事の通知（`OnSpellHit` 等）を足した。いずれも HP を書き換えた直後・`CheckBattleEnd` の前に発火し、トドメの一撃でも演出が出る。被弾は敵の攻撃と暴発を `PlayerDamageInfo` 1つにまとめた（`SpellCastResult` と同じ方針）。無敵モード中は流さない。

### 3-3. 演出の重なりは「成分ごとに持って1か所で合成」

敵の演出は同時に重なる（予告中に被弾、Stun中にDoT、ボスの影からの登場中に揺れ）。各演出が SpriteRenderer の色や Transform を直接書くと互いを上書きするため、`EnemyView` は演出ごとに自分の成分（予告の明滅・フラッシュ・影・透明度・揺れ・踏み込み・拡縮）だけを持ち、`ApplyVisual()` で合成して反映する。`BackgroundView` も同じ形。

### 3-4. 倒した敵の行動ループを撃破直後に止める（B3からの不具合）

B3では、敵を倒してから次の敵が出るまでの待機中も、倒した敵の行動ループが回り続けていた。ダメージは `_isBattleActive` で弾かれるので表に出ていなかったが、演出を付けると死んだ敵から予告・攻撃が出る。`SectionProgressManager.AdvanceAsync` で、通知スタックから抜けた後（B3 §3-3 と同じ理由）に `ClearEnemyEffects` → `StopLoop` を行うようにした。効果のクリアを先にするのは、Stun 解除（`EndStun`）がループを再開させても後の `StopLoop` で確実に止めるため。

### 3-5. 演出の尺は Model が「待つ」、View が「合わせる」

Model は View を知らないので演出の完了を待てない。そこで演出の尺の合計を Model 側の定数として確保した。

| 定数（`SectionProgressManager`） | 値 | 中身 |
|---|---|---|
| `TransitionDuration` | 2.5秒 | 撃破演出 0.8 + 前進演出 1.2 + 余白 |
| `NormalEntryDelay` | 1.0秒 | 雑魚の登場 0.6 + 余白。この間は1手目の予告を出さない |
| `BossEntryDelay` | 3.0秒 | ボスの登場シーケンス 2.4 + 余白 |

1手目までの待機は、当初 `EnemyStateManager` の const（全敵共通1.0秒）だったが、ボスだけ長くするため `SetEnemy` の引数にし、`TransitionDuration` と同じ `SectionProgressManager` に集めた。View 側の尺を変えたらこれらも合わせる（コメントに明記）。

> B4後の整備で `PresentationTimingData` に移し、待機は尺から計算するようにした（§3-11）。

### 3-6. ボス演出の時間表を1か所に置く

ボスの登場・撃破は、暗転・敵本体・カメラ・ボス名・決着表示の5つの View にまたがって決まった順番で起きる。Presenter にタイマーを持たせない（ロジックを持たない規約）ため、各 View のメソッドが「開始までの遅延」を受け取り、秒数は `BossPresentationTiming` に集約した。Presenter は同じ瞬間に全部を呼ぶだけで、順番は時間表が保証する。

> B4後の整備で `PresentationTimingData` に移した（§3-11）。

### 3-7. StatusEffect は属性ではなく効果の種類で表す

当初はアイコンを「焼」（DoT・橙）「痺」（Stun・青）にしていたが、今の術が火=DoT・雷=Stun だから成り立っているだけで、別属性の DoT/Stun を作ると違和感が出る。文字も色も効果の種類で決めるようにした（DoT＝「蝕」暗い紫、Stun＝「封」白銀。「封」は印で封じるの意で世界観にも合う）。DoT の tick の数字・敵のフラッシュも紫に揃えた。付けた術の属性色まで出す案は、効果に属性を持たせる Model の変更が要るため、別属性の DoT/Stun が実際に増えたら B8 で検討する。

### 3-8. 初期化を Awake 任せにしない View

最初の敵の登場・ステージ名・コンボ0などは `BattleInitializer.Awake` 内の `StartStage` と購読から流れてくるため、オブジェクト間の Awake 順によっては View の Awake より先に公開メソッドが呼ばれる。`EnemyView` / `StageTitleView` / `ComboView` / `StatusIconView` / `BattleResultView` は `EnsureInitialized()` で「最初に使われた時か Awake の早いほう」で1回だけ初期化する。特に `EnemyView` は基準の拡縮を記録するので、順番が逆だと拡縮0を基準にして敵が見えなくなる。

### 3-9. 回復術で被弾演出を出さない

回復術（威力0）も `OnSpellHit` を流すため、敵が被弾フラッシュし、「0」の数字も出てしまう。Presenter 側で `TotalDamage > 0` のときだけ被弾演出・数字を出す。`OnSpellHit` 自体は回復術でも流す（コンボは加算されるため、「術が成立した」通知としては正しい）。

### 3-10. ヒットストップは `Time.timeScale`

ボス撃破のヒットストップは `Time.timeScale = 0` を実時間0.2秒だけ行う（UniTask の `ignoreTimeScale` で戻す）。LitMotion と UniTask の待機はスケールされた時間で動くので、止めている間はフラッシュも揺れも止まり、戻ってから動き出す。`OnDestroy` で必ず1に戻す。

---

### 3-11. 演出値の整備：複数ファイルにまたがる値だけを1か所に（B4後）

View の調整値は名前付き const だったが、同じ意味の値が複数ファイルにあってコメントで「揃えた」だけになっており、実際にずれていた（DoT の紫、弱点の黄、帯の距離 2400 の重複、「大技 」と「大技：」）。Model の待機と View の尺も手で合わせていた。

- **尺**：`PresentationTimingData`（`PT_Battle`）に集めた。Model の待機（`TransitionDuration` / `NormalEntryDelay` / `BossEntryDelay`）は「尺＋余白」を計算するプロパティにしたので、尺を変えれば自動で追従する。ボス登場の順番が崩れる値を入れると `OnValidate` が警告する
- **色**：`BattlePaletteData`（`PL_Battle`）に集めた。DoT は数字側の明るい紫 `(0.75,0.45,0.9)`、弱点は `(1,0.85,0.2)` に揃えた（敵のフラッシュの色が変わる）
- **SO にしなかったもの**：調整しない共有値は `BattleUiConstants`。1つの View に閉じた振幅・倍率・色は const のまま残した（§6）

SO にしたので、Play 中に Inspector で尺と色を調整できる（変更はアセットに残る）。

## 4. 動作確認結果

Step 0・2 は Unity MCP で Play モードを動かして確認、Step 3 以降はユーザーが Play モードで確認した。

| 確認項目 | 結果 |
|----------|------|
| 術のパーティクルが敵の手前、HUD がさらに手前に描かれる | ✅ |
| ステージ名 → 敵の登場 → 1秒後に予告（登場と重ならない） | ✅ |
| 攻撃予告（攻撃名・ゲージ・明滅）→ 攻撃 | ✅ |
| 被弾・Stun（気絶表示・予告の中断） | ✅ |
| 撃破 → 前進 → 次の敵（倒した敵が予告を出さない） | ✅ |
| ボスの登場（拡大表示・ボス名） | ✅ |
| 被弾・ガード成功・暴発のフィードバック、術ごとのエフェクト、カットイン | ✅ |
| ダメージ数字・MultiHit の時間差・弱点/迅速・コンボ・状態アイコン・決着表示 | ✅ |
| StatusEffect の汎用表現、ボスの登場シーケンス・大技の特別予告・撃破演出 | ✅ |
| 演出値の整備（§3-11）後のコンパイル | ✅（MCP でエラー・警告0件） |
| 演出値の整備後の Play 確認（間隔が変わらない・DoT/弱点の色が揃う） | ⏳ 未確認 |

---

## 5. アセット変更一覧

| アセット | 変更 |
|----------|------|
| `Particle/T_SoftCircle.png` / `M_SpellParticle.mat` | 新規。パーティクル用の柔らかい円と `Sprite-Unlit-Default` のマテリアル |
| `Particle/Spell/PS_*.prefab`（術7種） | 新規。属性色で作り分け。雷連撃は5回のバースト、火炎弾は残り火、回復術は上昇 |
| `Particle/Spell/PS_BossDefeat.prefab` | 新規。ボス撃破の爆散（2段のバースト） |
| `Particle/T_Vignette.png` | 新規。大技予告のビネット |
| `ScriptableObject/Player/*.asset`（術7種） | `effectPrefab` を設定。回復術のみ `effectOnCaster = true` |

| `ScriptableObject/Presentation/PT_Battle.asset` / `PL_Battle.asset` | 新規（§3-11）。初期値は整備前の値と同じ。`InGame.unity` の8か所から参照 |

テクスチャと Prefab は Unity MCP の `execute_code` でスクリプト生成した（手作業の Inspector 設定ではない）。

---

## 6. 技術的負債・保留事項

| 負債 | 対処方針 |
|------|----------|
| アートは仮素材（敵は既定スプライト＋名前から決めた色、背景は1枚） | 本番素材は `EnemyData.sprite` / `StageData` の背景 / `SpellData.cutInSprite` を差し替えるだけで入る |
| ヒットストップが `Time.timeScale` を直接触る | B5 でポーズを `timeScale` で作るなら、ここと取り合いになる。時間を止める窓口を1つにまとめる |
| Main Camera を揺らすと右下のカメラ映像（Screen Space - Camera）も揺れる | 気になればカメラ映像の Canvas を Overlay にするか、揺らす対象を `BattleField` にする |
| ~~演出の尺が Model の定数と View の定数に分かれている~~ | **解消（B4後の整備）。** `PresentationTimingData` に集め、Model の待機は尺から計算するようにした |
| `BattleResultView` は仮の決着表示 | B5 のリザルト画面で置き換える |
| `TransitionDuration` に 0.5秒の空白（明転後、次の敵が出るまで） | 間として残した。詰めるなら 2.1秒 |
| 敵の攻撃エフェクト（`EnemyAttackData.effectPrefab`）は全て未設定 | 仕組みはある。未設定の攻撃はフラッシュと揺れだけ |
| StatusEffect に属性を持たせていない | 別属性の DoT/Stun が増えたら B8 で検討（§3-7） |
| 1つの View に閉じた調整値（振幅・倍率・その View だけの色、約100個）は const のまま | 本番素材で調整を始める時に、必要になった View から SO へ移す（§3-11 の続き） |
| LitMotion を素の `Create().Bind()` だけで書いている | 減衰する揺れが `EnemyView` と `CameraShakeView` で二重実装。「入る→保持→出る」が `WithOnComplete` の入れ子（StageTitle / SpellTelop / BossEncounter）。いずれ揺れを1つの関数（または `LMotion.Shake`）にまとめ、入れ子は `LSequence` で平らにし、ハンドルの多い View は `CompositeMotionHandle` でまとめる |
| 演出の流れを Unity Timeline で組んでいない | 今の規模では時間表（`PT_Battle`）で足りるので見送り。カットシーン的な演出が増えたら B8 あたりで検討 |
| 暴発の色が ScreenFlash（紫）と SpellTelop の文字（赤）で違う | 揃えると見た目が大きく変わるため §3-11 では対象外にした。演出を見直す時に決める |
| Unity が背面にあると Play モードが進まない（Run In Background がオフ） | MCP で Play 確認する時の注意点。設定は変えていない |

---

## 7. 仕様書更新（v1.5 → v1.6、反映済み）

| セクション | 更新内容 |
|------------|----------|
| 2-1 クラス一覧 | Presenter 層・View 層を実装に合わせて更新（`EnemyPresenter` と View 13種）。`GuardWindowManager` の受付時間を1秒に修正 |
| 3-2 SpellData | `effectOnCaster` を追加 |
| 8 MVP構成 | Battle / HandSign / Enemy の各 MVP を実装に合わせて更新 |
| 10-2 未決定の仕様 | StatusEffect の表現（効果の種類で表す）を確定として追加 |
| 11 B4内訳 | 進捗実績を追記 |

あわせて `docs/architecture/` の 00・02 を B4 時点に更新した（02 に §12 演出レイヤーを新設）。

---

## 8. B5への引継ぎ

- 決着の受け口は `SectionProgressManager.OnStageCleared` / `OnGameOver`。今は `EnemyPresenter` が `BattleResultView` に「討伐」「敗北」を出しているだけなので、リザルト画面への遷移はここを差し替える。ボス撃破時は撃破演出（`PresentationTimingData.BossDefeatDuration`）の後に出している
- ポーズを `Time.timeScale` で作る場合は、ボス撃破のヒットストップ（`BossEncounterView.PlayHitStop`）と整理が要る
- デバッグの「ボスへ飛ぶ」で、ボスの登場シーケンスを含めて確認できる
