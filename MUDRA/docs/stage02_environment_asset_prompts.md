# Stage02「水底の社」 3D環境用 画像素材の生成プロンプト

画像生成AIに渡すためのまとめ。**前提・条件は日本語、プロンプト本文は英語**で書いている。
プロンプトは前置きと末尾の指定を含めた完成形にしてあり、そのまま貼れる。
形式と運用は `stage01_environment_asset_prompts.md`（v2）に揃えている。

---

## 1. 現状（生成AIに伝えておくこと）

- 「MUDRA」は、Webカメラで手印を組んで術を放つ和風ボス戦アクション（Unity / Windows）
- バトル画面は **2.5D（3D空間に「板」を立てて絵を貼る、舞台の書き割りのような表現）**。HD-2D に近い方向性。立体モデルではなく、**平面の絵を板に貼って3D空間に並べる**
- ステージ1「封土の里」は素材の当てはめまで完了。今回はその続きの素材
- ステージ2「水底の社」は、浅く水の張った参道を進み、最後に**水の中へ潜って**水底に沈んだ社でボス（水蛇）と戦う
  - 道中（セクション0〜2）: 水面の参道、両側に苔むした石灯籠の列、枝を広げた古木、大きな鳥居（水に立つ）。霧が濃い
  - ボス（セクション3）: 水底。砂の底、石の円柱、沈んだ社、傾いた鳥居、水草。上から光の筋が差す（光の筋はゲーム側で作る）
- 3D空間の形は、単色の箱で仮組み済み。届いた素材を箱と差し替える
- **ゲーム内ではライティングを使わない（Unlit）。** 光と影は絵に描かれたものがそのまま出る
- 遠くの物は霧でかすむ（ゲーム側で付ける）。絵に霧やかすみを描き込まなくてよい

### 参考画像（毎回2枚とも添付する）

既存の背景2枚が画風の基準。この2枚と同じ画風・色調で揃える。

- `Assets/_MUDRA/Sprites/Background/BG_Stage02_Route.png`（道中）
- `Assets/_MUDRA/Sprites/Background/BG_Stage02_Boss.png`（ボス）

遠景（編集型）だけは、編集元の1枚だけを添付する。
参考画像だけだと構図まで真似されやすいので、前置きに「構図や場面は真似ない」の一文を入れてある。消さないこと。

### このステージの色

道中は**青みがかった灰緑**（霧の参道）。石は苔で緑がかった灰色。水底は**暗い青緑**。暖色は使わない。参考画像の青白い光の粒は、絵に描き込まなくてよい（ゲーム側でパーティクルを出す）。

---

## 2. 全素材に共通する条件

| 項目 | 条件 |
|---|---|
| 画風 | 参考画像と同じ。**水彩の塗り＋墨の線画**。くすんだ低彩度。色の方向性は下の「このステージの色」に従う |
| 光 | **柔らかい光、正面やや上から**。強い影・はっきりした落ち影は描かない（Unlitなので、絵の影がそのまま残る）。光を絵に焼き込んでよいのは、各素材の条件で許可したものだけ |
| 視点 | **真正面（または真横）から見た平面図**。パース（遠近法）を付けない。斜め上からの視点にしない。例外は各素材の条件に書いたもの（床は真上視点） |
| 背景 | 切り抜く素材は**薄いグレー（#D0D0D0）の単色**。紙の質感、紙の四角、枠、チェッカー柄は付けない。床と遠景は背景なしで画面いっぱいに描く。炎は黒、煙は白（下の「特殊な背景」） |
| 地面 | 物の下に地面や床、落ち影を描かない（3Dの床に立てるため） |
| 輪郭 | 透明部分との境目をくっきり。半透明のにじみ、白い縁取り、**シルエットの外へはみ出す光や霧**を描かない |
| 文字 | 札や石碑の文字は、読めないかすれた墨の筆跡程度にする |
| 構図 | 対象を画像の中央に置き、上下左右に余白を取る。ただし左右につなげる素材は、左右の端まで通す |

