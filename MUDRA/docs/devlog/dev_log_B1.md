# 📋 開発ログ：β版 B1（開発基盤整備）

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** β版 B1
> **期間:** 2026/07/18
> **ステータス:** ✅ 完了

---

## 1. マイルストーン概要

| 項目 | 内容 |
|------|------|
| **完了条件** | キーボード操作のみでバトルの全機能がテスト可能。デバッグメニューからHP操作・StatusEffect付与ができる |
| **開発方針** | α版クロージングと並行し、β版全工程の効率を底上げするデバッグ基盤を構築する |
| **位置づけ** | α版A5完了（07/17）から8日前倒しで生まれた余裕を活用し、β版の再計画と基盤整備を1日で完了 |

### B1に至る経緯

α版A5が07/17に完了（予定07/25から8日前倒し）。この余裕を活かしてβ版のスケジュールを全面再構成した。当初のB1〜B4（11日間）をB1〜B9（33日間、〜08/19）に拡張し、以下のタスクを新たに追加した。

- バトル演出・UI強化（敵側/プレイヤー側の視覚フィードバック全般）
- ゲームフロー実装（Title/StageSelect/Result画面 + シーン遷移）
- 両手印対応（Union = 8）
- サウンド（SE・BGM）
- ステージ構成変更（パズドラ式セクション制：道中雑魚×N + ボス×1）
- 開発者用デバッグ機能

仕様書をv1.2→v1.3に更新し、StageDataのセクション配列化、バトル進行フローのセクションループ追加、10-2未決定事項の拡充、11章マイルストーンの全面書き換えを実施した。

---

## 2. 実装ファイル一覧

### ビジュアル方針確定

コード変更なし。仕様書10-2に「2Dで確定」を記録。HD-2Dはβ後の展望としてSprite差し替えパスを維持する方針。

### リネーム

| ファイル | 変更種別 | 内容 |
|----------|----------|------|
| `SpellSequenceRunner.cs` → `HandSignPresenter.cs` | リネーム | A1からの技術的負債解消。Riderの「Rename Symbol」で一括実施 |

### キーボード手印シミュレーション

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `DebugKeyboardInput.cs` | 新規作成 | キー押下でHandSignを即発火。`Subject<HandSign>`で公開。Rキーでシーンリロード。`#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`で囲む |
| `HandSignPresenter.cs` | 改修 | `_debugKeyboardInput`がアタッチされている場合、`HandTrackingService.Tick()`をバイパスし、`DebugKeyboardInput.OnHandSignInput`を`HandleSignConfirmed`に接続。Update内のTick呼び出しもスキップ |

### デバッグメニューUI

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `DebugMenuView.cs` | 新規作成 | OnGUI（IMGUI）ベースのRuntime Overlayメニュー。F1でトグル。HP操作・StatusEffect付与・無敵モード・FPS表示・シーンリロードを搭載 |
| `BattleModel.cs` | 改修 | `IsInvincible`フラグ追加。`DebugModifyPlayerHp/DebugModifyBossHp`追加。`ApplyEnemyDamage`/`ApplyMisfireDamage`に無敵チェック追加。全て`#if`で囲む |
| `BattleInitializer.cs` | 改修 | `FindFirstObjectByType<DebugMenuView>()`で検索し、`Inject(battleModel, statusEffectManager, enemyStateManager)`で依存注入。`#if`で囲む |

---

## 3. 設計判断の記録

### 3-1. ビジュアル方針: 2Dで確定

HD-2D（3D背景 + 2Dキャラ）はアセット制作コストが高く、フリー素材での代用も困難。2D（背景・キャラ共にAsepriteベースのスプライト）を採用し、演出・アニメーション（パーティクル、LitMotionトゥイーン、画面エフェクト）で単調さを回避する方針。HD-2Dはβ後の展望として差し替えパスを維持する。

### 3-2. キーボードシミュレーション: HandSignレベルでのモック（案B）

差し込み箇所として2案を比較し、案Bを採用した。

- **案A（ランドマークレベル）** — `IHandLandmarkProvider`のモックで21点×3軸の座標を返す。認識パイプライン全体のテストが可能だが、座標セットの用意が重い
- **案B（HandSignレベル・採用）** — `HandTrackingService`の出力を直接差し替え。キー押下で即HandSign確定

**判断根拠:** B1の目的は「バトルの全機能がカメラなしで動くか」のテストであり、認識精度のテストではない。認識精度はカメラで直接検証すべきもの。

### 3-3. バトルリセット: シーンリロード方式（案A）

リセット実装として2案を比較し、案Aを採用した。

