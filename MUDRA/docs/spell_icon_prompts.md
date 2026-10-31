# 術アイコン 用 画像素材の生成プロンプト

図鑑（術タブ）の一覧と詳細に出す、術11種のアイコン。`SpellData.icon` に割り当てる。

## 1. 現状（生成AIに伝えておくこと）

- 「MUDRA」は、Webカメラで手印を組んで術を放つ和風ボス戦アクション（Unity / Windows）。和風の民俗ホラー、墨と水彩の画風
- アイコンは図鑑の一覧（小さく、約64px）と詳細（大きく、約256px）の両方に出す。**小さくしても形が分かる、太く単純な図柄**にする
- **白地に黒の墨で生成する**。図鑑では**黒い墨のまま、円い和紙の台紙（§7）に載せて**出す（属性色には染めない）
  - 当初は白い墨に変えて属性色で染める案だったが、取り込んだ絵をそのまま使うため台紙方式にした

## 2. 全素材に共通する条件

| 項目 | 条件 |
|---|---|
| 画風 | 墨の筆描き。太い筆の一筆書きに近い、勢いのある図柄 |
| 構図 | 円形の墨の輪（円相）の中に、術のモチーフを1つ。**四辺に触れない**（上下左右に10%以上の余白） |
| 色 | **純白（#FFFFFF）の背景に黒（#000000）の墨だけ**。グレーは墨の擦れ・薄墨としてのみ |
| 文字 | 文字・漢字・落款・署名を描かない（術名はゲーム側で出す） |
| 比率・サイズ | 1:1、1024 × 1024 |

### 置き場所と名前

`Assets/_MUDRA/Sprites/UI/SpellIcon/T_UI_SpellIcon_<アセット名>.png`（例: `T_UI_SpellIcon_Hellfire.png`）。取り込み設定（Sprite化・透明化）は Claude 側で行う。

## 3. 素材一覧

| # | 術 | アセット | 属性 | モチーフ | 優先度 |
|---|---|---|---|---|---|
| 1 | 風刃 | WindBlade | 風 | 三日月形の風の刃1枚 | A |
| 2 | 双刃 | CrossBlade | 土 | ×字に交差する2本の刃 | A |
| 3 | 水爪 | WaterCrow | 水 | 3本の鉤爪の引っかき跡（水しぶき付き） | A |
| 4 | 火炎弾 | Fireball | 火 | 尾を引く火の玉 | A |
| 5 | 光輪 | Halo | 光 | 光を放つ輪 | A |
| 6 | 雷連撃 | Lightning | 雷 | 枝分かれしたジグザグの稲妻 | A |
| 7 | 氷風 | IceWind | 風 | 渦を巻く風に氷の結晶 | A |
| 8 | 岩砕 | RockShatter | 土 | ひび割れて砕ける岩 | A |
| 9 | 破魔光 | PurifyingLight | 光 | 中心から四方に射す光芒 | A |
| 10 | 業火 | Hellfire | 火 | 大きく燃え上がる炎の柱 | A |
| 11 | 回復術 | Heal | 土 | 芽吹く若葉と、それを包む掌の形の曲線 | A |

同じ属性の2つ（風刃と氷風、火炎弾と業火など）は**シルエットがはっきり違う**ことを優先する（同じ色に染まるので、形でしか見分けられない）。

## 4. 素材ごとのプロンプト

### 共通の前置き

```
Traditional Japanese sumi ink brushwork on a pure white background, designed as a game spell icon. Use only pure black ink on pure white (#FFFFFF); any gray tones must come only from the ink itself thinning out, such as dry-brush streaks and diluted ink. No paper texture, no colored tint, no shading of the background. Bold, simple brush shapes that stay readable when the image is shrunk to 64 pixels. A single enso-like circular ink brush ring frames the motif in the center.
```

### 共通の末尾

```
The whole design is centered and stays well inside the image, leaving at least 10 percent empty white margin on every side, and nothing touches the edges. Do not include any text, letters, kanji, seal stamp, signature or frame other than the brush ring. The white background must be completely flat and clean. Square 1:1 image.
```

各プロンプトは **前置き＋モチーフ文＋末尾** をつないで貼る。下はモチーフ文だけ。

### #1 風刃 `T_UI_SpellIcon_WindBlade.png`

```
Inside the ring: a single sharp crescent-shaped blade of wind, drawn as one fast sweeping brush stroke, with a few thin speed streaks trailing behind it.
```

### #2 双刃 `T_UI_SpellIcon_CrossBlade.png`

```
Inside the ring: two straight short blades crossing each other in an X shape, drawn with heavy angular brush strokes, with small rock chips scattering from the crossing point.
```

### #3 水爪 `T_UI_SpellIcon_WaterCrow.png`

```
Inside the ring: three parallel curved claw slash marks running diagonally, each ending in splashing water droplets.
```

### #4 火炎弾 `T_UI_SpellIcon_Fireball.png`

```
Inside the ring: a round ball of fire flying diagonally, with a flickering flame tail trailing behind it.
```

### #5 光輪 `T_UI_SpellIcon_Halo.png`

