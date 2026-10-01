namespace MUDRA.Data
{
    /// <summary>
    /// 属性 ボスの弱点属性との相性計算に使用する
    /// </summary>
    public enum ElementType
    {
        Wind,
        Earth,
        Thunder,
        Water,
        Fire,
        Light,
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