# Stage01「封土の里」 3D環境用 画像素材の生成プロンプト（v2・目標比率で直接生成する試験版）

画像生成AIに渡すためのまとめ。**前提・条件は日本語、プロンプト本文は英語**で書いている。
プロンプトは前置きと末尾の指定を含めた完成形にしてあり、そのまま貼れる。

> **この版は、最終的に使う比率（18:5、1:4、5:1など）をそのまま生成比率に指定する試験版。**
> 生成AIが極端な比率を守らない場合は、16:9 / 9:16 / 4:3 / 1:1 で生成してトリミングする `stage01_environment_asset_prompts.md` に切り替える。
>
> v1からの主な変更点
> - 生成比率を、最終的に使う比率にそのまま指定した（比率が守られるかは未確認）
> - 否定タグの羅列をやめ、文章での指示に変えた
> - 切り抜き素材の背景を、薄いグレー単色で標準化した（マゼンタは縁に色が残るため取りやめ）
> - 切り抜きでハローが残る原因になる「縁の外へ漏れる光や霧」を、祠と竹林の帯から外した
> - 遠景（#8）は、既存の絵から手前を消す編集型を第一候補にした
> - 継ぎ目なしの素材は、生成だけで保証せず後処理で直す前提にした

---

## 1. 現状（生成AIに伝えておくこと）

- 「MUDRA」は、Webカメラで手印を組んで術を放つ和風ボス戦アクション（Unity / Windows）
- バトル画面を2Dから **2.5D（3D空間に「板」を立てて絵を貼る、舞台の書き割りのような表現）** に移行中。HD-2D に近い方向性。立体モデルではなく、**平面の絵を板に貼って3D空間に並べる**
- 今回はステージ1「封土の里」の素材。土の一本道を奥へ進み、最後に開けた広場でボス（泥人形）と戦う
  - 道中: 両側に崩れかけた土塀（瓦屋根つき）、注連縄で結ばれた苔むした石柱、古い木の鳥居。奥に青白く光る竹林と山
  - ボス広場: 崩れた小さな祠、瓦礫、札と縄の巻かれた木の柱
- 3D空間の形は、単色の箱で仮組み済み。届いた素材を箱と差し替える
- **ゲーム内ではライティングを使わない（Unlit）。** 光と影は絵に描かれたものがそのまま出る。素材ごとに光の向きがバラバラだと、並べたときに目立つ
- 前進するカメラに対して、遠景（#8）と中景の竹林の帯（#8b）はずれて動き、奥行きを出す

### 参考画像（毎回2枚とも添付する）

既存の背景2枚が画風の基準。この2枚と同じ画風・色調で揃える。

- `Assets/_MUDRA/Sprites/Background/r_SealedVillage.png`（道中）
- `Assets/_MUDRA/Sprites/Background/b_SealedVillage.png`（ボス広場）

例外は#8の編集型で、`r_SealedVillage.png`の1枚だけを添付する。
参考画像だけだと構図まで真似されやすいので、プロンプトの前置きに「構図や場面は真似ない」の一文を入れてある。消さないこと。

---

## 2. 全素材に共通する条件

| 項目 | 条件 |
|---|---|
| 画風 | 参考画像と同じ。**水彩の塗り＋墨の線画**。くすんだ低彩度、暗い曇天の空気感。土塀は黄土色、苔は暗い緑、木は焦げ茶 |
| 光 | **曇天の柔らかい光、正面やや上から**。強い影・はっきりした落ち影は描かない（Unlitなので、絵の影がそのまま残る）。青白い光を絵に焼き込んでよいのは、遠景（#8）の竹林と、祠（#5）の開口部の内側だけ |
| 視点 | **真正面（または真横）から見た平面図**。パース（遠近法）を付けない。斜め上からの視点にしない。パースは3D空間側で付く。例外は祠（#5）の軽い斜めと、地面（#7）の真上視点 |
| 背景 | 切り抜く素材は**薄いグレー（#D0D0D0）の単色**。紙の質感、紙の四角、枠、チェッカー柄は付けない。地面（#7）と遠景（#8）は背景なしで画面いっぱいに描く |
| 地面 | 物の下に地面や床、落ち影を描かない（3Dの床に立てるため。描くと浮いて見える） |
| 輪郭 | 透明部分との境目をくっきり。半透明のにじみ、白い縁取り、**シルエットの外へはみ出す光や霧**を描かない |
| 文字 | 札や石柱の文字は、読めないかすれた墨の筆跡程度にする（意味のある文字にしない） |
| 構図 | 対象を画像の中央に置き、上下左右に余白を取る。ただし左右につなげる素材（#1、#3）は、左右の端まで通す |

