# 📋 開発ログ：β版 B6（チュートリアル・キャリブレーション）

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** β版 B6
> **期間:** 2026/10/22〜10/24
> **ステータス:** ✅ 実装完了（コミット済み。スキップボタンの表示変更のみ未コミット）

---

## 1. 概要

| 項目 | 内容 |
|------|------|
| **出発点** | B5 で Title → StageSelect → InGame → Result が一周するようになった。カメラが使えるか・手が映るかを確かめる場所が無く、初見のプレイヤーは操作を知らないままボス戦に入る |
| **完了条件** | 初見プレイヤーがキャリブレーション→チュートリアルを経て、StageSelect に辿り着ける（仕様書 §11） |
| **仕様書の内訳** | Calibration 画面（カメラプレビュー・認識テスト・カメラ未検出時エラー）／Tutorial（詠唱→発動→Cancel→Guard→属性）／「手が認識されません」ガイド |
| **内訳に足したもの** | カメラが映像を返さない時のエラー（未検出とは別）と再試行、Tutorial のスキップ（ボタン・`Tab`）、F2 でカメラ無しでも Calibration を抜けるデバッグキー |
| **途中で変えた方針** | Tutorial は当初「初回のみ（セーブに完了フラグ）」としたが、**StageSelect の前に毎回通す**に変更した（§4-1） |
| **進め方** | 計画を先に承認してもらい、Step 1〜5 を1つずつ「変更内容の共有 → 承認 → 実装」で進めた。C# は手で書き、シーン・プレハブ・アセットは Unity MCP で組んだ。Play モードでの確認はユーザーが行った |

### 規模
自前コードは 105 ファイル・約 10,400 行から **117 ファイル・約 11,800 行**になった。

---

## 2. 全体像

### 2-1. フロー

```
Title ─はじめる→ Calibration ─次へ→ Tutorial ─完了／スキップ／木人撃破→ StageSelect
                     │                  └─敗北・ポーズの「もう一度」→ Tutorial（やり直し）
                     └─戻る→ Title
```

- `GamePhase` の末尾に `Calibration`・`Tutorial` を追加した（既存の整数値は変えていない）
- Tutorial は InGame シーンを使う。GameFlowPresenter から見ると「バトルを読み込むフェーズ」が InGame と Tutorial の2つになったので、
  判定を `GameStateManager.UsesBattleScene(phase)` にまとめた
- Tutorial は Result 画面・戦績に出さない。`BattleInitializer` で `EndBattle` を呼ばず、勝てば `CompleteTutorial`、負ければ `Retry` にした
- `AppInitializer._tutorialStage` が未設定なら Tutorial を飛ばして StageSelect へ進む（データの設定漏れで先へ進めなくなるのを避ける）

### 2-2. カメラと手の状態は App で1か所に持つ

```
MediaPipeHandLandmarkProvider（App 常駐）
        │ ResultVersion / 手が映っているか
        ▼
HandStatusPresenter（App・Update で実時間 Tick）
        ├─► CameraStatusModel   Starting / Running / NoDevice / NotResponding
        └─► HandPresenceModel   IsHandVisible / IsHandLost（5秒）
                 │
                 ├─► CalibrationPresenter … 状態の一文・エラーパネル・ガイド・「次へ」の可否
                 └─► PausePresenter（BattleContext 経由）… 手を見失ったら自動ポーズ＋ガイド
```

B5 では PauseModel が「手が5秒映らない」を自分で数えていた。B6 でこの判定を HandPresenceModel に移し、
Calibration とバトルの自動ポーズが同じ秒数・同じ判定を見るようにした（§4-2）。

---

## 3. 機能ごとの実装

### 3-1. フローの骨格（Step 1）
- `GameStateManager`: `StartGame` の行き先を Calibration に。`CompleteCalibration()` / `CompleteTutorial()` / `SkipTutorial()` を追加。
  `Retry` は Tutorial → Tutorial も許し、`ToStageSelect`・`ToTitle` は Tutorial からも呼べる
