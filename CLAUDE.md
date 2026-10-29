# CLAUDE.md

## プロジェクト

**MUDRA** — Webカメラで手印（ハンドサイン）を組んで術を放つ、ボス戦アクションゲーム。
Unity 6000.4.4f1 / URP（App は 2D Renderer、InGame は Universal Renderer）/ Windowsスタンドアロン（WebGL非対応）。開発は個人1名。
現在地: **β版 B7（サウンド）まで完了。次は B8（バランス調整）。**

---

## コードの場所と規模

**自前コードは `MUDRA/Assets/_MUDRA/Scripts/` の125ファイル・約13,400行が全て。** 小さい。
（うち `Editor/` の2本・約660行は敵・ステージSOの生成ツールと再生開始シーンの切替で、ランタイムからは参照しない）

- git管理下の `.cs` は501個あるが、**456個はサードパーティ**
  （`MUDRA/Assets/MediaPipeUnity/`, `MUDRA/Packages/`, `MUDRA/Assets/TextMesh Pro/`）
- `.meta` が959個ある。`find` / `ls` では `-not -name '*.meta'` で除外する
- `MUDRA/Library/` は2.3GB。読まない・触らない
- Unityプロジェクト本体は `MUDRA/` 配下。リポジトリルートは1つ上

---

## 調査のやり方

- **サブエージェント（Explore / general-purpose）は使わない。** この規模では自前コードを全部読んでも
  約40kトークンで済み、探索を挟むほうが高くつく。必要なファイルを直接読むこと
- 自前コードの検索は `MUDRA/Assets/_MUDRA/Scripts` にスコープする。** 無指定の `grep -r` や `**/*.cs` は
  9割がサードパーティに当たる。`.asset`、`.meta`、文書は対象を指定して検索する
- **`.unity` / `.asset` を Read しない。** `App.unity` / `InGame.unity` はそれぞれ約310KBのYAMLで、読んでも
  得られるのはコンポーネントの配線だけ。代わりに:
  - 値を見たいだけなら `grep -n '^  fieldName' <file>`
  - シーンの配線を知りたいなら `.cs.meta` の guid を集めて `m_Script:` を逆引きする
- **仕様書・devlog を全文 Read しない。** `grep -n '^#'` で見出しを出してから `sed -n '<範囲>p'`

---

## レイヤー構成

```
Input ──► Model ◄── Presenter ──► View        Data(ScriptableObject) は全層から参照
```

| 層 | 規約 |
|---|---|
| **Input** | 手印認識。`MediaPipeHandLandmarkProvider` 以外はPure C# |
| **Model** | **MonoBehaviour非継承・Unity API不使用。** ゲームロジックの本体 |
| **Presenter** | ロジックを持たない。購読の配線とViewへの受け渡しのみ |
| **View** | Modelを知らない。命令的メソッドを公開するだけ |

通知は常に **Model →(R3)→ Presenter →(メソッド呼び出し)→ View** の一方向。逆流させない。
生成と注入は App 側が `Presenter/AppInitializer.cs`、バトル側が `Presenter/BattleInitializer.cs` に集約（DIコンテナ・シングルトンは不使用）。
シーンは常駐の `App.unity`（カメラ・画面・遷移）と、バトル（InGame・Tutorial）のたびに加算ロードする `InGame.unity` の2つ。
InGame は 2.5D（3D 空間に絵の板を立てる書き割り）。ステージの見た目は環境 Prefab（`StageData.environmentPrefab`）で、カメラ・敵は `BattleRig` ごとマーカー間を移動する（B6.5）。
音は View の `SePlayer`（App と InGame に1つずつ）と `BgmPlayer`（App に常駐。バトルへは `BattleContext.Bgm`）が鳴らし、どの音かは `SoundData`（`SND_Sound`）と各 Data が持つ（B7）。

**詳細は `MUDRA/docs/architecture/00_overview.md` を読む。**

---

## ドキュメントの読み分け

| ファイル | 位置づけ |
|---|---|
| `MUDRA/docs/architecture/` 4本 | **現コードの断面図。調査はまずここから**（03_sound が BGM・効果音・音量。「この音はどこで鳴る？」は 03 §5） |
| `MUDRA/docs/specification_MUDRA.md` | 設計の正典（v1.9）。**未実装のものも含む。**§指定で部分読み |
| `MUDRA/docs/hand_sign_tuning.md` | 手印認識の閾値の調整手順。判定の違和感を直すときに読む |
| `MUDRA/docs/devlog/dev_log_*.md` | 当時のスナップショット。設計理由を追う時だけ該当1本 |
| `MUDRA/docs/devlog/dev_log_B6.5.md` | **InGame の 3D の仕組み・描画のルール・環境 Prefab の作り方。環境やカメラを触る前に読む** |
| `MUDRA/docs/stage0N_environment_asset_prompts.md` | ステージ環境の素材の生成プロンプト（4本） |
| `MUDRA/docs/sound_asset_list.md` | サウンド素材の一覧（ファイル名・探す語・入手記録）。素材を足す・探すときに読む |
| `README.md` / `docs/proposal_MUDRA.md` | 企画・世界観。実装作業では通常不要 |