- **案A（シーンリロード・採用）** — `SceneManager.LoadScene(現在のシーン)`で全状態を初期化。実装1行
- **案B（インプレースリセット）** — シーンそのままでInitializeを再呼び出し。ポストコンストラクション注入等「一度きりの初期化」前提の設計が各所にあり、二重購読・Dispose漏れのリスクが高い

**判断根拠:** デバッグ機能のためにコア設計へ手を入れるのは本末転倒。シーン規模も小さく、リロード時間は体感1秒未満。

### 3-4. デバッグメニュー: OnGUI（IMGUI）方式

- **uGUI** — Canvas + Panel + Buttonで構築。見た目のカスタマイズ性は高いが、セットアップ工数がかかる
- **OnGUI（IMGUI・採用）** — コードだけで完結。GameObjectやPrefab不要。デバッグ専用であり、ポートフォリオとして見せる部分ではないためスピード優先

### 3-5. デバッグメニューへの依存注入: FindFirstObjectByType方式

デバッグ専用クラスのためにBattleInitializerの本番インターフェースを広げることを避け、`FindFirstObjectByType<DebugMenuView>()`で検索して`Inject()`する方式を採用。`#if UNITY_EDITOR || DEVELOPMENT_BUILD`で囲むことで本番コードへの侵入を最小限に抑えている。

### 3-6. Input方式: デバッグはGetKeyDown、製品入力はB5で判断

デバッグ専用のキーボード入力にはInput Systemのセットアップコストが見合わない。`GetKeyDown`を`#if`で囲めばリリースビルドから完全に消える。B5（ゲームフロー実装）でポーズ機能等の製品入力を実装する際にInput Systemの正式導入を検討する。

### 3-7. 無敵モード時の暴発挙動

無敵モード中でもコンボリセットは維持する設計にした。ダメージだけ無効化し、暴発ペナルティのコンボリセットを残すことで、無敵中にRelease連打してもノーリスクにならず、バトルの挙動確認がしやすい状態を保つ。

### 3-8. ステージ構成変更: パズドラ式セクション制

β版計画策定時に、1ステージ = 1ボス戦から「道中セクション（雑魚）×N + ボスセクション×1」の構成に変更した。

- 1ステージのセクション数はステージごとに可変（2〜5の範囲）
- 雑魚戦もボス戦と同じメカニクス（手印→術発動）で戦う
- プレイヤーHPはセクション間で引き継ぐ（リソース管理要素）
- セクション遷移時に「前に進む」背景スクロール演出→停止→敵出現
- 背景は道中用 / ボス戦用の2枚構成

仕様書のStageDataをセクション配列構造（`StageSection[]`）に再設計し、7-3バトル進行フローにセクションループを追加した。

---

## 4. 動作確認結果

| # | 確認項目 | 結果 |
|---|-----------|------|
| ① | 1〜5キーで詠唱印5種が確定し、デバッグテキストに表示される | ✅ |
| ② | Spaceで発動印、Backspaceで解除印が正しく動作する | ✅ |
| ③ | Gキーでガードが発動する | ✅ |
| ④ | キーボードのみで術発動→ボスHP減少→勝敗判定まで一連のバトルが動作する | ✅ |
| ⑤ | Rキーでシーンリロードが実行され、全状態がリセットされる | ✅ |
| ⑥ | F1キーでデバッグメニューの表示/非表示がトグルされる | ✅ |
| ⑦ | HP操作ボタン（-10/-50/+50/全回復/瀕死）でPlayer/BossのHP増減が反映される | ✅ |
| ⑧ | Stun付与ボタンでボスがStunned状態に遷移し、時間経過で復帰する | ✅ |
| ⑨ | DoT付与ボタンで毎秒tickダメージがボスに入り続ける | ✅ |
| ⑩ | 全Effect解除ボタンで全StatusEffectがクリアされる | ✅ |
| ⑪ | 無敵モードトグルONでプレイヤーへのダメージが無効化される | ✅ |
| ⑫ | 無敵モード中の暴発でダメージは0だがコンボリセットは発生する | ✅ |
| ⑬ | FPS表示が画面右上に常時表示され、値に応じて色が変化する | ✅ |
| ⑭ | `_debugKeyboardInput`をNoneに戻すと通常のカメラ入力モードに復帰する | ✅ |

---

## 5. フォルダ構成（B1時点）