### 背景色を薄いグレーにする理由

マゼンタ背景は、水彩の半透明の縁にピンクや紫が混ざって残るため使わない。単色の薄いグレーなら、縁に混ざっても目立ちにくい。

透過風のチェッカー柄は、柄そのものが画像のピクセルとして描かれてしまい、切り抜きの邪魔になるので使わない。

ただし、石柱や瓦は灰色系、紙垂は白なので、**色キーでは抜けない。AIの背景除去か、手作業の選択で抜く。** 太い墨の輪郭が境目になるので、輪郭を太くする指示を全素材に入れてある。

生成AIが透明PNGを出せる場合は、末尾の「The background must be ...」の一文を `The background must be fully transparent.` に置き換えてよい。ただし、半透明の縁のにじみは別途確認する。

### 置き場所と名前

- 届いた画像は `Assets/_MUDRA/Sprites/Environment/Stage01/` に置く（取り込み設定はClaude側で行う）
- ファイル名は下の表の通り。英語（ASCII）の `プレフィックス_PascalCase`（仕様書3-2の命名規則に合わせる）

---

## 3. 素材一覧と生成比率

この版では、**生成比率と最終比率を同じにする**。ただし、極端な縦横比（18:5、5:1、1:4など）を生成AIが守るかは未確認。守られなかった素材は、16:9版の手順に切り替える。
Unity側では、実際に届いた画像の縦横比から板の幅を決め、高さは3D側の寸法を基準にする。画像サイズ（ピクセル数）は目安で、指定どおりになるとは限らない。足りない分は後から拡大する。

「3Dでの大きさ」はゲーム内の寸法（1ユニット≒1m）。

| # | 素材 | ファイル名 | 3Dでの大きさ（幅×高さ） | 生成比率 | 画像サイズ（目安） | 透明 | 優先度 |
|---|---|---|---|---|---|---|---|
| 1 | 土塀（瓦屋根つき） | `T_Env_S01_MudWall.png` | 12 × 3.3（左右につなげる） | 18:5 | 2048 × 560 | ○ | A |
| 1b | 土塀の崩れた端 | `T_Env_S01_MudWallBroken.png` | 3 × 3.3 | 10:11 | 1024 × 1120 | ○ | B |
| 2 | 石柱 | `T_Env_S01_StonePost.png` | 0.5 × 1.9 | 1:4 | 320 × 1216 | ○ | A |
| 3 | 注連縄＋紙垂 | `T_Env_S01_Shimenawa.png` | 6 × 1.2 | 5:1 | 2048 × 410 | ○ | A |
| 4 | 鳥居 | `T_Env_S01_Torii.png` | 9.4 × 6.7 | 7:5 | 2048 × 1460 | ○ | A |
| 5 | 崩れた祠 | `T_Env_S01_RuinedShrine.png` | 6 × 4.5 | 4:3 | 2048 × 1536 | ○ | A |
| 6a | 瓦礫の山 | `T_Env_S01_Rubble.png` | 3 × 1.2 | 5:2 | 1536 × 614 | ○ | B |
| 6b | 札と縄の巻かれた木の柱 | `T_Env_S01_TalismanPost.png` | 0.6 × 3.2 | 1:5 | 384 × 2048 | ○ | B |
| 7 | 土の地面（真上から） | `T_Env_S01_Dirt.png` | 4 × 4 を敷き詰める | 1:1 | 1024 × 1024 | × | A |
| 8 | 遠景（空・山・光る竹林） | `T_Env_S01_Backdrop.png` | 画面全体 | 16:9 | 3840 × 2160 | × | A |
| 8b | 竹林の帯（中景） | `T_Env_S01_BambooGrove.png` | 24 × 12 | 2:1 | 2048 × 1024 | ○ | B |