- `BattleContext` に `IsTutorial` と `HandPresence` を追加
- `GameFlowPresenter.CreateBattleContext` は、Tutorial の時に `TutorialStage` と `IsTutorial = true` を渡す

### 3-2. カメラと手の状態（Step 2）
| クラス | 層 | 役割 |
|---|---|---|
| `CameraStatus` | Model（enum） | `Starting` / `Running` / `NoDevice` / `NotResponding` |
| `CameraStatusModel` | Model | デバイス数と `ResultVersion` の進み方から状態を決める。起動から15秒届かない、または Running の後に3秒止まったら `NotResponding` |
| `HandPresenceModel` | Model | `IsHandVisible`、`IsHandLost`（5秒）。`IsHandLost` は手が映るか `RestartLostTimer()` まで立ったまま |
| `HandStatusPresenter` | Presenter（App） | 上の2つを `Time.unscaledDeltaTime` で回す（ポーズ中も数える）。`RecheckCamera()` でデバイスを数え直す |

- デバイス数（`WebCamTexture.devices.Length`）は Presenter が読んで渡す。Model に Unity API を入れないため
- `PauseModel` から手の判定を外し、代わりに「数え直してほしい時」を `OnHandWatchRestarted` で通知する（バトル開始時・再開直後）。
  再開した直後に手がまだ映っていなくても、すぐに再ポーズしない B5 の挙動はそのまま

### 3-3. Calibration 画面（Step 3）
- `CalibrationView`（ScreenView 継承）: 状態の一文、認識テストの印一覧（開/握/指/刃/掌/合/双/発/散/盾）、「次へ」「戻る」、カメラのエラーパネル（「再試行」「タイトルへ」）
- `CalibrationModel`: 「次へ」を押せるのは **カメラが Running で、画面を開いてから手が一度でも映った** とき。認識テストは任意（苦手な印で先へ進めなくなるのを避ける）
- `CalibrationPresenter`: 認識テスト用に **バトルとは別の `HandTrackingService`** を持ち、画面を出している間だけ実時間で Tick する（§4-5）。
  確定した印はマスを点灯させ、判定中の印は `HandSignStatusView` を App 側にも1つ置いて出す
- 再試行は `HandLandmarkerRunner.Play()`（動いていれば止めてから起動し直す）→ `RecheckCamera()`
- `CameraPreviewView.SetEmphasized(bool)`: Calibration の間だけ、右下のカメラ映像を左寄りに大きく出す（§4-4）
- キー: `Space`/`Enter` = 次へ（押せる時だけ）、`Esc`/`Backspace` = 戻る、`F2` = カメラ無しでも次へ（デバッグビルドのみ）

### 3-4. 「手が認識されません」ガイド（Step 4）
- `HandGuideView` ＋ `Prefabs/HandGuide.prefab`。見出しと確認項目（明るい場所で／カメラから30cm〜1mほど離す／手のひらをカメラに向ける／手首まで映像に入れる）
- 確認項目はどの画面でも同じにするため、文言は View の定数に持つ。ボタンは無く、レイキャストを通す
- 出す場所
  - **Calibration**: カメラが Running で `IsHandLost` の間。認識テストの上に重ねる。画面を開いた時に数え直すので、開いた途端には出ない
  - **ポーズメニュー**: 手を見失ってポーズした時だけ、右側の手印早見表の上に重ねる。理由の一文「手が映っていません」は残す
- カメラ自体が使えない時（NoDevice / NotResponding）は、ガイドではなく Calibration のエラーパネルを出す（原因が違うため）

### 3-5. Tutorial（Step 5）
**データ**（`B3ContentGenerator` の定義表に追加して生成）
| アセット | 内容 |
|---|---|
| `EA_DummyStrike` | 木人打ち。ダメージ3、予告2.5秒 |
| `ED_TrainingDummy` | 木人。HP 3000、弱点は火、行動は木人打ち（間隔4秒）だけ |
| `SD_Tutorial` | 修練場。stageNumber 0、セクションは木人1つ。`_allStages` には入れない |

