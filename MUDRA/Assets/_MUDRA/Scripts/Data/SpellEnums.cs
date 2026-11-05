namespace MUDRA.Data
{
    /// <summary>
    /// 属性。術・敵・（将来の対戦では）プレイヤーが持ち、相性は ElementAffinity が決める（B8.3）。
    /// 値はSpellData・EnemyDataにシリアライズ済みなので、全て明示し、追加は必ず末尾にする
    /// （HandSignで起きた、値の挿入によるずれの予防）。
    /// </summary>
    public enum ElementType
    {
        Wind    = 0, // 風（木を含む）
        Earth   = 1, // 土
        Thunder = 2, // 雷
        Water   = 3, // 水
        Fire    = 4, // 火
        Light   = 5, // 光
        Dark    = 6, // 闇（今は敵だけ）
        None    = 7, // 無（相性の影響を受けない）
    }

    /// <summary>
    /// 攻撃側から見た、受ける側の属性との関係（B8.3）。倍率そのものはBalanceDataが持つ。
    /// </summary>
    public enum ElementRelation
    {
        Neutral      = 0, // 等倍
        Advantage    = 1, // 有利（受ける側の弱点）
        Disadvantage = 2, // 不利（受ける側が強い、または同じ属性）
    }

    /// <summary>
    /// 攻撃範囲タイプ
    /// </summary>
    public enum AttackRangeType
    {
        Single,
        Area,
    }

    /// <summary>
    /// 副次効果の種類
    /// </summary>
    public enum StatusEffectType
    {
        None,
        Slow,
        Stun,
        DamageOverTime,
        /// <summary>
        /// 継続回復（HoT）。他の効果が敵に付くのに対し、これだけはプレイヤーに付く。
        /// セクション遷移時のクリア対象から外れる唯一の効果でもある（HotEffect参照）。
        /// </summary>
        HealOverTime,
    }

    /// <summary>
    /// ダメージ計算方式 SpellDataからStrategyを選択するために使用する
    /// </summary>
    public enum DamageType
    {
        SingleHit,
        MultiHit, 
        DamageOverTime,
    }
}