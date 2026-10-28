# Stage03「焔の廃寺」 3D環境用 画像素材の生成プロンプト

画像生成AIに渡すためのまとめ。**前提・条件は日本語、プロンプト本文は英語**で書いている。
プロンプトは前置きと末尾の指定を含めた完成形にしてあり、そのまま貼れる。
形式と運用は `stage01_environment_asset_prompts.md`（v2）に揃えている。

---

## 1. 現状（生成AIに伝えておくこと）

- 「MUDRA」は、Webカメラで手印を組んで術を放つ和風ボス戦アクション（Unity / Windows）
- バトル画面は **2.5D（3D空間に「板」を立てて絵を貼る、舞台の書き割りのような表現）**。HD-2D に近い方向性。立体モデルではなく、**平面の絵を板に貼って3D空間に並べる**
- ステージ1「封土の里」は素材の当てはめまで完了。今回はその続きの素材
- ステージ3「焔の廃寺」は、焼け落ちた寺の境内を進み、**石段を上って**燃えた本堂の中でボス（炎魔）と戦う
  - 道中（セクション0〜1）: 焦げた土の境内、左に焼け落ちたお堂、右に草の土手、火の灯る石灯籠、崩れた材木、立ちのぼる煙
  - セクション2: 石段を上った踊り場。焦げた鳥居をくぐって本堂へ
  - ボス（セクション3）: 燃えた本堂の内部。焦げた板床、黒焦げの柱の列、破れた障子の壁、頭上の崩れた梁。隙間の奥に燃える外
- 3D空間の形は、単色の箱で仮組み済み。届いた素材を箱と差し替える
- **ゲーム内ではライティングを使わない（Unlit）。** 光と影は絵に描かれたものがそのまま出る
- 遠くの物は霧でかすむ（ゲーム側で付ける）。絵に霧やかすみを描き込まなくてよい

### 参考画像（毎回2枚とも添付する）

既存の背景2枚が画風の基準。この2枚と同じ画風・色調で揃える。

- `Assets/_MUDRA/Sprites/Background/BG_Stage03_Route.png`（道中）
- `Assets/_MUDRA/Sprites/Background/BG_Stage03_Boss.png`（ボス）

遠景（編集型）だけは、編集元の1枚だけを添付する。
参考画像だけだと構図まで真似されやすいので、前置きに「構図や場面は真似ない」の一文を入れてある。消さないこと。

### このステージの色

全体は**黒焦げの茶と煙の灰**。暖色は**残り火の橙**だけに絞る（炎・灯籠の火・焦げ跡の縁）。火の粉はゲーム側でパーティクルを出すので、絵に散らさなくてよい。

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

### 特殊な背景（炎と煙だけ）

炎と煙は、切り抜かずに**重ね方（ブレンド）で背景を消す**。
- **炎は黒背景** … ゲームでは「加算」で重ねる。黒は足しても変わらないので、黒い部分が自動で消え、炎だけが光る
- **煙は白背景** … ゲームでは「乗算」で重ねる。白は掛けても変わらないので、白い部分が自動で消え、煙だけが暗く残る

どちらも、ふわっとした半透明の縁をそのまま活かせる（グレー背景で切り抜くと縁が汚くなる）。

### 置き場所と名前

- 届いた画像は `Assets/_MUDRA/Sprites/Environment/Stage03/` に置く（取り込み設定はClaude側で行う）
- ファイル名は下の表の通り。英語（ASCII）の `プレフィックス_PascalCase`

---

## 3. 素材一覧と生成比率

「3Dでの大きさ」はゲーム内の寸法（1ユニット≒1m）。Unity側では、届いた画像の縦横比から板の幅を決め、高さは3D側の寸法を基準にする。

