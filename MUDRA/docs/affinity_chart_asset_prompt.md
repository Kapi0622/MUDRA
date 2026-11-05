# 相性図 画像素材の生成プロンプト（B8.3）

画像生成AIに渡すためのまとめ。**前提・条件は日本語、プロンプト本文は英語**で書いている。
プロンプトは完成形にしてあり、そのまま貼れる。

---

## 1. 何に使うか

- バトル画面（InGame の HUD）の右上（ボスのHPバーの右下・コンボ表示の上）に**常に表示する属性の相性図**。表示の大きさは約 170 × 170px（1920×1080 の画面で）と小さいので、**漢字は珠いっぱいに大きく**描かせる
- 相性の規則（`Model/ElementAffinity.cs`）をそのまま絵にする:
  - 5すくみの循環 **水 → 火 → 風 → 土 → 雷 → 水**（矢印の先の属性に強い）
  - **光 ⇄ 闇**（互いに弱点）
- 文字まで含めて1枚の画像にする（Unity 側は Image を1つ置くだけ）
- 3D の背景の上に重なるので、図そのものに**暗い円盤の下地**を持たせて、どのステージでも文字が読めるようにする

## 2. 条件

| 項目 | 条件 |
|---|---|
| 画風 | ステージ素材と同じ**水彩の塗り＋墨の線画**。和風。HUD なので線は太め、塗りはくっきり |
| 配置 | 正五角形の頂点に5つの珠。**一番上が水**、時計回りに 水 → 火 → 風 → 土 → 雷。隣どうしを時計回りの矢印で結ぶ |
| 中央 | 五角形の内側に、小さめに「光 ⇄ 闇」を横並びで置く（両向きの矢印） |
| 文字 | 各珠の中に漢字1字（**水・火・風・土・雷・光・闇の7字だけ**）。太い筆文字で、白か生成り色。それ以外の文字・数字・記号は描かない |
| 色 | 珠の色はゲームの属性色に合わせる（下表）。矢印は墨色か生成り色で、どの向きかがはっきり分かる太さ |
| 下地 | 全体を、暗い墨色（ほぼ黒、少し透けた和紙風）の**円盤**に載せる。円盤の外側は切り抜く |
| 背景 | 円盤の外は**薄いグレー（#D0D0D0）の単色**（切り抜き用。理由はステージ素材の文書 §2 と同じ）。透明 PNG を出せるなら透明でよい |
| 比率 | 1:1（1024 × 1024 目安） |

| 属性 | 色（`PL_Battle` の値） |
|---|---|
| 水 | `#3373E6` |
| 火 | `#E64C1A` |
| 風 | `#59CC73` |
| 土 | `#B28040` |
| 雷 | `#D9BF1A` |
| 光 | `#F2E6B2` |
| 闇 | `#8C59BF` |

## 3. プロンプト

```
A circular Japanese-style game HUD diagram showing elemental affinities, hand-painted watercolor with bold dark ink outlines, drawn on a dark, almost black ink-wash paper disc. Five round orbs are placed on the corners of a regular pentagon. The top orb is blue (#3373E6) with the kanji 水. Going clockwise: a red-orange orb (#E64C1A) with the kanji 火, a green orb (#59CC73) with the kanji 風, a brown orb (#B28040) with the kanji 土, and a yellow orb (#D9BF1A) with the kanji 雷. Thick clockwise arrows connect each orb to the next one: 水 to 火, 火 to 風, 風 to 土, 土 to 雷, and 雷 back to 水. In the center of the pentagon, smaller, a pale cream orb (#F2E6B2) with the kanji 光 and a purple orb (#8C59BF) with the kanji 闇, side by side, connected by a double-headed arrow pointing both ways between 光 and 闇. Each kanji is a single bold brush-stroke character in off-white, large and clearly legible. No other text, letters, numbers or symbols anywhere. Flat front view, no perspective, no drop shadow. Outside the disc, the background must be one flat solid light gray (#D0D0D0) color with no gradient, no paper texture and no checkerboard pattern.
```

## 4. 届いたら確認すること

1. **7字が正しい字か。** 画像生成AIは漢字を崩しやすい。1字でも崩れていたら、文字を消した版を作り直し、文字は Unity の TMP で上に重ねる方式に切り替える（計画で決めた予備の手）
2. **矢印の向き。** 水→火→風→土→雷→水 の時計回りと、光⇄闇の両向き。循環に逆向きの矢印が混ざっていないか
3. 円盤の縁がくっきりしていて、切り抜いたときにグレーが残らないか

## 5. 置き場所と名前

- `Assets/_MUDRA/Sprites/UI/T_UI_AffinityChart.png`（取り込み設定と HUD への割り当ては Claude 側で行う）
- HUD の `AffinityChart`（`BattleCanvas` 直下の Image）に割り当てる