優先度 **A** が揃えば、仮の箱をほぼ全部置き換えられる。**B** は雰囲気の上積み。

---

## 4. 素材ごとのプロンプト

すべてのプロンプトは、次の3つをつないだ完成形になっている。

1. 前置き（画風の指定と、参考画像の使い方）
2. 素材ごとの本文
3. 末尾（背景色と、はみ出し禁止の指定）

画風や末尾の指定を直したくなったら、全素材で同じ文言に揃えて直すこと。

### 共通の前置き

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows.
```

### 共通の末尾（切り抜き素材）

```
The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

祠（#5）と竹林の帯（#8b）は、最後の「No perspective distortion.」を外した版を使っている。祠は軽い斜めがあるため。

### #1 土塀（瓦屋根つき） `T_Env_S01_MudWall.png`

**条件**

- **真横から見た立面図**。道の両脇に、道と平行に立てる
- **左右につなげて繰り返す**ので、左端と右端が同じ絵柄でつながること。端に柱や角を描かない。端に目立つ崩れや大きな染みを置かない
- 上に黒っぽい瓦屋根が載る。下端は塀の足元（地面は描かない）
- ひび割れ、剥がれ、苔、雨だれの染みで古びた感じに。ひびや染みは全体に均等に散らし、特定の場所に集中させない
- 生成比率は18:5。塀を画像いっぱいに、左右の端まで通す

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. A straight section of a rustic Japanese village mud wall (dozobei) with a dark gray clay tile roof cap on top. Strict orthographic side elevation, the camera is exactly perpendicular to the wall, flat front-on view with no perspective and no vanishing point. Weathered ochre packed-earth surface with small evenly spread cracks, a few peeling patches, patches of dark moss and rain stains. The wall runs straight past both the left and right edges of the image at the same height across the whole width, with no pillars, no corners, no end caps, and nothing unique or eye-catching near the left and right edges, so it can be repeated horizontally. The wall fills the whole image from the left edge to the right edge, with the roof near the top edge and the wall's lower edge near the bottom edge. Very wide 18:5 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #1b 土塀の崩れた端 `T_Env_S01_MudWallBroken.png`

**条件**

- #1と同じ塀の、途中で崩れて途切れている端。左側は#1につながる形、右側が崩れて落ちている
- 塀の隙間（崩れた所）の脇に置く
- 塀の高さ、屋根、色は#1と揃える。生成比率は10:11

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. The broken end of a rustic Japanese village mud wall (dozobei) with a dark gray clay tile roof cap. Strict orthographic side elevation, flat front-on view with no perspective. The left side is the intact wall continuing past the left edge of the image at full height, and the right side has crumbled and collapsed, with jagged broken earth, exposed inner clay and a few fallen roof tiles. Ochre packed earth with dark moss. Slightly tall 10:11 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #2 石柱 `T_Env_S01_StonePost.png`

**条件**

- 道沿いに並べる、苔むした石の柱1本。**正面から**。縄は描かない（縄は#3を別に渡す）
- 上端が丸みを帯びた角柱。かすれた彫りがある程度で、読める文字にはしない
- 生成比率は1:4の縦長
- 並べても同じに見えないよう、2〜3本の派生を作る（後述の派生プロンプト）

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. A single weathered stone pillar marker, a tall narrow square post with a rounded top, gray-green stone covered in moss and lichen, with faint worn carvings and no readable characters. Strict front view, no rope and no paper attached. The bottom of the pillar is cut off flat, with no base ground. Very tall narrow 1:4 portrait image with the pillar centered and filling most of the height. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #3 注連縄＋紙垂 `T_Env_S01_Shimenawa.png`

