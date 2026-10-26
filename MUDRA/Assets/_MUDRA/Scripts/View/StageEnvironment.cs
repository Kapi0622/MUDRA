using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D環境Prefabのルートに付ける部品（3D化ステップ2b）。セクションごとのマーカーを持つ。
/// マーカーは「そのセクションで敵が立つ位置と向き」。StageEnvironmentViewが戦闘一式（リグ）をここへ動かす。
/// マーカーの数はステージのセクション数に揃える。足りない分は最後のマーカーが使われる。
/// 床はマーカーのy − 1.5 に置く（EnemyViewの足元の影がこの高さにあるため）。
///
/// セクションごとの空気（霧）も持てる（Stage02〜04で追加）。水中に潜る・闇が深まる、のように
/// 進むにつれて空気が変わるステージのため。前進中は前後のセクションの値が補間される。
///
/// StageData.environmentPrefabに設定する。未設定のステージは背景画像から自動で廊下が組まれるので、この部品は要らない。
/// </summary>
public class StageEnvironment : MonoBehaviour
{
    /// <summary>
    /// 1セクション分の霧。距離はカメラからのワールド単位（線形の霧）。
    /// URP/Unlitの板と床は霞むが、Sprite（敵・遠景の幕）は霞まないので、敵の見やすさは保たれる。
    /// </summary>
    [Serializable]
    public struct SectionAtmosphere
    {
        [Tooltip("このセクションで霧を出すか")]
        public bool useFog;
        [Tooltip("霧の色。遠景の幕の霞の色に合わせると、板と幕の境目がなじむ")]
        public Color fogColor;
        [Tooltip("霧がかかり始める距離")]
        public float fogStart;
        [Tooltip("霧の色に完全に溶ける距離")]
        public float fogEnd;
        [Tooltip("遠景の幕に掛ける色。αが0なら色を変えない（白のまま）。\n" +
                 "幕はSpriteで霧に溶けないので、闇に沈める・水中で暗くするなどはこの色で行う")]
        public Color backdropTint;

        /// <summary>幕に実際に掛ける色。未設定（α=0）なら白（そのまま）</summary>
        public Color EffectiveBackdropTint => backdropTint.a > 0f ? new Color(backdropTint.r, backdropTint.g, backdropTint.b, 1f) : Color.white;
    }

    [Tooltip("セクション順のマーカー。リグ（カメラ・敵）がこの位置と向きへ移動する")]
    [SerializeField] private Transform[] _sectionAnchors;

    [Tooltip("セクション順の霧。空なら霧なし。マーカーより少なければ最後の値を使う")]
    [SerializeField] private SectionAtmosphere[] _sectionAtmospheres;

    [Tooltip("SectionAtmosphere.backdropTint を掛ける遠景の幕")]
    [SerializeField] private SpriteRenderer[] _backdrops;

    /// <summary>色を変える遠景の幕</summary>
    public IReadOnlyList<SpriteRenderer> Backdrops => _backdrops ?? Array.Empty<SpriteRenderer>();

    /// <summary>マーカーの数</summary>
    public int AnchorCount => _sectionAnchors != null ? _sectionAnchors.Length : 0;

    /// <summary>index番目のマーカー。範囲は呼び出し側で確かめる</summary>
    public Transform GetAnchor(int index) => _sectionAnchors[index];

    /// <summary>
    /// index番目のセクションの霧を返す。設定が無ければ霧なし（useFog = false）。
    /// </summary>
    public SectionAtmosphere GetAtmosphere(int index)
    {
        if (_sectionAtmospheres == null || _sectionAtmospheres.Length == 0) return default;
        return _sectionAtmospheres[Mathf.Clamp(index, 0, _sectionAtmospheres.Length - 1)];
    }
}
