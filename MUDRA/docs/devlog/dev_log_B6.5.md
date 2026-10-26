# 📋 開発ログ：β版 B6.5（InGame の 2.5D 化）

> **ドキュメント種別:** マイルストーン開発ログ（3D 移行の専用構成）
> **対象フェーズ:** β版 B6.5（B6 と B7 の間に差し込んだ移行作業）
> **期間:** 2026/10/24〜10/26
> **ブランチ:** `feature/ingame-3d`（`9ea4856` → `f2ac1f2` → `012634f` → `4a2da96`）
> **ステータス:** ✅ 全ステージ・Tutorial の環境まで完了（コミット済み。main への統合は未）

---

## 1. 概要

| 項目 | 内容 |
|------|------|
| **出発点** | InGame は 2D（URP 2D Renderer・Orthographic）。奥行きは Sorting Layer（`Background` < `Enemy` < `Effect`）と、1枚絵の背景のズームで表していた |
| **ゴール** | InGame だけを **2.5D（3D 空間に絵を貼った板を立てる「書き割り」）** にする。HUD・Model・入力層は変えない |
| **結果** | Stage01〜04 にステージ専用の環境 Prefab、Tutorial は Stage01 を流用。前進演出はカメラごと奥へ進む。セクションごとに霧と空の色が変わる |
| **変えなかったもの** | Model（Pure C#）の差分ゼロ。入力層（MediaPipe〜`HandTrackingService`）・HUD（Overlay Canvas）・App 側の画面（2D のまま） |
| **進め方** | 調査（変更なし）→ 判断材料を元にユーザーが方針を決定 → 2D の性能を計測 → ステップごとに「提案 → 承認 → 実装 → ユーザーが再生して確認」。C# は手で書き、シーン・Prefab・マテリアル・取り込み設定は Unity MCP（`execute_code` 中心）で組んだ。素材画像はユーザーが生成 AI で用意した |

### 規模
- 自前コード: 117 ファイル・約 11,800 行 → **121 ファイル・約 12,490 行**
- 新規クラス: `StageEnvironmentView`（`BackgroundView` を改名・作り直し）/ `StageEnvironment` / `TiledSurface` / `ScrollingTexture` / `PerformanceRecorder`（Debug）
- 変更クラス: `CameraShakeView`（Impulse 化）/ `EnemyView`（接地・影）/ `EnemyPresenter`（呼び先の差し替えのみ）/ `StageData`（フィールド追加）
- 新規アセット: 環境 Prefab 4本、素材画像 50 枚（Stage01: 10 / Stage02: 13 / Stage03: 14 / Stage04: 13）、マテリアル・パーティクル・揺らぎのノイズ

---

## 2. 決めた方針（調査時の論点と結論）

着手前の調査で「判断が必要な論点」を9つ挙げ、ユーザーが決めた。

| # | 論点 | 結論 | 補足 |
|---|---|---|---|
| 1 | Tutorial も 3D にするか | **統一して 3D**（`InGame.unity` を共用しているため） | 最終的に Stage01 の環境を流用 |
| 2 | Renderer の構成 | **URP アセットに Universal Renderer を足し、カメラ単位で使い分け** | App は 2D Renderer のまま |
| 3 | 投影 | **Perspective** | 距離 8.66 にすると 2D 時代の座標・揺れ幅がそのまま使える（§4-2） |
| 4 | Cinemachine | **導入**（3.1.7） | 揺れは Impulse に置き換え |
| 5 | 敵のシェーダー | **Unlit**（Sprite-Unlit-Default） | 予告の明滅・影からの登場がそのまま成立する |
| 6 | 3D モデル化 | **板ポリと割り切る**（書き割り・HD-2D 寄り） | 2D の素材資産を活かせる |
| 7 | 前進の地点データ | **環境 Prefab 内のマーカー** | 移動方式はブレンドではなく「リグごと移動」に変更（§8-1） |
| 8 | 性能の目標 | **計測ツールを作って探る** | `PerformanceRecorder`（§9） |
| 9 | 未使用の 2D パッケージ | **後回し** | 効果が小さい（§12） |