**進行**（`TutorialStep` / `TutorialModel`）
| 段階 | 達成条件（購読するもの） |
|---|---|
| 詠唱 | `SpellSequenceModel.OnSignAdded` |
| 発動 | `OnSpellCast` の成功。暴発したら補足を出す（`OnMisfired`） |
| 解除 | `OnSequenceReset`（Cancel） |
| 防御 | `BattleModel.OnPlayerDamaged` の `WasGuarded` |
| 属性 | `BattleModel.OnSpellHit` の `IsWeakness` |

- 達成したら `tutorialStepClearHold`（1.5秒）だけ「よし！」を見せてから次の段階へ。全段階の後は「修練完了！」を同じ尺だけ見せて StageSelect へ
- `IsEnemyActive` は防御の段階に入るまで false。入ってから `tutorialEnemyStartDelay`（2秒）後に木人が1手目の予告を始める
- 決着が付いたら（木人を倒した・負けた）進行を止める。行き先は BattleInitializer が決める

**画面**（`TutorialGuideView` / `TutorialPresenter`）
- BattleCanvas の左上に、進み具合（修練 n / 5）・段階の見出し・指示文・補足・「よし！」・「スキップ（Tab）」
- 指示文の術名と印の並びは SpellData から組み立てる（`ScreenPresenter.BuildDefeatHint` と同じやり方）。
  発動の例には並びが一番短い術、属性の段階には木人の弱点属性の術を出す
- スキップはボタン・`Tab`・ポーズメニューの「ステージ選択へ」。ポーズ中は `Tab` に反応しない

### 3-6. 方針変更：Tutorial を毎回通す
Step 5 の後に変更した（§4-1）。
- `CompleteCalibration(bool needsTutorial)` → 引数なしに
- セーブの `isTutorialCompleted`、`ProgressModel.IsTutorialCompleted` / `MarkTutorialCompleted`、AppInitializer の記録の購読を削除。
  古いセーブにこの項目が残っていても、JsonUtility は無視するので読み込みは壊れない
- 毎回通る分、すぐ抜けられるように `Tab` でのスキップを足した

### 3-7. B6 の外で入った作業
- `HpBarView` に `OnDestroy` を足し、HP バーのモーションを止めるようにした（§6）。B6 以前からあった不具合で、通常のバトルでも決着直後のリトライで起こり得た
- フォントの差し替え（筆ごしらえ・ShipporiMincho・衡山毛筆の追加、コミット `08bb3eb` まで）。B6 の画面はこのフォントで組んだ

---

## 4. 設計判断の記録

### 4-1. Calibration も Tutorial も毎回通す
当初は「Calibration は毎回、Tutorial は初回のみ」とした。ユーザーの判断で Tutorial も毎回に変えた。
- スキップがボタン・`Tab`・ポーズメニューの3通りあり、慣れたプレイヤーの手間は1操作で済む
- 完了フラグを持たなくてよくなり、セーブの項目と「いつ完了扱いにするか」の決めごと（スキップは完了か、ポーズから抜けたら完了か）が丸ごと消えた
- Calibration は手をかざせばすぐ「次へ」を押せるので、毎回通っても数秒で抜けられる

### 4-2. 「手が映らない」の判定は HandPresenceModel の1か所
Calibration のガイドとバトルの自動ポーズで秒数がずれると、「さっきは出なかったのに今度は止まった」になる。
判定を App 側の1つの Model に持たせ、PauseModel は「数え直すタイミング」を知らせるだけにした。
App に置いたのは、カメラ（Provider）が App に常駐していて、画面をまたいで同じ値を見られるため。

### 4-3. カメラが動いているかは ResultVersion の進み方で見る
計画の時点では「手が映っていない時にも推論結果が届くか」が分からなかった。
MediaPipe の LIVE_STREAM では手が映っていなくても結果のコールバックが毎フレーム呼ばれる（`HandLandmarker.cs:263`）ことをソースで確かめ、
`ResultVersion` が進んでいれば Running、止まっていれば NotResponding とした。
WebCamTexture の状態を見るより、「推論まで含めて動いているか」を直接見られる。