| # | 素材 | ファイル名 | 3Dでの大きさ（幅×高さ） | 生成比率 | 画像サイズ（目安） | 背景 | 優先度 |
|---|---|---|---|---|---|---|---|
| 1 | 焼け落ちたお堂（側面） | `T_Env_S03_BurntHall.png` | 21 × 7 | 3:1 | 2048 × 683 | グレー（切り抜き） | A |
| 2 | 草の土手（右側・繰り返し） | `T_Env_S03_Embankment.png` | 16 × 4（左右につなげる） | 4:1 | 2048 × 512 | グレー（切り抜き） | B |
| 3 | 焦げた土の地面（真上・繰り返し） | `T_Env_S03_ScorchedGround.png` | 4 × 4 を敷き詰める | 1:1 | 1024 × 1024 | なし（全面） | A |
| 4 | 火の灯る石灯籠 | `T_Env_S03_FireLantern.png` | 1.3 × 2.5 | 1:2 | 768 × 1536 | グレー（切り抜き） | A |
| 5 | 崩れた材木 | `T_Env_S03_FallenTimber.png` | 3 × 1.2 | 5:2 | 1536 × 614 | グレー（切り抜き） | B |
| 6 | 石段の石（繰り返し） | `T_Env_S03_StoneSlab.png` | 2 × 2 を敷き詰める | 1:1 | 1024 × 1024 | なし（全面） | A |
| 7 | 焦げた鳥居 | `T_Env_S03_CharredTorii.png` | 9.2 × 6.5 | 7:5 | 2048 × 1460 | グレー（切り抜き） | A |
| 8 | 立ちのぼる煙 | `T_Env_S03_Smoke.png` | 4 × 8 | 1:2 | 1024 × 2048 | 白（乗算） | B |
| 9 | 炎 | `T_Env_S03_Flame.png` | 2 × 3 | 2:3 | 1024 × 1536 | 黒（加算） | B |
| 10 | 焦げた板床（真上・繰り返し） | `T_Env_S03_CharredPlanks.png` | 4 × 4 を敷き詰める | 1:1 | 1024 × 1024 | なし（全面） | A |
| 11 | 破れた障子の壁（繰り返し） | `T_Env_S03_TornShojiWall.png` | 15 × 5（左右につなげる） | 3:1 | 2048 × 683 | グレー（切り抜き） | A |
| 12 | 焦げた柱 | `T_Env_S03_CharredPillar.png` | 1.6 × 8 | 1:5 | 410 × 2048 | グレー（切り抜き） | A |
| 13 | 崩れた梁 | `T_Env_S03_BrokenBeam.png` | 12 × 2 | 6:1 | 2048 × 341 | グレー（切り抜き） | B |
| 14 | 遠景（黒煙の空と燃える森） | `T_Env_S03_Backdrop.png` | 画面全体 | 16:9 | 3840 × 2160 | なし（全面） | A |

優先度 **A** が揃えば、仮の箱をほぼ全部置き換えられる。**B** は雰囲気の上積み（無ければ箱のまま、または他の素材の流用で済ませる）。

### 作らなくてよいもの（流用・自作）

- 石段の段 … 箱に #6 の石の面を貼る（段の形は3D側で作る）

---

## 4. 素材ごとのプロンプト

すべてのプロンプトは、前置き（画風と参考画像の使い方）＋素材ごとの本文＋末尾（背景色とはみ出し禁止）をつないだ完成形。
画風や末尾の指定を直したくなったら、全素材で同じ文言に揃えて直すこと。

### 共通の前置き

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows.
```

### 共通の末尾（切り抜き素材）

```
The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #1 焼け落ちたお堂（側面） `T_Env_S03_BurntHall.png`

**条件**

- 道の左側に、道と平行に立てるお堂の側面。**真横から見た立面図**
- 柱が並び、屋根が焼け落ちて骨組みがのぞく。焦げ跡の縁にわずかな残り火
- 左右の端は画像の端まで通す（端に別の建物を描かない）。下端は床で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. The long side of a burnt-out Japanese temple hall, strict orthographic side elevation, a row of charred wooden pillars, a collapsed roof with exposed blackened rafters, broken walls and faint orange embers glowing along the burnt edges. The hall runs past both the left and right edges of the image. The bottom is cut off flat at the bottom edge. Wide 3:1 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #2 草の土手（右側・繰り返し） `T_Env_S03_Embankment.png`

**条件**

- 道の右側に、道と平行に立てる草の土手。**真横から**
- 左右につなげて繰り返す。焦げた切り株や灰がところどころ
- 下端は床で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A low grassy earth embankment seen strictly from the side, dull dark green grass and weeds, a few charred stumps and patches of gray ash, running past both the left and right edges at the same height with nothing distinctive near the edges, so it can repeat horizontally. The bottom is cut off flat at the bottom edge. Very wide 4:1 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #3 焦げた土の地面（真上・繰り返し） `T_Env_S03_ScorchedGround.png`

**条件**