---

## コード規約

- **Model は Pure C#。** MonoBehaviourを継承せず、Unity APIも使わない
- **R3:** イベント = `Subject<T>`、状態 = `ReactiveProperty<T>`。
  公開は `Observable<T>` / `ReadOnlyReactiveProperty<T>` に絞る。
  購読は `readonly CompositeDisposable _disposables` に集約し `OnDestroy` で破棄
- **非同期は UniTask のみ。** `UniTaskVoid` + `.Forget()`、キャンセルは `CancellationTokenSource`。
  コルーチンと `async void` は使わない
- **演出は LitMotion。** `MotionHandle` は struct なので `IsActive()` を確認してから `Cancel()`
- **環境 Prefab の床はマーカーの y − 1.5。** 敵の足元の影がこの高さ固定。立てる物は URP/Unlit の不透明＋Alpha Clipping の板にする（半透明は前後が入れ替わりやすいので、炎・煙など縁の柔らかい物に限る）
- **デバッグ機能は `#if UNITY_EDITOR || DEVELOPMENT_BUILD` で囲う**（ファイル単位でもメンバ単位でも可）
- private field は `_camelCase`。マジックナンバーはクラス冒頭の `private const` に名前付きで置く
- 型・publicメンバには**日本語のXMLドキュメントコメント**を付ける。設計理由も書く（既存コードに倣う）
- 名前空間は `MUDRA.HandTracking` / `MUDRA.Data` / `MUDRA.Debugging` のみ。
  Model・Presenter・View はグローバル名前空間（統一されていないが、現状に合わせる）

---

## 壊してはいけないもの

- **`HandSign` の整数値は `SpellData` アセットにシリアライズ済み。**
  既存値の変更・並べ替えは全アセットを壊す。**追加は必ず末尾に**
- `MUDRA/Assets/MediaPipeUnity/Samples/Scenes/Hand Landmark Detection/HandLandmarkerRunner.cs`
  の2行（`OnLandmarkDetected` の宣言と `Invoke`）は**手動パッチ**。ゲーム側への唯一の入口なので消さない
- 印を追加したら `View/SequenceGuideView.cs` の `GetSignDisplayName()` にも表示名を追加する
  （漏れると「？」と表示される）
- **AudioListener は App の `Audio` に1つだけ。** カメラ（App・InGame）に付けると遷移の瞬間に2つ重なる
- `MX_Main` の公開パラメータ名 `BgmVolume` / `SeVolume` は `View/AudioVolumeView.cs` の定数と一致させる（ずれると音量設定が効かない）
- 一部の `.cs` は改行コードが CRLF（`BattlePresenter` / `EnemyPresenter` / `HandSignPresenter` など）。**スクリプトで置換するときは改行コードを保つ**（LF で書き戻すと全行が差分になる）

---

## ビルド・テスト・コミット

- **自動テストは存在しない。** `com.unity.test-framework` は入っているが未使用。
  「テストを実行します」と言わないこと。`Debug/HandTrackingActiveTest.cs` はテストではなくログ出力
- **コンパイル確認は原則ユーザーが Unity エディタで行い、エラーを貼る。** 推測で直さない
  （Unity本体は `/mnt/c/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe`。
  batchmode での確認は未検証。試すならエディタを閉じた状態でユーザーの了承を得てから）
- **動作確認はカメラ不要のキーボードモードで行える。**
  `HandSignPresenter` の `_debugKeyboardInput` に `DebugKeyboardInput` を割り当てると有効になる。
  `1`〜`5`=詠唱印 / `U`=合 / `Space`=発動 / `Backspace`=解除 / `G`=ガード / `R`=リトライ、`F1`=デバッグメニュー
- 再生は App から（InGame を開いて再生しても `MUDRA/Play From App Scene` が ON なら App から始まる）。
  カメラ無しで通すには Calibration を `F2` で抜け、Tutorial を `Tab` でスキップする
- コミットメッセージは既存形式に合わせる: `feat:B<番号> <日本語の要約>`（例: `feat:B4 ...`）。**指示があるまでコミットしない**