### 置き場所と名前

- 届いた画像は `Assets/_MUDRA/Sprites/Environment/Stage02/` に置く（取り込み設定はClaude側で行う）
- ファイル名は下の表の通り。英語（ASCII）の `プレフィックス_PascalCase`

---

## 3. 素材一覧と生成比率

「3Dでの大きさ」はゲーム内の寸法（1ユニット≒1m）。Unity側では、届いた画像の縦横比から板の幅を決め、高さは3D側の寸法を基準にする。

| # | 素材 | ファイル名 | 3Dでの大きさ（幅×高さ） | 生成比率 | 画像サイズ（目安） | 背景 | 優先度 |
|---|---|---|---|---|---|---|---|
| 1 | 苔むした石灯籠 | `T_Env_S02_StoneLantern.png` | 1.3 × 2.5 | 1:2 | 768 × 1536 | グレー（切り抜き） | A |
| 2 | 古木 | `T_Env_S02_OldTree.png` | 8 × 11 | 3:4 | 1536 × 2048 | グレー（切り抜き） | A |
| 3 | 大鳥居（水に立つ） | `T_Env_S02_Torii.png` | 10.5 × 7.6 | 7:5 | 2048 × 1460 | グレー（切り抜き） | A |
| 4 | 水面（真上・繰り返し） | `T_Env_S02_WaterSurface.png` | 6 × 6 を敷き詰める | 1:1 | 1024 × 1024 | なし（全面） | A |
| 5 | 霧の社と石段（中景） | `T_Env_S02_DistantShrine.png` | 14 × 9 | 3:2 | 2048 × 1365 | グレー（切り抜き） | B |
| 6 | 遠景（霧の森と山） | `T_Env_S02_Backdrop.png` | 画面全体 | 16:9 | 3840 × 2160 | なし（全面） | A |
| 7 | 砂の水底（真上・繰り返し） | `T_Env_S02_SeaFloor.png` | 6 × 6 を敷き詰める | 1:1 | 1024 × 1024 | なし（全面） | A |
| 8 | 石の円柱（水没） | `T_Env_S02_StoneColumn.png` | 2.4 × 12 | 1:5 | 410 × 2048 | グレー（切り抜き） | A |
| 9 | 沈んだ社 | `T_Env_S02_SunkenShrine.png` | 12 × 8 | 3:2 | 2048 × 1365 | グレー（切り抜き） | A |
| 10 | 傾いた鳥居（水底） | `T_Env_S02_BrokenTorii.png` | 6.5 × 5.5 | 6:5 | 1536 × 1280 | グレー（切り抜き） | B |
| 11 | 水草 | `T_Env_S02_Seaweed.png` | 1.5 × 3 | 1:2 | 768 × 1536 | グレー（切り抜き） | B |
| 12 | 水中の奥壁（遠景） | `T_Env_S02_UnderwaterBackdrop.png` | 28 × 16（奥の壁一面） | 16:9 | 3840 × 2160 | なし（全面） | A |
| 13 | 水面の裏側（見上げ・繰り返し） | `T_Env_S02_WaterUnderside.png` | 6 × 6 を敷き詰める | 1:1 | 1024 × 1024 | なし（全面） | B |

優先度 **A** が揃えば、仮の箱をほぼ全部置き換えられる。**B** は雰囲気の上積み（無ければ箱のまま、または他の素材の流用で済ませる）。

### 作らなくてよいもの（流用・自作）

- 光の筋 … グラデーションのテクスチャを Claude が自作済み（`Environment/Textures/T_Env_LightShaft.png`）
- 傾いた鳥居 … #3 の鳥居を傾けて流用してもよい（#10 は B）

---

## 4. 素材ごとのプロンプト

すべてのプロンプトは、前置き（画風と参考画像の使い方）＋素材ごとの本文＋末尾（背景色とはみ出し禁止）をつないだ完成形。
画風や末尾の指定を直したくなったら、全素材で同じ文言に揃えて直すこと。