---

## 3. 進め方の記録

| ステップ | 目的 | 主な変更 | 確認 | コミット |
|---|---|---|---|---|
| **0 計測** | 2D の基準値を取る | `Debug/PerformanceRecorder.cs`（F3 で CSV 記録） | 3場面を計測（§9） | `9ea4856` に同梱 |
| **1 最小構成** | 2D 素材のまま 3D 空間に置いても成立するか | Universal Renderer 追加、InGame カメラを Perspective・z=-8.66、背景を奥（z=10）へ、Sprite-Unlit-Default、影オフ。**コード変更なし** | 見た目が以前と同じ・性能維持 | `9ea4856` |
| **2a Cinemachine** | カメラの持ち主を Cinemachine に移す | `CinemachineBrain`、仮想カメラ `CM_BattleCamera`、Impulse Listener/Source。`CameraShakeView` を Impulse 化 | 揺れ方・ヒットストップ・ポーズ | `f2ac1f2` |
| **2b リグと前進** | 前進をカメラの移動で表す | `BattleRig` に戦闘一式をまとめ、`StageEnvironmentView` がマーカー間を移動。背景画像から廊下を自動生成 | 前進・デバッグジャンプ | `f2ac1f2` |
| **3 Stage01** | ステージ専用の環境を作る流れを確立 | 仮の箱で配置 → プロンプト資料 → 素材生成 → 当てはめ。`EnemyView` の接地と足元の影、`TiledSurface`、カメラの揺らぎ | スクリーンショット・通しプレイ | `012634f` |
| **4 Stage02〜04** | 残り3ステージと Tutorial | 共通部品（霧・`ScrollingTexture`・加算マテリアル・環境パーティクル）→ 3ステージの箱組み → プロンプト資料 → 当てはめ。幕の色の切り替え、Tutorial の流用 | スクリーンショット・通しプレイ | `4a2da96` |

---

## 4. 今の仕組み

### 4-1. Renderer とカメラの構成

| 対象 | 構成 |
|---|---|
| URP アセット（`Settings/UniversalRP.asset`） | Renderer List: 0 = `Renderer2D`（既定）、1 = `UniversalRenderer` |
| App のカメラ | 2D Renderer のまま。InGame を読み込んでいる間は止まる（`GameFlowPresenter`） |
| InGame の `Main Camera` | Renderer = `UniversalRenderer`、Perspective。`CinemachineBrain` が位置・レンズを毎フレーム書く。`CameraShakeView` と `CinemachineImpulseSource` もここ |
| `CM_BattleCamera`（`BattleRig` の子） | `CinemachineCamera`（FOV 60・ローカル z=-8.66・Orthographic Size 5）＋ `CinemachineImpulseListener` ＋ `CinemachineBasicMultiChannelPerlin`（`NS_CameraDrift`：位置だけのごく小さな揺らぎ） |
| HUD | Screen Space Overlay のまま。カメラの揺れ・移動の影響を受けない |

### 4-2. 距離 8.66 の根拠

```
見える高さ = 2 × 距離 × tan(FOV / 2)
FOV 60°・距離 8.66 → z=0 の面で見える高さ 10（= Orthographic Size 5 と同じ）
```
敵の立つ面（リグのローカル z=0）では、2D 時代の数値（登場スライド 10、揺れ幅 0.06〜0.5 など）が同じ見え方になる。

### 4-3. 階層（InGame.unity）

```
BattleField                 ← StageEnvironmentView
├─ (実行時) 環境 Prefab のインスタンス、または Environment (Generated)
└─ BattleRig                ← マーカーの位置・向きへ移動する
   ├─ CM_BattleCamera       (0, 0, -8.66)
   ├─ Enemy                 (0, 0, 0)    EnemyView。子に Body / EffectSpawnPoint / FootShadow
   ├─ PlayerEffectSpawnPoint (0, -3.2, 0)
   └─ BossDimOverlay        (0, 0, -7.5) カメラの直前。Sorting Layer で敵より奥に描く
```

