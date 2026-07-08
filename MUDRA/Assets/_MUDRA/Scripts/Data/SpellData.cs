using MUDRA.HandTracking;
using UnityEngine;

namespace MUDRA.Data
{
    /// <summary>
    /// 術の定義データ Inspector上で新しい術を追加する際はこのアセットを作成するだけで完結する
    /// A1時点ではspellNameとsequenceのみ使用中 その他はA2以降で活用予定
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpell", menuName = "MUDRA/SpellData")]
    public class SpellData : ScriptableObject
    {
        [Header("基本情報")] 
        [Tooltip("術の表示名")] 
        public string spellName;

        [Tooltip("属性")] 
        public ElementType element;

        [Tooltip("UIアイコン")] 
        public Sprite icon;

        [Tooltip("術の説明文（図鑑用）")] 
        [TextArea(2, 4)]
        public string description;

        [Header("シーケンス定義")] 
        [Tooltip("詠唱印の配列 この順番で手印を入力し 最後に発動印で発動する")]
        public HandSign[] sequence;

        [Header("戦闘パラメータ")] 
        [Tooltip("基礎威力")] 
        public float basePower;

        [Tooltip("ダメージ計算方式（Strategyの選択に使用）")] 
        public DamageType damageType;

        [Tooltip("ヒット数（MultiHit時に使用）")] 
        public int hitCount = 1;
            
        [Tooltip("攻撃範囲")]
        public AttackRangeType rangeType;

        [Header("副次効果")] 
        [Tooltip("付与する副次効果")] 
        public StatusEffectType statusEffect;

        [Tooltip("副次効果の持続時間（秒）")] 
        public float statusEffectDuration;

        [Header("演出")] 
        [Tooltip("術エフェクトのPrefab")]
        public GameObject effectPrefab;

        [Tooltip("発動時のSE")] 
        public AudioClip castSE;

        [Tooltip("カットイン演出のSprite（術名テロップ）")] 
        public Sprite cutInSprite;
    }
}