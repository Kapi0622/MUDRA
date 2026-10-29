# App画面・UI 用 画像素材の生成プロンプト

画像生成AIに渡すためのまとめ。**前提・条件は日本語、プロンプト本文は英語**で書いている。
プロンプトは前置きと末尾の指定を含めた完成形にしてあり、そのまま貼れる。
書式は `stage01_environment_asset_prompts.md` に揃えている。

対象は、App側の画面（タイトル・ステージ選択・リザルト）の背景と、UIの部品（墨ワイプの縁・墨の飛沫・HPバーの枠）。
タイトルロゴは自作するので、ここには仕様だけを載せる（§4 の最後）。

---

## 1. 現状（生成AIに伝えておくこと）

- 「MUDRA」は、Webカメラで手印を組んで術を放つ和風ボス戦アクション（Unity / Windows）。和風の民俗ホラー、墨と水彩の画風
- タイトル・ステージ選択・リザルトの画面は、今は背景が黒一色。ここに背景の絵を敷き、その上に文字やボタン（UI）を重ねる
- 画面は **1920×1080 基準の16:9**。背景はゆっくり拡大・横移動させるので、少し大きめ（3840×2160）に作る
- 背景には、ゲーム側で火の粉や墨の粒のパーティクルと、光のにじみ・画面の縁を暗くする効果を重ねる。**絵そのものに強い光や霧を描き込みすぎない**
- 勝ち・負けで背景の色味を変えるのはゲーム側で行う。絵は1枚でよい
- UI部品（墨の縁・飛沫・枠）は、**白地に黒の墨で生成し、取り込むときに明るさを透明度に変える**。色はゲーム側で付ける（例: 判子の飛沫は金、敗北時は赤）

### 参考画像

既存の背景が画風の基準。背景（#1・#2）では次の2枚を添付する。

- `Assets/_MUDRA/Sprites/Background/r_SealedVillage.png`
- `Assets/_MUDRA/Sprites/Environment/Stage04/T_Env_S04_Backdrop.png`（暗い色調の基準）

UI部品（#3〜#5）は画風より形が大事なので、参考画像は添付しない。
参考画像だけだと構図まで真似されやすいので、前置きに「構図や場面は真似ない」の一文を入れてある。消さないこと。

---

## 2. 全素材に共通する条件

| 項目 | 条件 |
|---|---|
| 画風 | **水彩の塗り＋墨の線画**。くすんだ低彩度。背景は暗め（上に白い文字が乗るため） |
| 明るさ | 背景（#1・#2）は**全体に暗く、明暗差を小さく**。文字が乗る位置（#1は上部中央、#2は全体）に細かい模様や明るい点を置かない |
| 文字 | 意味のある文字・ロゴ・署名・落款を描かない |
| 枠 | 枠線・紙の四角・チェッカー柄を描かない |
| UI部品の背景 | #3〜#5 は**純白（#FFFFFF）の単色背景に、黒（#000000）の墨だけ**で描く。グレーの陰影は墨の濃淡としてのみ使う（そのまま半透明になる） |

### UI部品を白黒で作る理由

墨の擦れや飛沫は、半透明の縁がたくさんある。色付きの背景から抜くと縁が崩れるか、背景色が残る。
白地に黒で作っておけば、明るさをそのまま透明度に変えるだけで、擦れや濃淡が崩れずに残る。色はゲーム側で付けるので、1枚を金・赤・黒に使い回せる。

### 置き場所と名前

| 種類 | 置き場所 |
|---|---|
| 背景（#1・#2） | `Assets/_MUDRA/Sprites/AppBackground/`（新規フォルダ） |
| UI部品（#3〜#5）・タイトルロゴ | `Assets/_MUDRA/Sprites/UI/` |

取り込み設定（Sprite化・圧縮・9スライスの区切り）はClaude側で行う。

---

## 3. 素材一覧と生成比率

| # | 素材 | ファイル名 | 生成比率 | 画像サイズ（目安） | 生成時の背景 | 優先度 |
|---|---|---|---|---|---|---|
| 1 | タイトル背景 | `T_UI_TitleBackdrop.png` | 16:9 | 3840 × 2160 | なし（画面いっぱい） | A |
| 2 | ステージ選択・リザルト共通の背景 | `T_UI_ScreenBackdrop.png` | 16:9 | 3840 × 2160 | なし（画面いっぱい） | A |
| 3 | 墨ワイプの縁 | `InkEdge.png`（既存を上書き） | 1:4 | 512 × 2048 | 白 | A |
| 4 | 墨の飛沫 | `T_UI_InkSplash.png` | 1:1 | 1024 × 1024 | 白 | B |
| 5 | HPバーの枠 | `T_UI_HpFrame.png` | 8:1 | 2048 × 256 | 白 | B |
| 6 | 墨の帯（ボタン・攻撃ゲージの下地） | `T_UI_InkBand.png` | 4:1 | 2048 × 512 | 白 | A（追加分） |

