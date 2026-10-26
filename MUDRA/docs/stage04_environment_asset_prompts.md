# Stage04「影の深淵」 3D環境用 画像素材の生成プロンプト

画像生成AIに渡すためのまとめ。**前提・条件は日本語、プロンプト本文は英語**で書いている。
プロンプトは前置きと末尾の指定を含めた完成形にしてあり、そのまま貼れる。
形式と運用は `stage01_environment_asset_prompts.md`（v2）に揃えている。

---

## 1. 現状（生成AIに伝えておくこと）

- 「MUDRA」は、Webカメラで手印を組んで術を放つ和風ボス戦アクション（Unity / Windows）
- バトル画面は **2.5D（3D空間に「板」を立てて絵を貼る、舞台の書き割りのような表現）**。HD-2D に近い方向性。立体モデルではなく、**平面の絵を板に貼って3D空間に並べる**
- ステージ1「封土の里」は素材の当てはめまで完了。今回はその続きの素材
- ステージ4「影の深淵」（最終ステージ）は、山の石畳を進み、**闇の中の山門をくぐって**大社殿の中でボス（影龍）と戦う
  - 道中（セクション0〜1）: 山の石畳、木の欄干、苔むした石垣、紫に灯る石灯籠、松、両脇に迫る巨岩と竹
  - セクション2: 闇の入口。山門の先は真っ暗（暗さはゲーム側の霧で作る）
  - ボス（セクション3）: 大社殿の内部。板床、注連縄を巻いた太い柱の列、障子の壁、天井の梁、天窓からの光（光の筋はゲーム側で作る）
- 3D空間の形は、単色の箱で仮組み済み。届いた素材を箱と差し替える
- **ゲーム内ではライティングを使わない（Unlit）。** 光と影は絵に描かれたものがそのまま出る
- 遠くの物は霧でかすむ（ゲーム側で付ける）。絵に霧やかすみを描き込まなくてよい

### 参考画像（毎回2枚とも添付する）

既存の背景2枚が画風の基準。この2枚と同じ画風・色調で揃える。

- `Assets/_MUDRA/Sprites/Background/BG_Stage04_Route.png`（道中）
- `Assets/_MUDRA/Sprites/Background/BG_Stage04_Boss.png`（ボス）

遠景（編集型）だけは、編集元の1枚だけを添付する。
参考画像だけだと構図まで真似されやすいので、前置きに「構図や場面は真似ない」の一文を入れてある。消さないこと。

### このステージの色

全体は**夜の青灰と墨の黒**。差し色は**灯籠と光のかすかな紫**だけ。社殿の内部は暗い木の焦げ茶と灰紫。

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

- 届いた画像は `Assets/_MUDRA/Sprites/Environment/Stage04/` に置く（取り込み設定はClaude側で行う）
- ファイル名は下の表の通り。英語（ASCII）の `プレフィックス_PascalCase`

---

## 3. 素材一覧と生成比率

「3Dでの大きさ」はゲーム内の寸法（1ユニット≒1m）。Unity側では、届いた画像の縦横比から板の幅を決め、高さは3D側の寸法を基準にする。