### 4-4. カメラ映像の強調は、並び順を一時的に上げて出す
カメラ映像の Canvas（order 0）は画面（ScreenCanvas, 10）より奥にあり、そのままでは Calibration の背景に隠れる。
強調表示の間だけ order を 15 にし（画面より手前、設定 30・遷移 100 より奥）、位置と大きさを LitMotion で動かす。
元の配置は最初に強調した時にシーンの値を覚えるので、シーン側で右下の位置を変えても追従する。
強調の間は、設定で映像を非表示にしていても表示する（手が映っているかを確かめる画面なので）。

### 4-5. 認識テストはバトルとは別の HandTrackingService で回す
バトル側の `HandTrackingService` は InGame の読み込み中しか存在しない。Provider は結果を引いてくる（pull）形なので、
2つ目を作っても互いに干渉しない。Calibration を出している間だけ実時間で Tick する。
B5 §9 の「印でのメニュー操作」で認識を App に移すかは、引き続き未決。

### 4-6. Tutorial で敵を止めるのは EnemyStateManager の「保留」
`StopLoop()` で止めるだけでは足りなかった。セクション開始（`SetEnemy` → `StartLoop`）と Stun の解除（`EndStun`）が、それぞれループを自分で再開させるため。
`SetHeld(bool, resumeDelay)` を足し、保留中は `StartLoop`・`EndStun` でもループを回さないようにした。
保留を解く時、Stun 中なら EndStun に任せる。通常のバトルでは呼ばないので挙動は変わらない。

### 4-7. TutorialModel はバトルの外から見ているだけ
チュートリアル専用の判定や分岐をバトルの Model に入れず、既存のイベントを購読して「教えたい操作が起きたか」を見るだけにした。
本番と同じ操作がそのまま練習になり、バトル側の変更（術や判定の調整）にも追従する。
待機（「よし！」の尺）を Model が持つのは、SectionProgressManager・BattleEndModel と同じ方針（Model は演出の完了を待てない）。

---

## 5. 動作確認

| 項目 | 結果 |
|---|---|
| コンパイル（MCP でコンソールを確認） | ✅ エラー・警告なし（各 Step と方針変更の後） |
| `B3ContentGenerator` の再実行 | ✅ 新規3件。既存アセットの差分なし。検証④⑤⑥ OK |
| Tutorial を最後まで進める | ✅ ユーザー確認。終了後に出た MissingReferenceException は §6 で修正 |
| 上の修正後、Tutorial の終了・リトライでエラーが出ない | |
| 毎回 Calibration → Tutorial → StageSelect と進む（2回続けて起動） | |
| Calibration: カメラ映像が大きく手前に出て、抜けると右下に戻る | |
| Calibration: 手をかざすと「次へ」が押せる。確定した印のマスが光る。F2 で抜けられる | |
| Calibration: 手を5秒隠すとガイドが出て、かざすと消える | |
| カメラ未検出・映像が届かない時のエラーと再試行（カメラのアクセスを切る／USB を抜く） | |
| バトル中に手を5秒隠すと、ポーズメニューにガイドが重なる。Esc のポーズでは出ない | |
| Tutorial: 防御の段階まで木人が攻撃しない。雷連撃で気絶させても動き出さない | |
| Tutorial: スキップ（ボタン・`Tab`・ポーズの「ステージ選択へ」）。ポーズ中の `Tab` は無視 | |
| Tutorial の結果が Result・戦績に出ない | |
| 指示パネルがボスの HP バーなど他の表示と重ならない | |

---

## 6. つまずいた点