**条件**

- 石柱と石柱の間（6ユニット）に渡す、縄1区間。**正面から**
- 縄の**左端と右端は、画像の左右の端の同じ高さ**に来る。真ん中が少したるむ
- 縄から白い紙垂（ギザギザの紙）が3〜4本垂れ下がる。紙は少し汚れて古びている。白い紙は背景除去で消えやすいので、輪郭の墨線を太くする
- 生成比率は5:1。縄を左右の端まで通す

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. A single span of thick twisted straw shimenawa rope hanging between two points. The rope starts at the left edge of the image and ends at the right edge at exactly the same height, sagging gently in the middle. Three or four zigzag white paper shide streamers and one small tassel hang from the rope, slightly dirty and aged, with bold dark ink outlines around every white paper streamer so they stay clearly readable. Strict front view. Very wide 5:1 aspect ratio image, the rope runs across the upper part of the image from edge to edge with the streamers hanging below it. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #4 鳥居 `T_Env_S01_Torii.png`

**条件**

- 古びた木の鳥居。**真正面から**（左右対称）。道をまたいで立て、カメラがくぐる
- 朱塗りではなく、塗りの剥げた焦げ茶の木（参考画像の鳥居と同じ）
- 上の横木（笠木）は反りのある形。下の横木（貫）から注連縄と紙垂が垂れる
- 柱の下端は水平に切り落とし、地面は描かない。生成比率は7:5

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. An old weathered wooden torii gate seen exactly from the front, perfectly symmetrical. Dark brown bare wood with remnants of peeling paint, a curved upper lintel (kasagi) and a lower crossbeam (nuki), moss on the beams. A thick straw shimenawa rope with white zigzag shide streamers, each outlined in bold dark ink, hangs under the lower beam. The area between the pillars and under the beams is empty background. The bottoms of both pillars are cut off flat at the same height, with no ground. Flat front elevation. 7:5 aspect ratio image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #5 崩れた祠 `T_Env_S01_RuinedShrine.png`

**条件**

- ボス広場の奥に置く、崩れかけた小さな祠（参考画像b_SealedVillageの右側の建物）
- 正面からやや斜め。ここだけ少し角度があってよい。ただし見下ろしにはしない
- 瓦屋根が傾いて崩れ、土壁に穴、折れた柱、札が貼られている
- 青白い光は、**壁の穴や開口部の内側だけ**に描く。建物の輪郭の外へ光を漏らさない（切り抜きでハローが残るため）。生成は4:3

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. A small collapsed Japanese roadside shrine (hokora), seen at eye level from the front and rotated about 20 degrees to one side, with only mild perspective and never viewed from above. Sagging broken clay tile roof, crumbling mud-brick walls with holes, a cracked wooden pillar, and old paper talismans (ofuda) with illegible faded brush marks stuck on it. A faint eerie pale cyan glow is visible only inside the dark broken openings of the walls, painted as part of the interior and fully contained inside the building's outline. Ruined and abandoned. 4:3 image. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border.
```

### #6a 瓦礫の山 `T_Env_S01_Rubble.png`

**条件**

- 広場に散らす瓦礫。崩れた日干しレンガ、割れた瓦、折れた板
- **正面から**、低く横長の山。下端は水平に切る
- 生成比率は5:2

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. A low wide pile of rubble from a collapsed building: broken mud bricks, shattered clay roof tiles and snapped wooden planks. Front view at eye level, low wide mound shape, the bottom edge cut off flat. Wide 5:2 aspect ratio image with the mound filling most of the width. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #6b 札と縄の巻かれた木の柱 `T_Env_S01_TalismanPost.png`

**条件**

- 広場に立つ、封印の札と縄が巻かれた古い木の柱。**正面から**。少し傾いていてもよい
- 札は読めないかすれた筆跡にし、白い紙には太い墨の輪郭を付ける
- 生成比率は1:5の縦長

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. A tall old wooden post wrapped with straw rope and covered in paper talismans (ofuda) with faded illegible brush marks, cracked weathered wood, standing slightly leaning. Bold dark ink outlines around each paper talisman. Strict front view, the bottom end cut off flat. Very tall narrow 1:5 portrait image with the post centered and filling most of the height. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border. No perspective distortion.
```