| # | 素材 | ファイル名 | 3Dでの大きさ（幅×高さ） | 生成比率 | 画像サイズ（目安） | 背景 | 優先度 |
|---|---|---|---|---|---|---|---|
| 1 | 石畳（真上・繰り返し） | `T_Env_S04_Flagstone.png` | 4 × 4 を敷き詰める | 1:1 | 1024 × 1024 | なし（全面） | A |
| 2 | 木の欄干（繰り返し） | `T_Env_S04_Railing.png` | 4 × 1.4（左右につなげる） | 3:1 | 1536 × 512 | グレー（切り抜き） | A |
| 3 | 苔むした石垣（繰り返し） | `T_Env_S04_StoneWall.png` | 13 × 3.2（左右につなげる） | 4:1 | 2048 × 512 | グレー（切り抜き） | A |
| 4 | 紫に灯る石灯籠 | `T_Env_S04_PurpleLantern.png` | 1.3 × 2.5 | 1:2 | 768 × 1536 | グレー（切り抜き） | A |
| 5 | 松 | `T_Env_S04_Pine.png` | 6 × 9 | 2:3 | 1365 × 2048 | グレー（切り抜き） | A |
| 6 | 巨岩 | `T_Env_S04_Boulder.png` | 4.5 × 7 | 2:3 | 1365 × 2048 | グレー（切り抜き） | A |
| 7 | 山門 | `T_Env_S04_Gate.png` | 11 × 8 | 7:5 | 2048 × 1460 | グレー（切り抜き） | A |
| 8 | 社殿の板床（真上・繰り返し） | `T_Env_S04_HallFloor.png` | 4 × 4 を敷き詰める | 1:1 | 1024 × 1024 | なし（全面） | A |
| 9 | 障子の壁（繰り返し） | `T_Env_S04_ShojiWall.png` | 21 × 10.5（左右につなげる） | 2:1 | 2048 × 1024 | グレー（切り抜き） | A |
| 10 | 社殿の柱 | `T_Env_S04_HallPillar.png` | 2 × 10 | 1:5 | 410 × 2048 | グレー（切り抜き） | A |
| 11 | 天井の梁（見上げ・繰り返し） | `T_Env_S04_Ceiling.png` | 4 × 4 を敷き詰める | 1:1 | 1024 × 1024 | なし（全面） | B |
| 12 | 本殿の奥壁 | `T_Env_S04_InnerSanctum.png` | 20 × 10 | 2:1 | 2048 × 1024 | なし（全面） | A |
| 13 | 遠景（闇の山と霧） | `T_Env_S04_Backdrop.png` | 画面全体 | 16:9 | 3840 × 2160 | なし（全面） | A |

優先度 **A** が揃えば、仮の箱をほぼ全部置き換えられる。**B** は雰囲気の上積み（無ければ箱のまま、または他の素材の流用で済ませる）。

### 作らなくてよいもの（流用・自作）

- 光の筋・床の光の輪 … Claude が自作済み（`T_Env_LightShaft.png` と丸い光）
- 柱の間の注連縄 … Stage01 の `T_Env_S01_Shimenawa.png` を暗く色付けして流用
- 竹 … Stage01 の `T_Env_S01_BambooGrove.png` を暗く色付けして流用

---

## 4. 素材ごとのプロンプト

すべてのプロンプトは、前置き（画風と参考画像の使い方）＋素材ごとの本文＋末尾（背景色とはみ出し禁止）をつないだ完成形。
画風や末尾の指定を直したくなったら、全素材で同じ文言に揃えて直すこと。

### 共通の前置き

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows.
```

### 共通の末尾（切り抜き素材）

```
The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #1 石畳（真上・繰り返し） `T_Env_S04_Flagstone.png`

**条件**