優先度 **A** が揃えば、App側の背景と画面切替の見た目が変わる。**B** はリザルトとバトルの表示の上積み。
極端な比率（#3 の 1:4、#5 の 8:1）が守られない場合は、1:1 や 16:9 で生成してトリミングする。

---

## 4. 素材ごとのプロンプト

### 共通の前置き（背景 #1・#2）

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game background, watercolor with dark ink-wash and ink outlines, muted desaturated palette, dark and moody, low contrast overall.
```

### 共通の末尾（背景 #1・#2）

```
Do not include any text, letters, logo, signature, seal stamp, frame or border. Keep the image dark enough that white text placed on top stays easy to read. 16:9 image.
```

### 共通の前置き（UI部品 #3〜#5）

```
Traditional Japanese sumi ink brushwork on a pure white background. Use only pure black ink on pure white (#FFFFFF); any gray tones must come only from the ink itself thinning out, such as dry-brush streaks and diluted ink. No paper texture, no paper grain, no colored tint, no shading of the background.
```

### 共通の末尾（UI部品 #3〜#5）

```
Do not include any text, letters, seal stamp, signature, frame or border. The white background must be completely flat and clean.
```

---

### #1 タイトル背景 `T_UI_TitleBackdrop.png`

**条件**

- ゲームの顔になる1枚。夜の山奥、霧の中に古い鳥居が立ち、その奥に封印の気配（青白い光）がある
- **上部中央の約40%（横幅の中央60%、高さの上40%）を、ロゴを置く余白として空ける**。暗い空と雲だけにして、目立つ物を置かない
- 下部の中央にも「Spaceで開始」の文字が乗るので、暗く静かにしておく
- 主役（鳥居と光）は画面の中央やや下。左右の端は暗く沈める（ゲーム側で縁を暗くする効果も重ねる）
- 背景はゆっくり拡大・横移動させるので、端まで描き込んでおく

**プロンプト**（参考画像2枚を添付）

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game background, watercolor with dark ink-wash and ink outlines, muted desaturated palette, dark and moody, low contrast overall. A key visual for the title screen: a remote mountain shrine at night. In the lower center, an old weathered wooden torii gate stands in drifting mist, with a stone path leading up to it. Beyond the torii, deep in the misty forest, a faint pale cyan glow suggests an ancient seal. Dark ink-wash cedar trees frame the left and right edges and fade into near-black. The upper 40 percent of the image, across the central 60 percent of its width, is left as calm empty space: only a dark night sky with soft ink-wash clouds and nothing else, so a title logo can be placed there. The bottom edge is dark and quiet. Do not include any text, letters, logo, signature, seal stamp, frame or border. Keep the image dark enough that white text placed on top stays easy to read. 16:9 image.
```

---

### #2 ステージ選択・リザルト共通の背景 `T_UI_ScreenBackdrop.png`

**条件**

- 画面全体に文字・カード・ボタンが乗るので、**模様は控えめで、全体に均一に暗い**
- 暗い墨色の和紙の上に、薄い墨のにじみが流れている抽象的な背景。物（建物・木・人）は描かない
- 勝ち（金寄り）・負け（赤黒）の色味はゲーム側で付けるので、**ほぼ無彩色**にしておく（色を付けると色味の変更が濁る）
- 背景はゆっくり動かすので、端まで同じ密度で描く

**プロンプト**（参考画像2枚を添付）

```
Use the attached two images only as an art style and color reference (line quality, watercolor wash, paper texture, palette). Do not copy their composition or scene. Hand-painted Japanese folk-horror game background, watercolor with dark ink-wash and ink outlines, muted desaturated palette, dark and moody, low contrast overall. An abstract background for menu screens: dark charcoal-colored Japanese washi paper with visible soft fibers, with faint flowing sumi ink washes drifting across it like slow smoke. Almost monochrome, with only a very slight warm gray tint. The pattern is subtle and evenly spread over the whole image, with no bright spots and no focal point, and slightly darker toward the edges. Do not draw any objects, buildings, trees, people or creatures. Do not include any text, letters, logo, signature, seal stamp, frame or border. Keep the image dark enough that white text placed on top stays easy to read. 16:9 image.
```

---

### #3 墨ワイプの縁 `InkEdge.png`（既存を上書き）

**条件**

- 画面切替の墨ワイプで、黒い面の右端に付いて動く縁（`TransitionView`）。今の素材は 128×512 の仮
- **左端は完全な黒で途切れなく塗りつぶし、右へ行くほど筆が擦れて途切れる**。筆を縦に大きく一払いした右側の縁、というイメージ
- 左端は上から下まで全部黒（黒い面とつながるため。ここに隙間があると線が見える）
- 上下の端も途切れずに縁が続くこと（画面の上下いっぱいに伸ばして使う）
- 右端の約15%は白（何も無い）にしておく

**プロンプト**

```
Traditional Japanese sumi ink brushwork on a pure white background. Use only pure black ink on pure white (#FFFFFF); any gray tones must come only from the ink itself thinning out, such as dry-brush streaks and diluted ink. No paper texture, no paper grain, no colored tint, no shading of the background. A tall vertical strip showing the right-hand edge of one huge vertical brush stroke. The left side of the image is solid pure black, completely filled from the very top to the very bottom with no gaps. Moving to the right, the ink breaks up into rough dry-brush streaks, frayed bristle marks and a few small ink flecks, all running vertically. The frayed edge continues all the way from the top edge to the bottom edge of the image. The rightmost 15 percent of the image is empty white. Do not include any text, letters, seal stamp, signature, frame or border. The white background must be completely flat and clean. Tall vertical 1:4 image.
```

---

### #4 墨の飛沫 `T_UI_InkSplash.png`

**条件**

- リザルトでランクの判子が押された瞬間に、判子の後ろに広がる飛沫。ゲーム側で金色や朱色に染める
- 中央に濃い墨の塊、外へ向かって放射状に飛び散る大小の滴
- **四辺に触れない**（上下左右に10%以上の余白）。切れると四角く見える

**プロンプト**

```
Traditional Japanese sumi ink brushwork on a pure white background. Use only pure black ink on pure white (#FFFFFF); any gray tones must come only from the ink itself thinning out, such as dry-brush streaks and diluted ink. No paper texture, no paper grain, no colored tint, no shading of the background. A single dramatic ink splash seen from directly above: a dense black ink blot in the center, with irregular splatter, thin streaks and many droplets of different sizes flying outward in all directions. The whole splash is centered and stays well inside the image, leaving at least 10 percent empty white margin on every side, and nothing touches the edges. Do not include any text, letters, seal stamp, signature, frame or border. The white background must be completely flat and clean. Square 1:1 image.
```

---

### #5 HPバーの枠 `T_UI_HpFrame.png`

**条件**

- バトルのHPバー（プレイヤー・ボス共通）を囲む枠。9スライス（両端を固定し、中央を伸ばす）で使う
- **両端（左右それぞれ横幅の約12%）にだけ飾り**（巻物の軸、または木札の留め具のような形）。中央部分は上下の細い線だけで、**模様を入れない**（伸ばすと模様が歪むため）
- 枠の内側（バーが入る所）は白（何も無い）。線の太さは上下で揃える
- 左右対称

**プロンプト**

```
Traditional Japanese sumi ink brushwork on a pure white background. Use only pure black ink on pure white (#FFFFFF); any gray tones must come only from the ink itself thinning out, such as dry-brush streaks and diluted ink. No paper texture, no paper grain, no colored tint, no shading of the background. A long thin horizontal frame for a game health bar, drawn in ink, perfectly left-right symmetrical. The frame is a long empty rectangle outlined by one bold ink line along the top and one along the bottom, both of the same even thickness, and both perfectly straight and plain across the whole middle section with no decoration. Only the two ends carry ornaments: each end, about 12 percent of the width, is capped with a short vertical scroll rod or wooden tag clasp drawn in bold ink. The inside of the frame is empty white. Do not include any text, letters, seal stamp, signature, frame or border other than the described frame. The white background must be completely flat and clean. Wide 8:1 image.
```

---

### #6 墨の帯 `T_UI_InkBand.png`

**条件**

- 全ボタン（App・ポーズまわり）と敵の攻撃ゲージの下地。9スライスで**左右の端だけ固定し、中央を横に伸ばす**。色はゲーム側で付ける（主要ボタンは朱、その他は墨色）
- 太い筆で**左から右へ一払いした横長の帯**。中央部分は**濃く均一に塗りつぶす**（上に白い文字が乗るので、中にかすれや白い筋を作らない）
- **両端（左右それぞれ横幅の約10%）だけ**が、筆の入りと抜けでかすれて途切れる
- 上下の縁はわずかに波打つ程度。帯の高さは画像の高さの約60%で、上下に余白を残す
- 中央部分には模様・濃淡の変化を入れない（伸ばすと歪むため）

**プロンプト**

```
Traditional Japanese sumi ink brushwork on a pure white background. Use only pure black ink on pure white (#FFFFFF); any gray tones must come only from the ink itself thinning out, such as dry-brush streaks and diluted ink. No paper texture, no paper grain, no colored tint, no shading of the background. One single bold horizontal brush stroke made with a wide flat brush, sweeping from left to right across the image. The whole middle part of the stroke is solid, evenly filled deep black with no white streaks, no gaps and no texture inside, and its top and bottom edges are only very slightly wavy. Only the two ends, each about 10 percent of the width, show the brush entering and leaving: rough dry-brush streaks and frayed bristle marks that break up into white. The stroke is about 60 percent of the image height and is vertically centered, leaving empty white margins above and below. Do not include any text, letters, seal stamp, signature, frame or border. The white background must be completely flat and clean. Wide 4:1 image.
```

---

### タイトルロゴ（自作）`T_UI_TitleLogo.png`

生成プロンプトは載せない。ゲーム側に組み込むための仕様だけ。

| 項目 | 仕様 |
|---|---|
| 形式 | **透過PNG**（文字以外は完全な透明） |
| サイズ | 横長。目安 1600 × 600 前後（縦横比は自由。ゲーム側で比率を保って置く） |
| 色 | 暗い背景（#1）の上で読める明るい色。光のにじみはゲーム側で付けるので、ロゴ自体に外側の光彩を付けなくてよい |
| 余白 | 上下左右に数%の透明の余白（墨のにじみ演出で端が切れないように） |
| 置き場所 | `Assets/_MUDRA/Sprites/UI/` |

サブタイトルは今の TextMeshPro の文字のまま使う。

---

## 5. 受け取ったあとの処理（取り込み側への申し送り）

1. **UI部品の透明化（#3〜#5）**: 白黒の画像を、明るさから透明度に変換する（黒＝不透明、白＝透明）。色は白にしておき、ゲーム側の `Image.color` で付ける。変換はスクリプトで行える（Claude側で対応）
2. **白の残りの確認**: 生成画像の白が完全な #FFFFFF でないと、全体がうっすら残る。変換時に、ほぼ白の範囲を完全な透明に寄せる
3. **墨ワイプの縁（#3）**: 左端の列が上から下まで完全に黒か確認する。途切れていたら手で塗る。今の 128×512 から大きくなるが、`TransitionView` 側のRectTransformの幅で表示幅を決めているので、見た目の幅は別途合わせる
4. **HPバーの枠（#5）**: 取り込み時に9スライスの区切り（Sprite Border）を、左右は飾りの内側、上下は線の内側に設定する。中央に模様が出ていたら、中央部分だけ手で消す
5. **背景（#1・#2）**: 明るすぎたら、取り込み前に全体を暗くする（ゲーム側でも色味で暗くできるので、迷ったら暗めに）
6. **記録**: 使った生成ツール、生成日、プロンプトを `CREDITS.md` に残す。公開前に、生成ツールの利用規約を自分で確認する

### 新しい素材を足さないもの

- **火の粉・墨の粒のパーティクル**: 既存の `Assets/_MUDRA/Particle/T_SoftCircle.png` で作る。物足りなければ Kenney「Particle Pack」（CC0・無料）の煙や火花の画像を使う
- **勝ち・負けの背景**: #2 の色味をゲーム側で変える

---

## 6. 運用メモ

- 1素材ごとに新しいチャットで、必要な参考画像を添付してからプロンプトを貼る（会話が長いと画風がずれやすい）
- 1つのプロンプトにつき数枚生成し、一番良いものを採用する
- タイトル背景（#1）は、ロゴの余白が守られないことが多い。守られなかった場合は、ロゴ位置の物を消す編集（「Remove the objects in the upper center and replace them with dark night sky and soft clouds.」）を追加で頼む
- 墨の部品（#3〜#5）で背景に紙の質感が出てしまったら、「pure white background」を強調するより、生成後に明るさの閾値で白に飛ばすほうが早い
- 素材が揃わなくても、次のステップ（背景の仕組みづくり）は仮の画像で進められる