### 4-4. 環境 Prefab（`StageEnvironment`）

| 項目 | 内容 |
|---|---|
| 置き場所 | `Assets/_MUDRA/Environment/Stage0N/ENV_Stage0N_*.prefab`。`StageData.environmentPrefab` に設定 |
| `_sectionAnchors` | セクション順のマーカー。**敵の立ち位置**。足りなければ最後のマーカーを使い警告 |
| `_sectionAtmospheres` | セクション順の空気。`useFog` / `fogColor` / `fogStart` / `fogEnd`（線形の霧）と `backdropTint`（遠景の幕の色。α=0 なら変えない） |
| `_backdrops` | `backdropTint` を掛ける遠景の幕（SpriteRenderer） |
| 床の約束 | **床はマーカーの y − 1.5**（`EnemyView` の足元の影がこの高さ固定） |

### 4-5. `StageEnvironmentView` の流れ

| メソッド | 呼ぶ所（`EnemyPresenter`） | 中身 |
|---|---|---|
| `Build(stage)` | `PrepareStage` / `HandleSectionStarted` | 同じステージなら何もしない。違えば環境を作り直す（Prefab か自動生成）。霧は一旦切る |
| `MoveToSection(index)` | `PrepareStage`（0）/ `HandleSectionStarted` | 既にそのセクションなら何もしない。違えばリグを即座に置き直し、霧と幕の色をそのセクションの値に |
| `PlayAdvance(toIndex, delay)` | `HandleSectionCleared` | 撃破演出の後、`advanceDuration`（1.2秒）で次のマーカーへ。位置・向き・霧・幕の色を同じ t で補間。ゲーム内時間なのでポーズで止まる |

`Environment (Generated)`（Prefab が無いステージ用）: セクションごとに背景画像の板を z = i×20+10 に立て、床（`M_Env_FallbackFloor`）を敷く。全ステージが Prefab を持った今は保険。

---

## 5. 描画のルール

| ルール | 理由 |
|---|---|
| 立てる物は **URP/Unlit・不透明＋Alpha Clipping** の板 | 不透明物は深度に書くので、何枚重なっても前後が位置で正しく決まる。半透明は距離順に並べ替えるため、板が多いと前後が入れ替わる |
| 余白はマテリアルの Tiling/Offset で切り落とす | 板の下端を絵の下端に合わせ、床に接地させる |
| 敵・遠景の幕は **Sprite（Sprite-Unlit-Default）** | 敵は霧に埋もれない。幕は霧でつぶれない（ただし霧で暗くもならない → `backdropTint` で沈める） |
| 半透明物どうしは **Sorting Layer が距離より優先** | `BossDimOverlay` をカメラの直前に置いても、敵より奥に描ける |
| 光の筋・炎の光・床の光の輪は **加算**（`M_Env_Additive_*`） | 黒が消え、明るさだけが足される |
| 炎・煙は **半透明（アルファ）** | 素材が透明背景で届いたため。縁のふわっとした部分を活かす |
| 霧がかかるのは URP/Unlit の物だけ | Sprite は霧に対応しない。霧で隠したい場面は、幕の色か物で塞ぐ |
| 画面は 16:9 固定 | 幕・背景の余白（1.1〜1.15倍）はこの前提で計算している |

---

## 6. ステージ環境の作り方（パイプライン）

```
① 仮の箱で配置（execute_code で Prefab を生成）→ マーカーごとのスクリーンショットで確認
② プロンプト資料（docs/stage0N_environment_asset_prompts.md）→ ユーザーが生成 AI で素材を作る
③ 当てはめ（取り込み設定 → マテリアル → 箱を板に差し替え → SD への割り当て）
④ スクリーンショットで確認 → ユーザーが通しプレイ
```

### 6-1. 取り込み設定