- 道の床。**真上から見た図**
- 上下左右につなげて繰り返す。不揃いな平石と、目地の苔

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. A seamless tileable texture of an old mountain path paved with irregular flat flagstones seen straight from above, flat top-down orthographic view, filling the whole square image edge to edge, dark blue-gray stone with thin moss in the joints. Low contrast, evenly distributed detail, nothing distinctive near the edges, so the image can repeat in all four directions. Square 1:1 image.
```

### #2 木の欄干（繰り返し） `T_Env_S04_Railing.png`

**条件**

- 道の両側に、道と平行に立てる木の欄干1区間。**真横から**
- 左端に柱が1本、上下2本の横木。右端は次の区間の柱につながるよう、柱を描かずに横木を端まで通す
- 横木の間は抜ける（背景色）。下端は床で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. One bay of an old Japanese wooden railing seen strictly from the side, one square post at the left edge, a top rail and a lower rail running from the post to the right edge of the image, weathered dark gray wood, empty background between the rails. The rails continue past the right edge so the bay can repeat horizontally. The post bottom is cut off flat at the bottom edge. Wide 3:1 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #3 苔むした石垣（繰り返し） `T_Env_S04_StoneWall.png`

**条件**

- 欄干の外側に、道と平行に立てる石垣。**真横から見た立面図**
- 大小の石を積み、目地と上面に苔。左右につなげて繰り返す
- 上の縁は草で少しなだらかに。下端は床で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. A dry-stacked Japanese stone retaining wall (ishigaki) seen strictly from the side, irregular dark gray stones of mixed sizes with thick moss in the joints and on the top edge, a few small ferns, running past both the left and right edges at the same height with nothing distinctive near the edges, so it can repeat horizontally. The bottom is cut off flat at the bottom edge. Very wide 4:1 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #4 紫に灯る石灯籠 `T_Env_S04_PurpleLantern.png`

**条件**

- 道沿いに並べる石灯籠1基。**真正面から**
- 火袋の窓の**内側だけ**、かすかな紫の灯り（窓の外へ漏らさない）

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. A single old Japanese stone lantern (ishidoro), dark blue-gray stone with moss, a square base, a slim shaft, a fire box whose small windows show a faint violet light inside, and a curved roof cap. The glow stays inside the window openings. Strict front view at eye level. Tall 1:2 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #5 松 `T_Env_S04_Pine.png`

**条件**

- 道の外側に立てる、ねじれた松1本。**真正面から**
- 幹は画像の左寄り、枝は**右側（道の上）へ張り出す**片側だけの形。反対側には左右反転して置く
- 根元は画像の下端で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. A single old twisted Japanese black pine tree, the trunk standing near the left side of the image, branches with flat layered needle clusters reaching out to the right, painted with strong ink strokes, dark blue-gray tones. Strict front view at eye level. The trunk base is cut off flat at the bottom edge. 2:3 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #6 巨岩 `T_Env_S04_Boulder.png`

**条件**

- 道の両脇に迫る、縦長の巨岩1つ。**真正面から**
- ゴツゴツした面に苔と小さな竹の葉。下端は床で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. A single huge tall jagged boulder, strict front view at eye level, dark blue-gray rock with deep ink-drawn cracks, thick moss on the ledges and a few small bamboo leaves at the base. The bottom is cut off flat at the bottom edge. 2:3 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #7 山門 `T_Env_S04_Gate.png`

**条件**

- 闇の入口に立つ山門。**真正面から**（左右対称）。カメラがくぐる
- 太い柱2本、重い瓦屋根。門の奥（開口部の中）は**真っ黒**に塗る
- 柱の下端は画像の下端で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. A heavy old Japanese mountain temple gate (sanmon) seen in strict symmetrical front view, two thick dark wooden pillars, a massive tiled roof with deep eaves, faded paper talismans on the pillars, and the opening between the pillars painted completely solid black. The pillar bottoms are cut off flat at the bottom edge. 7:5 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #8 社殿の板床（真上・繰り返し） `T_Env_S04_HallFloor.png`

**条件**

- 大社殿の床。**真上から見た図**。板は奥行き方向（画像の縦）に並ぶ
- 上下左右につなげて繰り返す。暗い木に、かすかな艶

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. A seamless tileable texture of old polished dark wooden floor planks in a shrine hall seen straight from above, the planks run vertically in the image, dark brown-gray wood with a faint cool sheen and fine grain. Evenly distributed detail, nothing distinctive near the edges, so the image can repeat in all four directions. Square 1:1 image.
```

### #9 障子の壁（繰り返し） `T_Env_S04_ShojiWall.png`

**条件**

- 大社殿の両側の壁。**真横から見た立面図**
- 下に板壁、中ほどに障子の格子、上に欄間と梁。紙は灰紫で暗い
- 左右につなげて繰り返す。下端は床で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. The tall interior wall of a dark Japanese shrine hall, strict orthographic elevation, a wooden wainscot at the bottom, a row of shoji lattice panels with dim gray-violet paper in the middle, and carved transom panels and beams at the top, running past both the left and right edges at the same height with nothing distinctive near the edges, so it can repeat horizontally. The bottom is cut off flat at the bottom edge. Wide 2:1 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #10 社殿の柱 `T_Env_S04_HallPillar.png`

**条件**

- 社殿の太い柱1本。**真正面から**
- 暗い木の丸柱。中ほどに注連縄が巻かれ、紙垂が垂れる。下端は床で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. A single tall thick round wooden pillar of a dark shrine hall, strict front view, dark aged wood with fine grain, a thick shimenawa rope wrapped around its middle with white zigzag shide hanging from it. The bottom is cut off flat at the bottom edge. Very tall 1:5 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #11 天井の梁（見上げ・繰り返し） `T_Env_S04_Ceiling.png`

**条件**

- 社殿の天井。**真下から見上げた図**
- 格子状の梁と、間の暗い板。上下左右につなげて繰り返す

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. A seamless tileable texture of a dark coffered wooden ceiling seen from directly below, a grid of dark beams with shadowed panels between them, filling the whole square image edge to edge, nothing distinctive near the edges, so the image can repeat in all four directions. Square 1:1 image.
```

### #12 本殿の奥壁 `T_Env_S04_InnerSanctum.png`

**条件**

- 社殿の一番奥に立てる壁（遠景の代わり）。参考画像（ボス）の奥の部分
- **真正面から**（左右対称）。中央に本殿へ上る短い階段と閉じた扉、上に注連縄と紙垂
- 画面いっぱいに描く（背景なし）。光の筋は描かない（ゲーム側で足す）

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. The far inner wall of a dark Japanese shrine hall seen in strict symmetrical front view, a short flight of wooden steps in the center leading up to closed sanctum doors, a thick shimenawa rope with white shide hanging above the doors, dark wooden pillars and panels on both sides, filling the whole image edge to edge. Do not draw any beam of light. Wide 2:1 aspect ratio image.
```