- 境内の床。**真上から見た図**
- 上下左右につなげて繰り返す。灰と焦げ跡のムラ。目立つ物は入れない

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A seamless tileable texture of scorched packed earth seen straight from above, flat top-down orthographic view, filling the whole square image edge to edge. Dark brown earth with patches of gray ash, faint black scorch marks and tiny bits of charcoal. Low contrast, evenly distributed detail, no large or distinctive features, nothing important near the edges, so the image can repeat in all four directions. Square 1:1 image.
```

### #4 火の灯る石灯籠 `T_Env_S03_FireLantern.png`

**条件**

- 道沿いに並べる石灯籠1基。**真正面から**
- 火袋の窓の**内側だけ**、橙の火が灯っている（窓の外へ光を漏らさない。光の広がりはゲーム側で足す）
- 石は煤で黒ずんでいる

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A single old Japanese stone lantern (ishidoro) blackened with soot, a square base, a slim shaft, a fire box whose small windows show a warm orange flame inside, and a curved roof cap. The glow stays inside the window openings. Strict front view at eye level. Tall 1:2 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #5 崩れた材木 `T_Env_S03_FallenTimber.png`

**条件**

- 道端に散らす、崩れた焦げた材木の山。**正面から**、低く横長

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A low pile of fallen charred timber beams and broken planks with blackened ends and faint orange embers in the cracks, front view at eye level, low wide shape. Wide 5:2 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #6 石段の石（繰り返し） `T_Env_S03_StoneSlab.png`

**条件**

- 石段・踊り場の箱に貼る石の面。**正面から見た平らな石の面**
- 上下左右につなげて繰り返す。煤と細かいひび

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A seamless tileable texture of weathered gray granite stone slabs seen straight on, flat orthographic view, filling the whole square image edge to edge, faint soot stains and fine cracks, subtle joints between slabs. Evenly distributed detail, nothing distinctive near the edges, so the image can repeat in all four directions. Square 1:1 image.
```

### #7 焦げた鳥居 `T_Env_S03_CharredTorii.png`

**条件**

- 踊り場の先に立てる、焼けて黒焦げの鳥居。**真正面から**（左右対称でなくてよい。上の横木が少し傾く）
- 焦げ跡の縁にわずかな残り火。注連縄は焼け落ちて切れ端だけ
- 柱の下端は画像の下端で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. An old wooden torii gate badly burnt and blackened, front view at eye level, the upper lintel slightly tilted and cracked, charred surfaces with faint orange embers along the burnt edges, only a frayed scorched remnant of a shimenawa rope left. The pillar bottoms are cut off flat at the bottom edge. 7:5 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #8 立ちのぼる煙 `T_Env_S03_Smoke.png`

**条件**

