using UnityEngine;

namespace MUDRA.Data
{
    /// <summary>
    /// 意味を持ち、複数のViewで同じ色を使う「意味の色」の表。
    /// 以前は各Viewのconstにコメントで「揃えた」と書いていたが、実際には値がずれていたため1か所に集めた。
    ///
    /// 効果（DoT・回復）の色は属性ではなく効果の種類で決める（devlog B4 §3-7）。
    /// 1つのViewでしか使わない色は、それぞれのViewのconstに残している。
    /// </summary>
    [CreateAssetMenu(fileName = "PL_Battle", menuName = "MUDRA/Battle Palette")]
    public class BattlePaletteData : ScriptableObject
    {
        [Header("効果")]
        [Tooltip("DoT。敵の被弾フラッシュとtickの数字")]
        public Color dot = new Color(0.75f, 0.45f, 0.9f);

        [Tooltip("回復量の数字")]
        public Color heal = new Color(0.4f, 1f, 0.5f);

        [Tooltip("ガード成功。画面フラッシュとガード時の被ダメージ数字")]
        public Color guard = new Color(0.6f, 0.85f, 1f);

        [Tooltip("弱点ヒット。敵の被弾フラッシュとダメージ数字")]
        public Color weakness = new Color(1f, 0.85f, 0.2f);

        // 術エフェクト（パーティクル）の色と揃えている。変えるときはエフェクトのプレハブも合わせる
        [Header("属性")]
        public Color wind = new Color(0.35f, 0.8f, 0.45f);
        public Color earth = new Color(0.7f, 0.5f, 0.25f);
        public Color thunder = new Color(0.85f, 0.75f, 0.1f);
        public Color water = new Color(0.2f, 0.45f, 0.9f);
        public Color fire = new Color(0.9f, 0.3f, 0.1f);
        public Color light = new Color(0.95f, 0.9f, 0.7f);

        [Tooltip("色を定義していない属性")]
        public Color unknownElement = Color.gray;

        /// <summary>属性の色。カットインの帯など、どの属性の術かを示す表示に使う</summary>
        public Color ElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Wind => wind,
                ElementType.Earth => earth,
                ElementType.Thunder => thunder,
                ElementType.Water => water,
                ElementType.Fire => fire,
                ElementType.Light => light,
                _ => unknownElement,
            };
        }
    }
}