| 用途 | Texture Type | Mipmap | Wrap | Filter / Aniso |
|---|---|---|---|---|
| 立てる板（柱・鳥居・灯籠など） | Default | オン | Clamp | Trilinear / 8 |
| 横に繰り返す壁（塀・欄干・障子・石垣） | Default | オン | U: Repeat / V: Clamp | Trilinear / 8 |
| 床（地面・水面・板床） | Default | オン | Repeat | Trilinear / **16**（ほぼ真横から見るため） |
| 遠景の幕 | Sprite のまま | — | — | — |

### 6-2. マテリアルの種類

| 種類 | 設定 | 例 |
|---|---|---|
| 切り抜きの板 | URP/Unlit・Opaque・Alpha Clipping 0.5、Tiling/Offset で余白を切る | `M_Env_S0N_*` |
| 繰り返しの壁 | 同上。Tiling の y / Offset の y だけ縦の余白を切る（横は `TiledSurface` が決める） | 土塀・欄干・障子 |
| 床 | URP/Unlit・Opaque、Tiling = 床の大きさ ÷ 絵1枚の大きさ | `M_Env_Dirt` |
| 半透明 | Transparent・Alpha | 炎・煙 |
| 加算 | Transparent・Additive・両面 | `M_Env_Additive_LightShaft` / `Glow` / `GlowPurple` |

### 6-3. 部品

| 部品 | 役割 |
|---|---|
| `TiledSurface` | 板の長さに合わせて絵を横に繰り返す（回数 = 幅 ÷ (高さ × 切り落とし後の縦横比)）。縦の切り落としはマテリアルの値を引き継ぐ。エディタ上でも反映 |
| `ScrollingTexture` | 絵を時間で流す（水面・水面の裏側）。ゲーム内時間。`TiledSurface` と併用可 |
| `PS_Env_Embers` / `Bubbles` / `Motes` | 火の粉・泡・漂う光の粒（ワールド空間・ゲーム内時間） |
| `T_Env_LightShaft` | 光の筋のグラデーション（自作） |

### 6-4. 新しいステージを足すときのチェックリスト
1. `Environment/Stage0N/` に Prefab を作り、ルートに `StageEnvironment` を付ける
2. マーカーをセクション数ぶん置く（床はマーカーの y − 1.5）
3. 霧（`_sectionAtmospheres`）と幕（`_backdrops`・`backdropTint`）を設定する
4. 遠景の幕は z=400、`2 × 408.66 × tan30°` の高さを覆う大きさ ×1.1
5. 屋内・水中など空が見えない場所は、幕の色を沈めるか物で塞ぐ
6. `StageData.environmentPrefab` に割り当てる
7. マーカーごとにスクリーンショットで確認（§10-6 の手順）

---

## 7. ステージごとの記録

値は Prefab の実値（`4a2da96` 時点）。霧は 開始–終了 の距離、色は 16 進。

| ステージ | セクション | マーカー | 霧 | 幕の色 | 見どころ |
|---|---|---|---|---|---|
| **Stage01 封土の里** | 0 / 1 / 2(ボス) | (0,0,0) / (0,0,20) / (0,0,44) | なし | — | 0→1 で鳥居をくぐる。2 は広場 |
| **Stage02 水底の社** | 0 / 1 / 2 / 3(ボス) | (0,0,0) / (0,0,20) / (0,0,50) / **(0,-6,68)** | 859EA3 14–95 / 8099A1 12–85 / 759199 10–75 / **1A454F 2–38** | S3 のみ 336673 | 1→2 で大鳥居をくぐる。2→3 で**水面を抜けて潜る**。水面と裏側は流れる |
| **Stage03 焔の廃寺** | 0 / 1 / 2 / 3(ボス) | (0,0,0) / (0,0,20) / **(0,2,44)** / **(0,3.5,68)** | 453D3D 20–110 / 453D3D 18–100 / 473833 16–90 / 4C2E1F 8–60 | — | 石段を2回上る。2→3 で焦げた鳥居をくぐり本堂へ |
| **Stage04 影の深淵** | 0 / 1 / 2 / 3(ボス) | (0,0,0) / (0,0,20) / (0,0,40) / (0,0,68) | 4C4C5E 15–100 / 333345 10–70 / **0D0A14 5–32** / 120F1C 10–50 | — / 9E9EB8 / 120F1C / 120F1C | 進むほど暗くなり、2 で空まで闇に沈む。2→3 で山門をくぐり社殿へ |
| **Tutorial** | 0(ボス扱い) | Stage01 のマーカー0 | なし | — | 敵は木人のデータ（HP 3000）で、見た目・名前・弱点を泥ころに |

