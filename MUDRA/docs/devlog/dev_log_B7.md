# 📋 開発ログ：β版 B7（サウンド）

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** β版 B7
> **期間:** 2026/10/23〜10/30（10/23〜25 は発音ポイントの洗い出しと素材リスト、実装は 10/29〜30）
> **ブランチ:** `feature/sound`（`a64fc1f` に Step 1 と BGM の取り込み。以降は未コミット）
> **ステータス:** ✅ BGM・効果音・音量設定まで完了。一部の素材は保留（§6）

**今の仕組みは [architecture/03_sound.md](../architecture/03_sound.md) にまとめた。** 本書は「どう決めて、どう進めたか」の記録で、仕組みの説明は繰り返さない。

---

## 1. 概要

| 項目 | 内容 |
|---|---|
| **出発点** | 音を鳴らすコードは0行。受け皿の欄（`SpellData.castSE` / `EnemyAttackData.attackSE` / `StageData.bgm`）だけがあり、すべて空。AudioListener が App と InGame のカメラに1つずつあり、遷移の瞬間に重なって警告が出ていた（B6.5 の宿題） |
| **ゴール** | 戦闘・画面遷移・UI の音と、ステージ単位の BGM が鳴る（仕様書 §11 B7） |
| **結果** | 効果音を鳴らす `SePlayer`（App と InGame に1つずつ）、BGM の `BgmPlayer`（App に常駐）、固定の音をまとめた `SoundData`、設定画面の音量スライダー（AudioMixer）。BGM 12曲・効果音 41素材・ARC 15個を当てはめた |
| **変えなかったもの** | Model の差分は音量の値（`ProgressModel` / `SaveData`）だけ。入力層・バトルの計算・演出の時間表には触れていない |
| **方針** | 「可能な限り難しくない実装」（ユーザー指定）。AudioMixer・3D 音響・音量設定は当初入れない予定で、音量設定だけ後から追加した（§2 #9） |
| **進め方** | 洗い出しの調査（変更なし）→ 素材リスト作成 → ユーザーが素材を集める間に仕組みの計画 → ステップごとに「提案 → 承認 → 実装 → ユーザーが再生して確認」。C# は手で書き、シーン・アセットの割り当ては Unity MCP（`execute_code`）。素材の収集・ARC の作成・Mixer の作成はユーザー |

### 規模
- 自前コード: 121 ファイル・約 12,490 行 → **125 ファイル・約 13,440 行**
- 新規クラス: `SoundData` / `SePlayer` / `BgmPlayer` / `AudioVolumeView`
- 変更クラス: Presenter 9本（`HandSign` / `Battle` / `Enemy` / `Pause` / `GameFlow` / `Screen` / `Settings` / `Calibration` / `BattleInitializer`）、View 4本（`PauseMenuView` / `ResultView` / `StageSelectView` / `SettingsView`）、`ProgressModel` / `SaveData` / `BattleContext` / `SpellData` / `EnemyAttackData` / `StageData`
- 新規アセット: `SND_Sound`、`MX_Main`、BGM・ジングル 12（ogg）、効果音 41（mp3）、ARC 15

---

## 2. 決めた方針（論点と結論）

| # | 論点 | 結論 | 補足 |
|---|---|---|---|
| 1 | 音の方向性 | 和楽器 ＋ 現代的な低音・打撃 | 素材リスト（`docs/sound_asset_list.md`）に方向性と探す語を書いた |
| 2 | ボス戦の曲 | 道中と分け、**全ステージ別の曲**（`StageData.bossBgm`） | 空ならボス戦も道中の曲（チュートリアルの木人戦） |
| 3 | BGM の切り替え方 | フェードアウト → 次の曲（AudioSource 1つ） | 仕様書の当初案はクロスフェード |
| 4 | ポーズ中の音 | 戦闘の効果音だけ止め、BGM は流す | `AudioListener.pause` は使わない |
| 5 | 固定の音の置き場所 | ScriptableObject 1つ（`SND_Sound`） | App と InGame の両方から同じアセットを参照 |
| 6 | ピッチのばらつき | **Audio Random Container** | ユーザーの追加要望。`PlayOneShot` が使えないので AudioSource を使い回す作りに |
| 7 | 素材の形式 | BGM は ogg に変換して入れる。効果音は mp3 のまま | GitHub・容量の都合（wav 10曲で約400MB が履歴に残る） |
| 8 | 勝敗のジングルと Result | ジングルは表示と同時に鳴らし、Result の曲はジングルが終わってから | Result に多少割り込むのは許容（ユーザー確認） |
| 9 | 音量設定 | 当初は入れない → **BGM / 効果音の2本を設定画面に追加**（AudioMixer） | 音量の釣り合いを探るのにも使えるため |
| 10 | ステージの曲の開始 | 「結印！」の後、最初の敵の登場と同時 | 開始演出の間は拍子木と太鼓だけ |
| 11 | 術の「放つ音」と「当たる音」 | **1つの術に1つの音** | 発動と命中が同じフレームのため（§4 #1） |
| 12 | 術の瞬間に重ねる共通音 | **弱点のアクセントだけ** | 共通の発動音・迅速・コンボ・スタンの音は削除。共通の命中音は予備として欄を残す |
| 13 | 敵の攻撃の瞬間 | 攻撃音＋結果の音（被弾・ガード成功） | |
| 14 | 溜めの音 | 溜め時間に合わせて再生速度を伸縮 | この欄だけ `AudioClip`（ARC 不可） |
| 15 | キャリブレーション・チュートリアル | 認識時に印の確定音を鳴らす。チュートリアルの段階クリアは鳴らさない | |
| 16 | 素材の長さ | 保留。プレイで違和感が出たら都度切る | 工数削減（ユーザー判断） |