### #7 土の地面 `T_Env_S01_Dirt.png`

**条件**

- 床に敷き詰める地面。**真上から見た図**（ここだけ真上視点）
- 上下左右につなげて繰り返す。大きな石、轍、草の房など目立つ特徴を入れない（繰り返しが見えるため）。草は小物側で足す
- 透明は不要。単色背景も使わない。生成は1:1

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. A seamless tileable texture of a packed dirt village road seen straight from above, flat top-down orthographic view with no horizon and no perspective, filling the whole square image edge to edge. Muted ochre and brown earth with subtle watercolor mottling, tiny pebbles and faint hairline cracks. Low contrast, evenly distributed detail, no large or distinctive features, nothing important near the edges, so the image can repeat in all four directions. Do not include grass, puddles, tracks, shadows or any objects. Square 1:1 image.
```

### #8 遠景（空・山・光る竹林） `T_Env_S01_Backdrop.png`

**条件**

- 一番奥の背景幕。参考画像から手前の建物、塀、道、鳥居、石柱を取り除いた版
- 構成は、上から曇天の空、墨絵の山並み、中央奥に青白く光る竹林の入口
- 画面の下45%は手前の3Dの床で隠れるので、霧で灰色にぼかしてよい
- **新規生成より編集のほうが確実。** 案A（編集）を先に試し、崩れたら案B（新規）にする
- 透明は不要。16:9。この素材だけ、添付する参考画像は`r_SealedVillage.png`の1枚

**案A（編集。まずこちら）** 添付は `r_SealedVillage.png` の1枚だけ

```
Edit the attached image. Remove every foreground and midground object: the earthen walls with roofs, the stone pillars, the shimenawa ropes, the torii gate, the fence posts and the dirt road. Fill the freed area by naturally extending the existing dark stormy sky, the layered misty mountains, and the bamboo grove with its faint pale cyan glow in the center gap. The lower 45 percent of the image becomes soft flat gray fog that fades smoothly, with no ground details. Keep exactly the same art style, colors, line quality and 16:9 aspect ratio. Do not include buildings, walls, roads, torii gates or people. Do not add any text.
```

**案B（新規生成。案Aが崩れたとき）** 添付は参考画像2枚

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. A distant background matte painting: an overcast stormy gray sky with heavy ink-wash clouds, layered misty ink-wash mountains, and in the center a dense bamboo forest with an eerie pale cyan glow coming from deep inside it. The base of the bamboo forest sits at about 55 percent down from the top of the image, and the lower part fades into flat gray fog with no ground details. Do not include buildings, walls, roads, torii gates or any foreground objects. 16:9 image.
```

### #8b 竹林の帯（中景） `T_Env_S01_BambooGrove.png`

**条件**

