using UnityEngine;

namespace MUDRA.Data
{
    /// <summary>
    /// バトルの調整値（B8）。術・敵ごとの値はSpellData・EnemyDataに持たせ、ここには全体に掛かる値だけを集める。
    ///
    /// 以前は各Model（BattleModel・GuardWindowManager・SpellSequenceModel）のconstと
    /// BattleInitializerのInspectorに散っていた。通しプレイしながらInspectorで直せるよう1つにまとめている。
    /// Modelはこのアセットを読むだけで書き換えない（再生中にInspectorで変えた値はその場で効く）。
    /// </summary>
    [CreateAssetMenu(fileName = "BD_Balance", menuName = "MUDRA/Balance")]
    public class BalanceData : ScriptableObject
    {
        [Header("プレイヤー")]
        [Tooltip("プレイヤーの最大HP。ステージ開始時に満タンにする（セクション間では回復しない）")]
        [Min(1)]
        public int playerMaxHp = 100;

        [Tooltip("暴発のセルフダメージ（最大HPに対する割合）")]
        [Range(0f, 1f)]
        public float misfireDamageRate = 0.05f;

        [Header("ガード")]
        [Tooltip("Guard印を確定してからガードが有効な秒数")]
        [Min(0f)]
        public float guardWindowDuration = 1f;

        [Tooltip("通常攻撃をガードした時に受けるダメージの割合")]
        [Range(0f, 1f)]
        public float normalGuardRate = 0.5f;

        [Tooltip("大技をガードした時に受けるダメージの割合")]
        [Range(0f, 1f)]
        public float heavyGuardRate = 0.3f;

        [Header("速度ボーナス")]
        [Tooltip("印1つあたりの基準秒数。詠唱時間が「印数 × この値」以内なら速度ボーナスが付く")]
        [Min(0f)]
        public float speedBonusTimePerSign = 1.0f;

        [Tooltip("速度ボーナスの倍率。B8で1.5から下げた（上級者だけ戦闘が極端に短くなるのを抑えるため）")]
        [Min(1f)]
        public float speedBonusMultiplier = 1.3f;

        [Header("コンボ")]
        [Tooltip("1コンボあたりの倍率の増分。倍率 = 1 + この値 × min(コンボ数, 上限)")]
        [Min(0f)]
        public float comboBonusPerCount = 0.03f;

        [Tooltip("倍率に乗せるコンボ数の上限。カウント自体は上限を超えても増え続ける（表示とランク評価のため）")]
        [Min(0)]
        public int comboBonusCap = 10;

        [Header("連続減衰")]
        [Tooltip("直前に撃った攻撃術と同じ属性の術に掛ける倍率。弱点属性の連打を抑える")]
        [Range(0f, 1f)]
        public float repeatElementMultiplier = 0.6f;

        [Header("Stun（封殺し対策）")]
        [Tooltip("Stunが入る基本の確率。敵ごとのEnemyData.stunChanceMultiplierを掛けて使う")]
        [Range(0f, 1f)]
        public float stunBaseChance = 0.6f;

        [Tooltip("同じ敵にStunが入るたびに確率へ掛ける値（0.5なら 60%→30%→15%）。失敗した時と敵が替わった時に基本の確率へ戻る")]
        [Range(0f, 1f)]
        public float stunChanceDecay = 0.5f;
    }
}
