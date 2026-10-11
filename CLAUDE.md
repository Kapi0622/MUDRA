# CLAUDE.md

## プロジェクト

**MUDRA** — Webカメラで手印（ハンドサイン）を組んで術を放つ、ボス戦アクションゲーム。
Unity 6000.4.4f1 / URP 2D / Windowsスタンドアロン（WebGL非対応）。開発は個人1名。
現在地: **β版 B4.5（手印認識の改善）まで完了。次は B5（ゲームフロー実装）。**

---

## コードの場所と規模

**自前コードは `MUDRA/Assets/_MUDRA/Scripts/` の66ファイル・約7,100行が全て。** 小さい。
（うち `Editor/B3ContentGenerator.cs` 約590行は敵・ステージSOの生成ツールで、ランタイムからは参照しない）

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
- **`.unity` / `.asset` を Read しない。** `InGame.unity` は80KBのYAMLで、読むと約25kトークンを
  消費して得られるのはコンポーネント20個の配線だけ。代わりに:
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
生成と注入は `Presenter/BattleInitializer.cs` に集約（DIコンテナ・シングルトンは不使用）。

**詳細は `MUDRA/docs/architecture/00_overview.md` を読む。**

---

## ドキュメントの読み分け

| ファイル | 位置づけ |
|---|---|
| `MUDRA/docs/architecture/` 3本 | **現コードの断面図。調査はまずここから** |
| `MUDRA/docs/specification_MUDRA.md` | 設計の正典（v1.3）。**未実装のものも含む。**§指定で部分読み |
| `MUDRA/docs/hand_sign_tuning.md` | 手印認識の閾値の調整手順。判定の違和感を直すときに読む |
| `MUDRA/docs/devlog/dev_log_*.md` | 当時のスナップショット。設計理由を追う時だけ該当1本 |
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

---

## ビルド・テスト・コミット

- **自動テストは存在しない。** `com.unity.test-framework` は入っているが未使用。
  「テストを実行します」と言わないこと。`Debug/HandTrackingActiveTest.cs` はテストではなくログ出力
- **コンパイル確認は原則ユーザーが Unity エディタで行い、エラーを貼る。** 推測で直さない
  （Unity本体は `/mnt/c/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe`。
  batchmode での確認は未検証。試すならエディタを閉じた状態でユーザーの了承を得てから）
- **動作確認はカメラ不要のキーボードモードで行える。**
  `HandSignPresenter` の `_debugKeyboardInput` に `DebugKeyboardInput` を割り当てると有効になる。
  `1`〜`5`=詠唱印 / `U`=合 / `Space`=発動 / `Backspace`=解除 / `G`=ガード / `R`=リロード、`F1`=デバッグメニュー
- コミットメッセージは既存形式に合わせる: `feat:B<番号> <日本語の要約>`（例: `feat:B4 ...`）。**指示があるまでコミットしない**