### 共通の前置き

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows.
```

### 共通の末尾（切り抜き素材）

```
The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #1 苔むした石灯籠 `T_Env_S02_StoneLantern.png`

**条件**

- 参道の両側に並べる、苔むした石灯籠1基。**真正面から**
- 火袋（光の入る部分）の窓は暗いまま。灯りは描かない
- 足元は水に浸かる想定だが、水面は描かない（ゲーム側の水面に立てる）

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A single old Japanese stone lantern (ishidoro) covered in damp dark moss and lichen, wet stone surface, a square base, a slim shaft, a fire box with dark empty windows and a curved pagoda-like roof cap. Strict front view at eye level. Tall 1:2 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #2 古木 `T_Env_S02_OldTree.png`

**条件**

- 参道の外側に立てる、ねじれた古木1本。**真正面から**
- 幹は画像の左寄りに立ち、枝は**右側（道の上）へ大きく張り出す**片側だけの形。反対側には左右反転して置く
- 葉は暗い緑で、枝先にまとまって付く。幹の根元は画像の下端で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A single ancient gnarled tree with a thick twisted trunk standing near the left side of the image, with heavy branches reaching far out to the right over empty space, dark green foliage clustered at the branch tips, moss on the bark. Strict front view at eye level. The trunk base is cut off flat at the bottom edge. Tall 3:4 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #3 大鳥居（水に立つ） `T_Env_S02_Torii.png`

**条件**

- 参道の途中に立てる大きな鳥居。**真正面から**（左右対称）。カメラがくぐる
- 朱ではなく、**苔むした灰緑の石（または古い木）**。参考画像の鳥居に寄せる
- 下の横木から注連縄と紙垂が垂れる。中央に小さな額
- 柱の下端は画像の下端で切る（水面に立てる）

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A large old Japanese torii gate made of weathered gray-green stone covered in moss, strict symmetrical front view, a gently curved upper lintel (kasagi), a lower crossbeam (nuki), a small blank plaque in the center, and a thick straw shimenawa rope with white zigzag shide hanging under the lower beam. The pillar bottoms are cut off flat at the bottom edge of the image. 7:5 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #4 水面（真上・繰り返し） `T_Env_S02_WaterSurface.png`

**条件**

- 参道の床に敷く浅い水面。**真上から見た図**
- 上下左右につなげて繰り返す。ゲーム側でゆっくり流すので、目立つ模様（大きな波紋、映り込み、石）は入れない
- 暗い青緑に、細い墨線のさざ波と、ごく控えめな白いきらめき

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A seamless tileable texture of a calm shallow water surface seen straight from above, flat top-down orthographic view with no horizon and no reflection of objects, filling the whole square image edge to edge. Dark teal-gray water with thin flowing ink lines of small ripples and a few faint pale glints. Low contrast, evenly distributed detail, nothing distinctive near the edges, so the image can repeat in all four directions. Square 1:1 image.
```

### #5 霧の社と石段（中景） `T_Env_S02_DistantShrine.png`

**条件**

- 参道の先、鳥居の向こうに立てる中景の板。霧の中にかすむ社と、そこへ上る石段
- **真正面から**。石段の下端は画像の下端で切る
- 霧はゲーム側で付けるので、社そのものはくっきり描いてよい

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A Japanese shrine building on top of a short flight of stone steps with two small stone lanterns at the sides, strict front view at eye level, dark weathered wood and gray stone, symmetrical composition. The bottom of the steps is cut off flat at the bottom edge. 3:2 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #6 遠景（霧の森と山） `T_Env_S02_Backdrop.png`

**条件**

- 一番奥の背景幕。参考画像（道中）から、手前の鳥居・石灯籠・水面・社と石段を取り除いた版
- 上から霧の空、山、両脇から覆いかぶさる暗い森
- 画面の下45%は手前の3Dの床で隠れるので、霧で灰色にぼかしてよい
- **新規生成より編集のほうが確実。** 案A（編集）を先に試す

**案A（編集。まずこちら）** 添付は `BG_Stage02_Route.png` の1枚だけ

```
Edit the attached image. Remove every foreground and midground object: the torii gate, all stone lanterns, the shrine and its stone steps, and the water surface. Fill the freed area by naturally extending the misty sky, the distant mountains and the dark forest trees on both sides. The lower 45 percent of the image becomes soft flat teal-gray fog that fades smoothly, with no ground details. Keep exactly the same art style, colors, line quality and 16:9 aspect ratio. Do not add any text.
```

**案B（新規生成。案Aが崩れたとき）** 添付は参考画像2枚

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A distant background matte painting: a misty teal-gray sky, soft layered ink-wash mountains far away, and dark ancient forest trees framing both sides. The lower part fades into flat teal-gray fog with no ground details. Do not include buildings, torii gates, lanterns, water or any foreground objects. 16:9 image.
```