残っている仮の箱（`Materials/Placeholder` を使うもの）: Stage02 14個（水中の石段・水底の脇の壁など）、Stage03 3個（鴨居・梁）、Stage04 7個（天井の梁・山門脇の闇の壁）。どれも暗い単色で遠目には目立たない。

---

## 8. 設計判断の記録

### 8-1. 前進は「リグごと移動」（仮想カメラのブレンドではなく）
Cinemachine のブレンドで動くのはカメラだけで、敵・エフェクトの出る位置・暗転板は別途動かす必要がある。カメラ・敵・`PlayerEffectSpawnPoint`・`BossDimOverlay` を `BattleRig` にまとめ、リグを LitMotion で動かせば、互いの位置関係（2D 時代に合わせた値）が崩れず、`DamageNumberView` / `SpellEffectView` も無改造で済む。揺れは仮想カメラの出力に足されるのでリグの移動とぶつからない。ブレンドはカメラアングルの切り替えが必要になった時に使う。

### 8-2. `BackgroundView` は `.meta` ごと改名
`git mv` で `.cs` と `.meta` を一緒に移し、スクリプトの GUID を保った。シーンからの参照は切れない（ただしフィールドが変わったので値は付け直し）。

### 8-3. 敵の拡縮は絵の下端を基準に、踏み込みは -Z に
2D では中心基準の拡縮で問題なかったが、床に立つとボス（1.6倍）が床にめり込む。拡大した分だけ `Body` を持ち上げ、足元を固定した。攻撃の踏み込みは「画面下へ」から「カメラ方向へ」に変えた。足元の影は Quad なので、色は `SpriteRenderer.color` ではなく `MaterialPropertyBlock` で変える。

### 8-4. 揺れは Impulse のカスタム波形
標準の Rumble は「0 から膨らみ、中央で最大」の形で、被弾の瞬間に一番強く揺れる 2D 時代の手触りと合わない。2D の式（減衰する正弦波）をカーブにして渡す。Impulse は重ねると足されるので、`CinemachineImpulseManager.Instance.Clear()` で前の揺れを消してから発生させる（2D の「上書き」と同じ）。1方向しか揺れないので、方向を毎回ランダムにした。

### 8-5. 霧はセクションごと、補間は「霧なし＝遠くへ押しやった霧」
水中・闇のようにセクションで空気が変わるため、マーカーごとに霧を持たせた。霧なしのセクションと補間する時は、霧を描画範囲の外（2000）へ押しやった状態として扱い、色は霧のある側を使う（途中で灰色が混ざらない）。

### 8-6. InGame の霧は「線形・オン」で保存（Fog Stripping 対策）
Fog Stripping が Automatic なので、どのシーンも霧を使っていないとビルドで霧のシェーダー機能が削られる。InGame の霧を線形・開始 500 で保存し、実際の霧は `Build` / `MoveToSection` で必ず上書きする。バトル中は InGame がアクティブシーンなので、アンロードすれば App の設定に戻る（後始末不要）。

### 8-7. 遠景の幕は Sprite のまま、色で沈める
幕を霧のかかる板にすると、霧の終了距離（数十〜100）より遠い幕は霧の色一色になり、絵が消える。Sprite のまま残し、闇・水中では `backdropTint` で暗くする。当初は闇の場面を「黒い壁で塞ぐ」で作ったが、手前のセクションから空に長方形が浮いたため、この方式に変えた。

### 8-8. `TiledSurface` は縦の切り落としを引き継ぐ
障子・石垣・土手の絵には上下に透明な余白があり、繰り返しの値で上書きすると壁が床から浮く。マテリアルの Tiling y / Offset y を保ち、切り落とした後の縦横比で回数を決める。横は繰り返すので切り落とせない（左右の余白はそのまま）。