---

## 3. 進め方の記録

| ステップ | 内容 | 主な変更 | 確認 |
|---|---|---|---|
| **調査** | 発音ポイントの洗い出し、音の方向性 | なし（36か所を A/B/C で整理） | — |
| **素材リスト** | 素材探し用の一覧 | `docs/sound_asset_list.md`（英語ファイル名・探す語・長さ・入手記録） | — |
| **1 土台** | 鳴らす部品と受け皿 | `SoundData` / `SePlayer` / `BgmPlayer`。`castSE` / `attackSE` を `AudioResource` に、`bossBgm` 追加、`BattleContext.Bgm`。AudioListener を App の `Audio` に1つ（MCP） | AudioListener の警告が消えた（`a64fc1f`） |
| **2 BGM** | 曲の配線 | `GameFlowPresenter`（画面の曲）/ `EnemyPresenter`（ステージ・ボス・ジングル）。BGM 12曲を割り当て（MCP） | 通しで曲が切り替わる |
| **3 優先度A** | 遊びに直結する効果音 | 印・術・暴発・弱点・敵の攻撃・被弾・ガード・溜め（伸縮）。ポーズの一時停止を前倒し | キーボードモードで確認 |
| **4 優先度B・C** | 演出・UI の効果音 | `SePlayer.PlayDelayed`、View の合図3種（カウントダウン・ハンコ・カーソル）。App 側の Presenter 4本 | 通しプレイ |
| **5a 音量設定** | 設定画面のスライダー | `ProgressModel` / `SaveData` / `SettingsView` / `AudioVolumeView`、`MX_Main`（ユーザー作成）、スライダー2本の配置（MCP） | スライダー・保存・ポーズからの設定 |
| **5b 素材** | 素材の当てはめと ARC | 効果音を割り当て（MCP）、ARC 15個（ユーザー作成）に差し替え、名前を `ARC_` に統一（MCP） | ばらつきの確認 |
| **6 文書** | ドキュメント | `architecture/03_sound.md` 新規、本書、他文書の更新（§8） | — |

---

## 4. つまずいた点

| # | 症状・気づき | 原因 | 対処 |
|---|---|---|---|
| 1 | 術の発動音と命中音が重なって違和感が出る見込み（ユーザーの指摘） | `OnSpellCast` → `ApplySpellDamage` → `OnSpellHit` が同じフレーム。見た目も同時 | 1術1音にし、同時に重ねる共通音を弱点だけに絞った（§2 #11・#12） |
| 2 | 計画のままだとジングルが「討伐」の表示より2秒早く、Result の曲で途中で切られる | `OnStageCleared` の瞬間に鳴らす計画だった。ボス撃破は表示を `BossDefeatDuration` 遅らせている | `PlayJingle(clip, delay)` を表示と同じゲーム内時間で待たせ、ジングル中の曲の要求は終わるまで待つようにした |
| 3 | Presenter 3本の差分が全行に広がった | 置換に使ったスクリプトが CRLF のファイルを LF で書き戻していた | CRLF に戻し、以降は改行コードを保つ置換で編集した |
| 4 | 作業中に WSL から `/mnt/c` が読めなくなった | WSL のマウントが切れた | ユーザーが再接続して再開 |
| 5 | 起動直後に Mixer の音量が反映されない恐れ | `Awake` 中の `AudioMixer.SetFloat` は無視されることがある（Unity の既知の挙動） | `AudioVolumeView` が値を覚え、`Start` でもう一度書く |
| 6 | Mixer の中身（グループ・公開パラメータ）と ARC の中身をスクリプトで作れない | Unity が公開していない | ユーザーがエディタで作成。割り当てと差し替えは MCP |
| 7 | ARC の名前が `ARC_` と `SE_` で混在し、元の mp3 と同名のものがあった | 作成時の命名の揺れ | `AssetDatabase.RenameAsset` で `ARC_` に統一（GUID は変わらないので割り当ては残る） |
| 8 | 計画の段階でユーザーの回答が選択肢の一覧のまま貼られた | 回答の取り消し | すべて推奨案として計画に書き、違えば指摘してもらう形にした |