### #7 砂の水底（真上・繰り返し） `T_Env_S02_SeaFloor.png`

**条件**

- 水底の床。**真上から見た図**
- 上下左右につなげて繰り返す。目立つ石や貝は入れない
- 暗い青緑の砂に、細かいムラと小石

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A seamless tileable texture of a sandy underwater floor seen straight from above, flat top-down orthographic view, filling the whole square image edge to edge. Dark teal sand and silt with subtle watercolor mottling, tiny pebbles and faint ripple marks. Low contrast, evenly distributed detail, no large or distinctive features, nothing important near the edges, so the image can repeat in all four directions. Square 1:1 image.
```

### #8 石の円柱（水没） `T_Env_S02_StoneColumn.png`

**条件**

- 水底の両側に立つ、太い石の円柱1本。**真正面から**
- 上端は折れて欠けている。苔や水草が少し絡む
- 柱の下端は画像の下端で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A single tall thick round stone column from a sunken ruin, strict front view, weathered gray-green stone with cracks, a broken jagged top, patches of dark moss and a few strands of seaweed clinging to it. The column base is cut off flat at the bottom edge. Very tall 1:5 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #9 沈んだ社 `T_Env_S02_SunkenShrine.png`

**条件**

- 水底の正面奥に置く、沈んだ社（参考画像（ボス）の中央の建物）
- 正面からやや斜め（ここだけ少し角度があってよい。見下ろしにはしない）
- 屋根が傾き、海藻や水草が絡む。足元は砂に埋もれる（砂の地面は描かない）

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A large sunken Japanese shrine building resting at the bottom of the water, front three-quarter view at eye level, dark waterlogged wood, a sagging tiled roof with seaweed growing on it, broken railings and a frayed shimenawa rope with shide across the entrance. 3:2 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border.
```

### #10 傾いた鳥居（水底） `T_Env_S02_BrokenTorii.png`

**条件**

- 水底に沈んだ、傾いた鳥居。**真正面から**
- 片方の柱が折れかけ、全体が右へ傾いている。水草が絡む
- 柱の下端は画像の下端で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. An old sunken wooden torii gate leaning to the right, one pillar cracked, strict front view, dark waterlogged wood covered in seaweed and small barnacles. The pillar bottoms are cut off flat at the bottom edge. 6:5 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #11 水草 `T_Env_S02_Seaweed.png`

**条件**

- 水底に散らす水草のひと群れ。**真正面から**
- 細長い葉が上へ伸びる。根元は画像の下端で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A single clump of tall slender seaweed fronds growing upward, dark teal-green with ink outlines, strict front view. The base is cut off flat at the bottom edge. Tall 1:2 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #12 水中の奥壁（遠景） `T_Env_S02_UnderwaterBackdrop.png`

**条件**

- 水底の一番奥に立てる背景。参考画像（ボス）から、手前の社・鳥居・円柱・水草を取り除いた版
- 奥へ続く暗い水中と、遠くにかすむ遺跡の影。上の方は水面からの光でわずかに明るい
- **編集のほうが確実。** 案A（編集）を先に試す

