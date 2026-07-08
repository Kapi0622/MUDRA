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