```
Inside the ring: a smaller glowing ring of light floating in the center, with short soft rays radiating outward from it. The inner ring is clearly separate from the outer brush ring.
```

### #6 雷連撃 `T_UI_SpellIcon_Lightning.png`

```
Inside the ring: a jagged forked lightning bolt striking from top to bottom, splitting into several sharp zigzag branches.
```

### #7 氷風 `T_UI_SpellIcon_IceWind.png`

```
Inside the ring: a swirling spiral of wind with a few sharp six-pointed ice crystals caught in the swirl.
```

### #8 岩砕 `T_UI_SpellIcon_RockShatter.png`

```
Inside the ring: a heavy boulder cracking apart in the middle, with angular rock fragments bursting outward.
```

### #9 破魔光 `T_UI_SpellIcon_PurifyingLight.png`

```
Inside the ring: a bright point in the center with long straight rays of light shooting out in eight directions, like a sunburst, drawn with crisp straight brush strokes.
```

### #10 業火 `T_UI_SpellIcon_Hellfire.png`

```
Inside the ring: a towering column of roaring flames rising from the bottom, with large licking tongues of fire, filling most of the ring.
```

### #11 回復術 `T_UI_SpellIcon_Heal.png`

```
Inside the ring: a small sprouting seedling with two young leaves, gently cupped by a soft curved brush line shaped like an open palm beneath it.
```

## 5. 受け取ったあとの処理（取り込み側への申し送り）

1. **白地の除去**: 白地を透明にする（墨は黒のまま）。台紙が和紙の質感になると、白地が残った絵は台紙の上に白い円として浮く
   - 2026-11 時点で白地が残っているのは **双刃・火炎弾・光輪・水爪** の4枚。残り7枚は透過済み
2. **取り込み設定**: Sprite (2D and UI)、**Sprite Mode は Single**（Multiple だと飛沫ごとに自動で切り分けられる）、Max Size 512、圧縮は既存のUI部品に合わせる
3. **割り当て**: 各 `SpellData.icon` に設定（Claude が MCP で行う）
4. **記録**: 使った生成ツール、生成日、プロンプトを `CREDITS.md` に残す

## 6. 運用メモ

- 11枚は**同じチャットで続けて**生成したほうが筆致がそろいやすい。ずれてきたら、良かった1枚を添付して「この筆致に合わせて」と足す
- 外側の輪が描かれない・二重になる場合は、輪をあきらめてモチーフだけで生成し、輪はゲーム側の枠画像で付けてもよい

## 7. 台紙（和紙） `T_UI_IconPlate.png`

図鑑で術アイコンの後ろに敷く、円い和紙。一覧の行（約64px）と詳細（約360px）の両方で使う。
**2026-11 取り込み済み**（黒地を Claude 側で透明化して上書きした）。

### 条件

| 項目 | 条件 |
|---|---|
| 形 | **円**。縁は手漉き和紙の**耳（ちぎれたような毛羽立ち）**。円の外側は全部背景 |
| 紙 | やや黄みの生成り色（目安 `#EDE3CC`）。楮の繊維がうっすら見える程度。**中央は無地に近く、明暗差を小さく**（上に黒い墨のアイコンが乗るため） |
| 描かないもの | 墨・文字・判子・染み・焦げ・折り目・影・立体感（台紙は平らな紙として見せる） |
| 背景 | **純黒（#000000）の単色**。紙が明るいので、黒地から明るさで切り抜く |
| 比率・サイズ | 1:1、1024 × 1024。円は四辺に触れない（上下左右に5〜8%の余白） |

### プロンプト

```
A single round sheet of handmade Japanese washi paper, seen flat from directly above, centered on a pure solid black background (#000000). The paper is a soft warm off-white (around #EDE3CC) with faint, subtle kozo fibers visible in the texture. The edge of the circle is a natural deckled, slightly torn and feathery fiber edge, like the edge of handmade paper. The center of the paper is calm and nearly plain, with very low contrast, so that black ink artwork placed on top stays easy to read. Completely flat: no shadow, no folds, no curl, no lighting gradient, no stains, no ink, no text, no seal stamp. The circle stays well inside the image with about 6 percent empty black margin on every side, and nothing touches the edges. Square 1:1 image.
```

### 受け取ったあとの処理

1. **切り抜き**: 黒地を透明にする（明るさから透明度に。毛羽立ちの半透明は残す）。Claude 側で対応できる
2. **取り込み設定**: Sprite (2D and UI)、Sprite Mode は Single、Max Size 512。置き場所は `Assets/_MUDRA/Sprites/UI/`
3. **差し替え**: 図鑑の `IconPlate`（一覧の行）と `ImagePlate`（詳細）の Image の Sprite をこれにし、色は白
4. アイコンは台紙の 86% の大きさ（四辺 7% 内側）に置き、毛羽立ちの縁で輪が欠けないようにしている
5. 白地が残ったアイコン（双刃・火炎弾・光輪・水爪）は、`CodexImagePlate._imageTint`（和紙の平均の色）を掛けて白を和紙の色になじませている。白地を抜いた画像に差し替えれば、この色掛けは不要（白のままでよい）