**案A（編集。まずこちら）** 添付は `BG_Stage02_Boss.png` の1枚だけ

```
Edit the attached image. Remove the shrine building, both torii gates, the large stone columns in the foreground, the seaweed and the rubble on the floor. Fill the freed area by naturally extending the deep dark teal water and the faint distant ruins in the background. Keep the faint light from the water surface at the top. Keep exactly the same art style, colors, line quality and 16:9 aspect ratio. Do not add any text.
```

**案B（新規生成。案Aが崩れたとき）** 添付は参考画像2枚

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. An underwater background matte painting: deep dark teal water fading into darkness, faint silhouettes of distant ruined pillars and walls, the upper part slightly brighter from the water surface far above. Do not include the shrine, torii gates or any foreground objects. 16:9 image.
```

### #13 水面の裏側（見上げ・繰り返し） `T_Env_S02_WaterUnderside.png`

**条件**

- 潜った後に頭上に見える水面の裏側。**真下から見上げた図**
- 上下左右につなげて繰り返す。明るい青緑の揺らめく網目模様（コースティクス）

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, misty damp teal-gray mood, soft even flat lighting from the front, no strong shadows. A seamless tileable texture of a water surface seen from below, looking straight up, filling the whole square image edge to edge. Pale teal light with soft wavy caustic net patterns drawn with thin lines, slightly brighter than the surroundings. Evenly distributed, nothing distinctive near the edges, so the image can repeat in all four directions. Square 1:1 image.
```

---

## 5. 派生の作り方

同じ素材が並ぶと繰り返しが目立つ。数が多く並ぶ素材は2〜3種類あるとよい（下の表の素材）。完成画像を1枚目に添付し、参考画像2枚を続けて添付してから、次を貼る（`〇〇` を素材名に書き換える）。

対象: 石灯籠（#1）、水草（#11）

```
The attached first image is the base asset (〇〇). Create a variant in exactly the same style, colors, line quality and size, but with a different arrangement of small details (cracks, moss, damage). Keep the same view and exactly the same aspect ratio as the base image. The attached second and third images are only art style references, do not copy their composition. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border.
```

---

## 6. 受け取ったあとの処理（取り込み側への申し送り）

Stage01 と同じ。
1. **切り抜き**: AIの背景除去で抜き、崩れた箇所は手作業で直す。切り抜き後は暗い背景と明るい背景の両方に置いて、縁にグレーが残っていないかを確認する。炎（黒背景）と煙（白背景）は切り抜かない
2. **トリミング**: 比率が守られなかった素材は、対象の外接矩形でトリミングして目標比率に合わせる。左右につなげる素材は、左右の端を残したまま上下だけ詰める
3. **継ぎ目**: 左右・四辺につなげる素材は、半幅ずらして継ぎ目を中央に寄せ、境目を混ぜて直す
4. **置き場所**: `Assets/_MUDRA/Sprites/Environment/Stage02/` に置く。取り込み設定・マテリアル・Prefab への当てはめは Claude が行う
5. **記録**: 使った生成ツール、生成日、プロンプトを `CREDITS.md` に残す

うまく生成できない素材は、無理に揃えなくてよい。その素材だけ仮の箱のまま残して先に進められる。

---

## 7. 運用メモ

- 1素材ごとに新しいチャットで、参考画像2枚を添付してからプロンプトを貼る（会話が長いと画風がずれやすい）
- Stage01 の完成素材（例: `T_Env_S01_StonePost.png`）を3枚目に添付すると、ステージをまたいで線の太さや塗りが揃いやすい。その場合は前置きの「two images」を「three images」に書き換える
- 1つのプロンプトにつき数枚生成し、一番良いものを採用する
- 極端な比率（1:5 など）は守られない可能性が高い。守られなければ 16:9 / 9:16 / 4:3 / 1:1 で生成し、トリミングで合わせる