| 内容 | 対処 |
|---|---|
| Tutorial の終了後、`HpBarView` で MissingReferenceException | 遅延追従のバーは待機込みで約1.1秒動く。火炎弾の継続ダメージが入り続けたまま InGame がアンロードされ、破棄済みの Image に書き込んでいた。`OnDestroy` でモーションを止めた。LitMotion を使う他の View は止めているか `AddTo(gameObject)` 済みで、漏れはここだけだった |
| エディタが前面にないと Play モードが進まない（フレーム数が増えず、スクリーンショットが黒い） | MCP からの Play モード確認はあきらめ、ユーザーに確認してもらった |
| 再コンパイル後に MCP の接続が切れたまま戻らない（インスタンス数 0） | ユーザーにつなぎ直してもらった |
| MCP のコード実行（codedom）で `Object` が曖昧、`SceneManager` を `UnityEditor.SceneManagement` で探していた | `UnityEngine.Object` / `UnityEngine.SceneManagement.SceneManager` と書く。B5 と同じく C# 6 相当 |
| `CloseScene` の後に、閉じたシーンのオブジェクトを読んで例外 | 保存は済んでいた。値は閉じる前に読む |
| App.unity は作業前から未保存の変更があった | 中身が分からないまま保存せず、ユーザーの了承を得てから保存した |

---

## 7. 技術的負債・保留事項

| 内容 | 対処方針 |
|---|---|
| 木人の絵が無く、仮スプライトで出ている | 絵を用意して `ED_TrainingDummy.sprite` に設定する（生成ツールは絵のフィールドに書き込まない） |
| 木人がボス扱いになり、登場・撃破でボスの演出が出る | 生成ツールが「最後のセクション＝ボス」と決めているため。練習相手には大げさなら、`StageDef` にボス指定を持たせる |
| `CameraStatusModel` の秒数（起動15秒・停止3秒）は仮 | 実機の遅いカメラで確かめて調整 |
| ポーズ中に手が映り直しても、ポーズメニューのガイドは出たまま | 理由の一文と同じ扱い。気になるなら `IsHandVisible` で消す |
| Tutorial の指示文は `TutorialPresenter` の定数 | 文言の調整が増えたらデータ（SO）に出す |
| `PT_Battle.asset` に新しい2項目（`tutorialStepClearHold` / `tutorialEnemyStartDelay`）がまだ書き出されていない | 初期値で動く。Inspector で一度触るか、値を変える時に保存される |
| 印でのメニュー操作（B5 からの持ち越し） | Calibration・Tutorial のスキップもキーとマウスのみ。B5 §9 の方針決めと合わせる |

---

## 8. 仕様書・ドキュメントの更新（反映済み）

| 対象 | 内容 |
|---|---|
| 仕様書 §4-1 | 状態遷移図を書き直し、遷移とメソッドの対応表を足した。Calibration・Tutorial（毎回通る・スキップ・敗北でやり直し）と、B5 のリトライ・ステージ選択へも含む |
| 仕様書 §11 B6 | 進捗実績。全体スケジュールの引き直しと B5 の進捗実績は未反映（B5 からの持ち越し） |
| `CLAUDE.md` | 現在地（B6 完了・次は B7）、規模、2つの Composition Root と2シーン構成、`R` = リトライ、`F2` / `Tab` |
| `docs/architecture/00` | B5・B6 をまとめて反映：フォルダ構成、起動シーケンス（App / 遷移 / バトル）、毎フレーム駆動、App.unity と InGame.unity の構成、チュートリアル用アセット、§8 ギャップ表、開発再開ガイド |
| `docs/architecture/02` | `EnemyStateManager.SetHeld`、BattleInitializer の Tutorial 分岐、§13 ポーズとチュートリアル（自動ポーズの判定が HandPresenceModel に移ったことを含む） |

---

## 9. 次の作業

### B7（サウンド）に向けて
- 画面遷移は GameStateManager の `OnTransition` に集まっているので、BGM の切り替え・遷移の SE はここを購読すれば足りる
- Tutorial の「よし！」・Calibration の印の点灯は、UI 操作音・印確定音の置き場所の候補
- ポーズは `timeScale = 0` で止めているので、SE・BGM をポーズ中に止めるか（`AudioListener.pause` など）を AudioManager の設計で決める