```
Scripts/
├── Data/
├── Debug/                          ← B1新設
│   ├── DebugKeyboardInput.cs       ← B1新規
│   └── DebugMenuView.cs            ← B1新規
├── HandTracking/
├── Input/
├── Model/
│   ├── State/
│   ├── Strategy/
│   ├── StatusEffect/
│   ├── BattleModel.cs              ← B1改修（デバッグ用メソッド・無敵フラグ追加）
│   ├── EnemyStateManager.cs
│   ├── GuardWindowManager.cs
│   └── PlayerStateManager.cs
├── Presenter/
│   ├── BattleInitializer.cs        ← B1改修（DebugMenuView注入追加）
│   ├── BattlePresenter.cs
│   └── HandSignPresenter.cs        ← B1改修（キーボード入力分岐追加）+ リネーム
└── View/
```

---

## 6. 技術的負債・保留事項

| 負債 | 対処方針 |
|------|----------|
| `ResolveCalculator`が毎回newしている | A2から継続。ステートレスなので実害なし |
| MultiHitのView側時間差演出 | A4ストレッチ未着手。B4で対応 |
| Stun完封対策（免疫期間 or 逓減） | B8（バランス調整）で対応 |
| Guard印の手形は仮決定 | B2（両手印対応）時に再検討 |
| `GuardWindowManager.WindowDuration`のSO化 | B8で対応 |
| `DotEffect.TickInterval`と`DamageOverTimeCalculator.TickInterval`の二重定義 | 実害なし |
| ステージジャンプ機能 | B3でStageData再設計後に`DebugMenuView`に接続 |
| Input System導入検討 | B5（ゲームフロー）でポーズ機能実装時に判断 |

---

## 7. 仕様書更新対象リスト

B1で実施済みの更新（v1.2 → v1.3）:

| セクション | 更新内容 |
|------------|----------|
| ヘッダー | v1.3、最終更新日2026/07/18 |
| 2-1 Data Layerクラス一覧 | `StageSection`追加、`StageData`説明更新 |
| 3-2 StageData定義 | セクション配列構造（`StageSection[]`）に変更。`roadBackgroundSprite`/`bossBackgroundSprite`の2枚構成に |
| 7-3 バトル進行フロー | セクションループ + セクション遷移演出の説明追加 |
| 8-2 HandSign MVP | リネーム完了の注記更新 |
| 10-2 未決定事項 | ビジュアル方針を「2Dで確定」に更新。雑魚敵種類数・コンボ/StatusEffect引き継ぎ・回復手段の4項目追加 |
| 11 マイルストーン | β版をB1〜B9（33日間、〜08/19）に全面再構成 |

---

## 8. B2への引継ぎ

### キックオフPrompt

```
前回の作業: β版B1（開発基盤整備）が完了。
参照ドキュメント: docs/specification_MUDRA.md（v1.3）、docs/dev_log_B1.md

B2スコープ: 両手印対応（3日間、07/20〜07/22）

完了条件:
両手を合わせるとUnion印が認識され、Union含むシーケンスで新術が発動する。
キーボードでも再現可能。

主要タスク:
1. HandTrackingServiceの2手対応調査（MediaPipeUnityPluginが2手分のランドマークを返す場合のパイプライン設計）
2. 両手検出ロジック（DetectedHandCount == 2 + 手首座標距離閾値判定）
3. HandSign.Union = 8 追加（enum拡張 + 識別パターン登録）
4. Union使用新術のSpellDataアセット（ElementType.Light対応）
5. Guard印の再検討（両手検出との干渉確認）
6. SequenceGuideへのUnion表示追加
7. DebugKeyboardInputへのUnionキー追加

リスク:
MediaPipeの2手同時追跡精度が不十分な場合、Union判定条件の簡略化 or 延期のカット判断を3日目に入れる。

現在のデバッグ基盤:
- キーボードシミュレーション（1-5: 詠唱印、Space: Release、Backspace: Cancel、G: Guard、R: リロード）
- デバッグメニュー（F1トグル、HP操作、StatusEffect付与、無敵モード、FPS表示）

技術的注意点:
- MediaPipeUnityPluginの2手同時検出はカピさんにとって未経験領域。API調査から入る
- HandSign enumに明示的整数値が必須（挿入時のシリアライズシフト防止）
- IHandLandmarkProviderのPullパターン（Providerがキャッシュ、Serviceが毎フレームpull）は維持する
```

### B2で新たに必要になるもの

- MediaPipeUnityPluginの複数手検出API（`multi_hand_landmarks`相当）の調査
- `HandSign.Union = 8` の追加（enum値は明示的整数で指定）
- Union含む新術のSpellData SO定義
- `DebugKeyboardInput`にUnionキー（例: `U`キー）追加
- Guard印の誤認識テスト（両手検出環境下）