### #13 遠景（闇の山と霧） `T_Env_S04_Backdrop.png`

**条件**

- 一番奥の背景幕。参考画像（道中）から、手前の石段・欄干・石灯籠・岩・松・竹を取り除いた版
- 上から暗い雲の空、霧の山並み。中央奥は闇に沈む
- 画面の下45%は霧でぼかしてよい
- **編集のほうが確実。** 案A（編集）を先に試す

**案A（編集。まずこちら）** 添付は `BG_Stage04_Route.png` の1枚だけ

```
Edit the attached image. Remove every foreground and midground object: the stone path and steps, the wooden railings, the stone lanterns, the large rocks, the pine trees and the bamboo. Fill the freed area by naturally extending the dark cloudy sky, the misty mountains and the deep darkness in the center. The lower 45 percent of the image becomes soft flat blue-gray fog, with no ground details. Keep exactly the same art style, colors, line quality and 16:9 aspect ratio. Do not add any text.
```

**案B（新規生成。案Aが崩れたとき）** 添付は参考画像2枚

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, dark gloomy night mood with a faint violet tint, soft even flat lighting from the front, no strong shadows. A distant background matte painting: a dark night sky with streaks of ink-wash clouds, misty layered mountains, and a deep darkness in the center distance. The lower part fades into flat blue-gray fog with no ground details. Do not include paths, railings, lanterns, rocks, trees or any foreground objects. 16:9 image.
```

---

## 5. 派生の作り方

同じ素材が並ぶと繰り返しが目立つ。数が多く並ぶ素材は2〜3種類あるとよい（下の表の素材）。完成画像を1枚目に添付し、参考画像2枚を続けて添付してから、次を貼る（`〇〇` を素材名に書き換える）。

対象: 紫に灯る石灯籠（#4）、巨岩（#6）、社殿の柱（#10）

```
The attached first image is the base asset (〇〇). Create a variant in exactly the same style, colors, line quality and size, but with a different arrangement of small details (cracks, moss, damage). Keep the same view and exactly the same aspect ratio as the base image. The attached second and third images are only art style references, do not copy their composition. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border.
```

---

## 6. 受け取ったあとの処理（取り込み側への申し送り）

Stage01 と同じ。
1. **切り抜き**: AIの背景除去で抜き、崩れた箇所は手作業で直す。切り抜き後は暗い背景と明るい背景の両方に置いて、縁にグレーが残っていないかを確認する。炎（黒背景）と煙（白背景）は切り抜かない
2. **トリミング**: 比率が守られなかった素材は、対象の外接矩形でトリミングして目標比率に合わせる。左右につなげる素材は、左右の端を残したまま上下だけ詰める
3. **継ぎ目**: 左右・四辺につなげる素材は、半幅ずらして継ぎ目を中央に寄せ、境目を混ぜて直す
4. **置き場所**: `Assets/_MUDRA/Sprites/Environment/Stage04/` に置く。取り込み設定・マテリアル・Prefab への当てはめは Claude が行う
5. **記録**: 使った生成ツール、生成日、プロンプトを `CREDITS.md` に残す

うまく生成できない素材は、無理に揃えなくてよい。その素材だけ仮の箱のまま残して先に進められる。

---

## 7. 運用メモ

- 1素材ごとに新しいチャットで、参考画像2枚を添付してからプロンプトを貼る（会話が長いと画風がずれやすい）
- Stage01 の完成素材（例: `T_Env_S01_StonePost.png`）を3枚目に添付すると、ステージをまたいで線の太さや塗りが揃いやすい。その場合は前置きの「two images」を「three images」に書き換える
- 1つのプロンプトにつき数枚生成し、一番良いものを採用する
- 極端な比率（1:5 など）は守られない可能性が高い。守られなければ 16:9 / 9:16 / 4:3 / 1:1 で生成し、トリミングで合わせる