- 遠景（#8）と手前の間に立てる竹林。前進したときに遠景とずれて動き、奥行きが出る
- **正面から**、横長。竹の根元は帯の下端、上は葉が茂る
- 竹と竹の間に**広めの隙間**を空け、隙間が背景色（薄いグレー）できれいに抜けるようにする
- 光や霧は描かない。青白い光は遠景（#8）の竹林にあり、隙間から透けて見える設計にする
- 生成比率は2:1

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game asset, watercolor with bold dark ink outlines of even thickness, muted desaturated palette, overcast gloomy mood, soft even flat lighting from the front, no strong shadows. A wide band of tall bamboo stalks seen exactly from the front at eye level, gray-green stalks with dark ink outlines and leaf clusters at the top. The stalks are spaced widely with clear empty gaps of background between them so each gap can be cut out cleanly. The stalk bases are cut off flat along the bottom of the band. Do not draw any glow, mist or fog. Wide 2:1 aspect ratio image, the band fills the image from the left edge to the right edge. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern. Do not draw any ground, cast shadow, mist or glow outside the object's silhouette. Do not include text, a frame or a border.
```

---

## 5. 派生の作り方

同じ素材が並ぶと繰り返しが目立つので、石柱は2〜3本、土塀は3種類を用意する。完成画像を1枚目に添付し、参考画像2枚を続けて添付してから、次を貼る。

### 土塀の派生

```
The attached first image is the base wall. Create a variant with exactly the same height, roof, colors, line quality and style, but with a different placement of cracks, peeling patches and moss. Keep the wall running past both the left and right edges with nothing unique near the edges. Keep exactly the same aspect ratio as the base image. The attached second and third images are only art style references, do not copy their composition. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern.
```

### 石柱の派生

```
The attached first image is the base stone pillar. Create a variant in exactly the same style and colors, but with a different height, a chipped top and a different moss pattern. Keep the strict front view with no rope. Keep exactly the same aspect ratio as the base image. The attached second and third images are only art style references, do not copy their composition. The background must be one flat solid light gray (#D0D0D0) color filling the whole image with no gradient, no paper texture, no paper sheet and no checkerboard pattern.
```

---

## 6. 受け取ったあとの処理（取り込み側への申し送り）

1. **切り抜き**: AIの背景除去で抜き、崩れた箇所は手作業で直す。灰色系の石や瓦、白い紙垂は背景色と近いので、色キーは使えない。切り抜き後は、暗い背景と明るい背景の両方に置いて、縁に灰色の残りがないかを確認する
2. **トリミング**: 比率が指定どおり出た素材は、余白を軽く詰める程度でよい。比率が守られなかった素材は、対象の外接矩形でトリミングして目標比率に合わせる。左右につなげる素材（#1、#3）は、左右の端を残したまま上下だけ詰める
3. **土塀の継ぎ目**: 半幅ずらして継ぎ目を中央に寄せ、境目を混ぜて直す（スクリプト化できる）。3種類を交互に並べ、継ぎ目は石柱や崩れた端（#1b）で隠す
4. **地面の継ぎ目**: 四辺とも同様にオフセットして直す。別案として、TextureのWrap Modeを`Mirror`にすると境目は必ずつながるが、左右対称の繰り返しが見えるので、実際の画面で確認する
5. **竹林の帯の抜き**: 竹の隙間はAIの背景除去で崩れやすい。隙間を広く取る指示にしてあるので、まずAIの背景除去を試し、崩れた隙間だけ手作業で抜く
6. **仮の板で通しの確認**: 全素材を仮の板に貼り、ゲーム画面のスクリーンショットで色、明るさ、線の太さのばらつきを確認する。ずれた素材だけ作り直す
7. **記録**: 使った生成ツール、生成日、プロンプトを `CREDITS.md` に残す。公開前に、生成ツールの利用規約を自分で確認する

うまく生成できない素材は、無理に揃えなくてよい。その素材だけ仮の箱のまま残して先に進められる。

---

## 7. 運用メモ

- 1素材ごとに新しいチャットで、参考画像2枚を添付してからプロンプトを貼る（推奨。会話が長いと画風がずれやすい）
- 1つのプロンプトにつき数枚生成し、一番良いものを採用する
- 生成AIの現行仕様（出力比率、透過、解像度、枚数制限）は未確認。最初に土塀（#1）を1枚作り、比率と背景色が指定どおりに出るかを確認してから量産する
- 最初の1枚で画風が参考画像からずれたときは、プロンプトを書き換える前に、参考画像を添付し直す
- 極端な比率（#1の18:5、#3の5:1、#2の1:4、#6bの1:5）は、守られない可能性が高い。守られない素材が多ければ、16:9版（`stage01_environment_asset_prompts.md`）に切り替える。一部だけ守られないなら、その素材だけ16:9版のプロンプトを使えばよい