### 8-9. カメラの揺らぎは位置だけ
回転の揺れでは手前と奥がずれない（視差が出ない）。位置だけをごくゆっくり揺らす（X 振幅 0.12・0.13Hz、Y 0.06・0.09Hz）。

---

## 9. 性能計測

### 9-1. 計測ツール（`Debug/PerformanceRecorder.cs`）
- App に置く。Development Build で F3 を押すと記録開始・停止。1秒ごとに `Application.persistentDataPath/perf_日時.csv` へ書く
- 列: `t, scene, fps, frame_avg_ms, frame_max_ms, main_ms, infer_hz, hands`
- `infer_hz`（1秒間に届いた推論結果の数）がこのゲーム固有の指標。Windows では推論が CPU で動くため、描画の CPU 負荷と取り合う
- `main_ms` は VSync 待ちを含むので、60fps に収まっている間は余裕の大きさを表さない（改善案は見送り）

### 9-2. 結果（Development Build・カメラあり・各約60秒）

| 場面 | 指標 | 2D | Step1（3D） |
|---|---|---|---|
| Title | FPS / 推論 Hz | 59.93 / 29.35 | 59.93 / 29.65 |
| 道中 | FPS / 推論 Hz / 33ms 落ち | 59.75 / 29.47 / 9回（約7秒に1回） | 59.93 / 29.70 / **0回** |
| ボス | FPS / 推論 Hz / 33ms 落ち | 59.82 / 29.63 / 6回（約9秒に1回） | 59.93 / 29.71 / **0回** |

- 推論は約 30Hz で、Web カメラの映像レートが上限と見られる【推測】
- 2D の定期的なフレーム落ちは、当初 `DebugMenuView.DrawFps()` の毎フレームの `new GUIStyle` による GC を疑ったが、3D 版でも同じコードのまま消えたので**推測は外れ**。原因は未特定（2D Renderer 側の何かか）
- **Stage01 の素材入れ以降（板・霧・パーティクル・2048px テクスチャ約50枚）は未計測**（§12）

---

## 10. つまずいた点

| # | 症状 | 原因 | 対処 |
|---|---|---|---|
| 1 | 2a の後、背景だけ2倍に拡大された | `CinemachineCamera` の Lens の Orthographic Size は既定 10。Perspective でも Brain が Main Camera に丸ごと写し、`orthographicSize` を読む背景の計算が狂った | 仮想カメラの値を 5 に（MCP）。2b で背景の計算自体を廃止 |
| 2 | 提案した Rumble 波形が手触りに合わない | Rumble は中央が最大の片側の山 | 2D の式をカスタムカーブに（§8-4） |
| 3 | Stage04 で空に暗い長方形が浮いた | 闇を作るための黒い壁が、手前のセクションからも見えた | 幕の色を切り替える方式に（§8-7） |
| 4 | Stage02 の参道から黒い板が見えた | 水底の脇の壁の上端が水面より上に出ていた | 壁を水面の直下までに |
| 5 | 障子・石垣が床から浮く見込み | `TiledSurface` が余白の切り落としを上書きしていた | 縦の切り落としを引き継ぐ（§8-8） |
| 6 | プレビュー撮影でシーンを汚しかける | Prefab を置いたり霧を変えたりすると InGame が変更扱いになる | 置く物は `HideFlags.DontSave`、撮影後に削除し、霧を保存値（線形 500–1000）に戻してから保存。`.unity` に `__PREVIEW_ENV` が無いことを grep で確認 |
| 7 | 素材の想定違い（炎・煙が透明背景） | 資料では炎は黒背景（加算）、煙は白背景（乗算）の予定だった | 半透明で重ねる。縁に白っぽいふちが少し残る（§12） |

---

## 11. 2D と 3D の違い（学びどころのまとめ）