---

## 5. 素材の当てはめ（完了時点）

| 区分 | 状態 |
|---|---|
| BGM | タイトル・リザルト・道中4・ボス4・ジングル2。すべて割り当て済み |
| 術の発動音 | 8種すべて |
| 敵の攻撃音 | 8系統の ARC を 24件（チュートリアルの木人 `EA_DummyStrike` は打撃） |
| `SND_Sound` | 35欄中 32欄。空は `healTick` / `ketsuinVoice` / `newRecord` |
| 代わりの素材 | `bossRoar` ← `SE_BossDefeat`、`uiToggle` ← `SE_UiDecide`（ユーザーが割り当て） |
| ARC | 15個。印の確定音は再生側で音程を上げるので ARC にしなかった |

---

## 6. 技術的負債・保留事項

| 項目 | 内容 |
|---|---|
| 空の欄・代わりの素材 | §5。素材が揃ったら `SND_Sound` に入れるだけ |
| 長い素材 | `SE_EnemyCharge`（3.3秒。短い溜めで再生速度が上限3倍に当たり頂点に届かない）、`SE_SignConfirm`（2.5秒）、`SE_BossDefeat`（6.9秒。勝利ジングルと重なる）、`SE_Advance`（10秒）、`SE_EnemyAppear`（5.5秒）など。違和感が出たら都度切る |
| 効果音の形式 | mp3 のまま。頭の短い無音が気になれば ogg に揃える（拡張子が変わるので割り当て直しが要る） |
| ジングルの長さ | 敗北 14.8秒の間、Result の曲が始まらない |
| `ARC_DotTick` の音程の幅 | −150〜+100 cent で左右非対称（意図なら問題なし） |
| 共通の命中音 `hit` | 欄と素材はあるが鳴らしていない |
| `TempHandTrackingRunner` の NRE | B6.5 から持ち越し。再生を止める時に `OnDestroy` で出る。音とは無関係 |
| 仕様書の B7 の内訳 | 「シーン遷移時クロスフェード」「AudioManager 設計」は、フェードアウト→フェードインと `SePlayer` / `BgmPlayer` の2部品に置き換えた（仕様書に反映済み） |

---

## 7. 学びどころ

| テーマ | 内容 |
|---|---|
| `AudioResource` | Unity 6 では `AudioClip` と `AudioRandomContainer` の共通の親。欄をこの型にすると、素材でも ARC でも入れられる |
| ARC の鳴らし方 | `PlayOneShot` は `AudioClip` 専用。ARC は `AudioSource.resource` に入れて `Play()`。重ねたいなら AudioSource を複数用意する |
| timeScale と音 | `AudioSource` は timeScale の影響を受けない。ポーズで止めたいなら自分で止める。待ち時間はゲーム内時間か実時間かを意図して選ぶ（03 §4-3） |
| AudioMixer | 音量を「グループ」で持つので、シーンをまたいでも1か所の書き換えで効く。音量は 0〜1 をそのまま渡さず `20 × log10` でデシベルにする |
| AudioListener | 1シーン（読み込み中の全シーン）に1つ。カメラの切り替えと一緒に動かすと重なる瞬間ができる |
| 同時に起きる出来事 | 「発動」と「命中」のように同じフレームで起きる出来事に別々の音を付けると重なる。どれを鳴らすかを決めるのは設計の仕事 |

---

## 8. ドキュメント更新

| ファイル | 内容 |
|---|---|
| `docs/architecture/03_sound.md` | **新規。** サウンドの断面図（全体図・構成要素・BGM・効果音・発音ポイント一覧・音量・ポーズ・作業ガイド・設計判断・困った時の確認順） |
| `docs/architecture/00_overview.md` | 技術スタック（AudioMixer・ARC）、フォルダ構成（`Audio/`）、起動シーケンスの BGM、シーンの `Audio` / `BattleAudio`、仕様書とのギャップからサウンドを削除、新しい術を足す時の音 |
| `docs/architecture/02_battle_system.md` | `SpellData.castSE` / `EnemyAttackData.attackSE` の型と使用状況、Presenter の配線一覧から 03 への参照 |
| `docs/specification_MUDRA.md` | v1.8 → v1.9。クラス一覧（`SoundData` / `SePlayer` / `BgmPlayer` / `AudioVolumeView`）、ScriptableObject 定義（`castSE` / `attackSE` / `bossBgm`）、B7 の進捗実績 |
| `docs/sound_asset_list.md` | 1術1音・攻撃音の選び方・溜めの素材・ボス曲4曲、ARC の命名、使わない音の削除 |
| `CLAUDE.md` | 現在地、規模、ドキュメント表、壊してはいけないもの（AudioListener・Mixer のパラメータ名） |

---

## 9. 次の作業
1. 保留の素材（§5・§6）を揃え次第、欄に入れる
2. `feature/sound` を main へ統合
3. B8（バランス調整）