- 境内に立てる煙の柱。**白背景**（乗算で重ねるので、白い部分は消える）
- 下から上へ広がりながら立ちのぼる、墨のにじみのような暗い灰色の煙。輪郭はふわっとしてよい

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A single column of dark gray smoke rising upward and spreading out, painted like soft ink wash bleeding on paper, darker at the bottom and fading lighter toward the top, soft feathered edges. Tall 1:2 aspect ratio image. The background must be one flat solid pure white (#FFFFFF) color filling the whole image with no gradient and no texture. Do not include text, a frame or a border.
```

### #9 炎 `T_Env_S03_Flame.png`

**条件**

- 燃えている材木や建物の隙間に重ねる炎。**黒背景**（加算で重ねるので、黒い部分は消える）
- 下が明るい橙、上へ行くほど赤く細くなる。墨線は細く

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A single tall flickering flame, bright orange and yellow at the base turning deep red at the tips, painted in watercolor with thin ink lines, soft glowing edges. 2:3 aspect ratio image. The background must be one flat solid pure black (#000000) color filling the whole image with no gradient and no texture. Do not include text, a frame or a border.
```

### #10 焦げた板床（真上・繰り返し） `T_Env_S03_CharredPlanks.png`

**条件**

- 本堂の床。**真上から見た図**。板は奥行き方向（画像の縦）に並ぶ
- 上下左右につなげて繰り返す。焦げ跡と灰のムラ

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A seamless tileable texture of old charred wooden floor planks seen straight from above, the planks run vertically in the image, dark brown wood with black scorch patches, gray ash and faint glowing cracks. Evenly distributed detail, nothing distinctive near the edges, so the image can repeat in all four directions. Square 1:1 image.
```

### #11 破れた障子の壁（繰り返し） `T_Env_S03_TornShojiWall.png`

**条件**

- 本堂の両側の壁。**真横から見た立面図**
- 障子の格子が焦げ、紙は破れて穴がある。穴から**燃える外の橙の光**が見える（穴の中だけ）
- 左右につなげて繰り返す。下端は床で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A wall of burnt Japanese shoji sliding panels, strict orthographic elevation, charred wooden lattice frames with torn and holed paper, warm orange firelight visible only through the holes, running past both the left and right edges at the same height with nothing distinctive near the edges, so it can repeat horizontally. The bottom is cut off flat at the bottom edge. Wide 3:1 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #12 焦げた柱 `T_Env_S03_CharredPillar.png`

**条件**

- 本堂の柱1本。**真正面から**
- 黒焦げで、表面がひび割れ、縁に残り火。下端は床で切る

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A single tall round wooden pillar burnt black, strict front view, cracked charred surface like alligator skin with faint orange embers in the deepest cracks. The bottom is cut off flat at the bottom edge. Very tall 1:5 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #13 崩れた梁 `T_Env_S03_BrokenBeam.png`

**条件**

- 頭上に渡す、折れて垂れ下がった梁。**真横から**
- 片方の端は折れてギザギザ、黒焦げ

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A long charred wooden roof beam seen from the side, one end broken off with jagged splinters, blackened and cracked with faint embers. Very wide 6:1 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #14 遠景（黒煙の空と燃える森） `T_Env_S03_Backdrop.png`

**条件**

- 一番奥の背景幕。参考画像（道中）から、手前のお堂・石段・鳥居・石灯籠・地面を取り除いた版
- 上から黒煙の空、山、燃える森（残り火の橙）
- 画面の下45%は霧と煙で灰色にぼかしてよい
- **編集のほうが確実。** 案A（編集）を先に試す

**案A（編集。まずこちら）** 添付は `BG_Stage03_Route.png` の1枚だけ

```
Edit the attached image. Remove every foreground and midground object: the burnt temple hall on the left, the stone stairs and railings, the torii gate, the stone lanterns and the ground. Fill the freed area by naturally extending the smoky dark sky, the mountains and the dimly burning forest. The lower 45 percent of the image becomes soft flat smoky gray haze, with no ground details. Keep exactly the same art style, colors, line quality and 16:9 aspect ratio. Do not add any text.
```

**案B（新規生成。案Aが崩れたとき）** 添付は参考画像2枚

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, smoky charred mood with dim warm ember light, soft even flat lighting from the front, no strong shadows. A distant background matte painting: a dark sky filled with billowing black smoke, ink-wash mountains, and a burning forest with dim orange fire far away. The lower part fades into flat smoky gray haze with no ground details. Do not include buildings, torii gates, stairs, lanterns or any foreground objects. 16:9 image.
```

---

## 5. 派生の作り方

同じ素材が並ぶと繰り返しが目立つ。数が多く並ぶ素材は2〜3種類あるとよい（下の表の素材）。完成画像を1枚目に添付し、参考画像2枚を続けて添付してから、次を貼る（`〇〇` を素材名に書き換える）。

対象: 火の灯る石灯籠（#4）、焦げた柱（#12）、破れた障子の壁（#11）

```
The attached first image is the base asset (〇〇). Create a variant in exactly the same style, colors, line quality and size, but with a different arrangement of small details (cracks, moss, damage). Keep the same view and exactly the same aspect ratio as the base image. The attached second and third images are only art style references, do not copy their composition. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border.
```

---

## 6. 受け取ったあとの処理（取り込み側への申し送り）

Stage01 と同じ。
1. **切り抜き**: AIの背景除去で抜き、崩れた箇所は手作業で直す。切り抜き後は暗い背景と明るい背景の両方に置いて、縁にグレーが残っていないかを確認する。炎（黒背景）と煙（白背景）は切り抜かない
2. **トリミング**: 比率が守られなかった素材は、対象の外接矩形でトリミングして目標比率に合わせる。左右につなげる素材は、左右の端を残したまま上下だけ詰める
3. **継ぎ目**: 左右・四辺につなげる素材は、半幅ずらして継ぎ目を中央に寄せ、境目を混ぜて直す
4. **置き場所**: `Assets/_MUDRA/Sprites/Environment/Stage03/` に置く。取り込み設定・マテリアル・Prefab への当てはめは Claude が行う
5. **記録**: 使った生成ツール、生成日、プロンプトを `CREDITS.md` に残す

うまく生成できない素材は、無理に揃えなくてよい。その素材だけ仮の箱のまま残して先に進められる。

---

## 7. 運用メモ

- 1素材ごとに新しいチャットで、参考画像2枚を添付してからプロンプトを貼る（会話が長いと画風がずれやすい）
- Stage01 の完成素材（例: `T_Env_S01_StonePost.png`）を3枚目に添付すると、ステージをまたいで線の太さや塗りが揃いやすい。その場合は前置きの「two images」を「three images」に書き換える
- 1つのプロンプトにつき数枚生成し、一番良いものを採用する
- 極端な比率（1:5 など）は守られない可能性が高い。守られなければ 16:9 / 9:16 / 4:3 / 1:1 で生成し、トリミングで合わせる