| テーマ | 2D のとき | 3D（今） |
|---|---|---|
| 投影 | Orthographic：距離に関係なく同じ大きさ | Perspective：遠いほど小さい。基準にする面の距離を決めると 2D の数値を引き継げる |
| 前後の決まり方 | Sorting Layer / Order | 不透明物は**深度バッファ**。半透明物は**カメラからの距離順**で、Sorting Layer が距離より優先 |
| 見えなくなる所 | — | **ニアクリップ（0.3）**より手前は描かれない（板を通り抜けた瞬間に消える） |
| 奥行きの手がかり | 絵の中のパースだけ | 収束する線（床・壁）、**視差**（平行移動の時だけ出る。回転では出ない）、接地（足元の影）、霧 |
| カメラの持ち主 | Main Camera を直接動かす | `CinemachineBrain` が仮想カメラの値を毎フレーム写す。**見えない値（Orthographic Size など）まで写す** |
| 揺れ | カメラの位置を書き換える | Impulse の **Source（信号を出す）と Listener（受けて仮想カメラの出力に足す）** |
| 色の変え方 | `SpriteRenderer.color` | メッシュは `MaterialPropertyBlock`（マテリアルを複製せずに個別の値を持てる） |
| テクスチャ | そのまま | 斜めから見る物は**ミップマップ**（遠くのちらつき防止）と**異方性フィルタリング**（斜めのぼやけ防止） |
| 空気 | — | 霧は URP/Unlit にかかり Sprite にはかからない。シーン単位の設定で、アクティブシーンのものが使われる |

---

## 12. 技術的負債・保留事項

| 項目 | 内容 |
|---|---|
| **性能の再計測** | 素材入れ以降は未計測。Stage04 の社殿など物の多い場面で、推論 Hz ≒ 30・FPS ≒ 60 を確かめる |
| 仮の箱 | §7 の数。`Materials/Placeholder/` は残りの箱が使っているので消していない |
| 炎の縁 | Stage03 の炎に白っぽいふちが少し残る（切り抜き時の色残り【推測】）。絵を差し替えれば反映される |
| カメラの構図 | 目線が床から 1.5 と低く、床の面積が大きい。上げるなら HUD との重なりも調整が要る（保留） |
| 地面の繰り返し | Stage01 の土は繰り返しが少し見える（Tiling を大きくすれば軽減） |
| `TempHandTrackingRunner` の NRE | 無効のまま再生を止めると `OnDestroy` で `_service` が null。`_service?.Dispose()` で直る（未対応） |
| Audio Listener の重複の警告 | App と InGame のカメラに1つずつ。B7（サウンド）で一緒に見る |
| 未使用の 2D パッケージ | spriteshape / tilemap(+extras) / aseprite / psdimporter / animation。自前アセットでの使用は無し。整理は効果が小さい |
| README | 「現在のステータス」が B2 で止まっている（B6.5 の範囲外） |
| `PerformanceRecorder` の `main_ms` | VSync 待ちを含む。余裕を見たいなら VSync 待ちを除いた値を取る |

---

## 13. ドキュメント更新（反映済み）

| ファイル | 内容 |
|---|---|
| `docs/architecture/00_overview.md` | 技術スタック（Cinemachine・Renderer 構成）、View 一覧、InGame の構成表と描画のルール、環境アセットの置き場所 |
| `docs/architecture/02_battle_system.md` | `EnemyPresenter` の配線表、描画構成、View 一覧（`StageEnvironmentView` / `CameraShakeView` / `EnemyView`） |
| `docs/specification_MUDRA.md` | v1.7 → v1.8。クラス一覧と Enemy MVP の例の `BackgroundView` を `StageEnvironmentView` に |
| `CLAUDE.md` | 現在地、規模、Renderer 構成、ドキュメント表 |
| `docs/stage01〜04_environment_asset_prompts.md` | 素材生成のプロンプト資料（作業中に作成） |

---

## 14. 次の作業
1. 性能の再計測（§12）
2. `feature/ingame-3d` を main へ統合
3. B7（サウンド）。Audio Listener の重複も合わせて確認する
