using MUDRA.Data;

/// <summary>
/// 属性の相性（B8.3）。「攻撃側の属性 × 受ける側の属性」から関係を決める唯一の場所。
/// 術から敵へも、敵の攻撃から自分へも、将来の対戦でも同じ規則を使い、各所で属性を直接比べない。
///
/// 規則:
/// - どちらかが無 → 等倍
/// - 攻撃側が受ける側に強い → 有利
/// - 受ける側が攻撃側に強い、または同じ属性 → 不利
/// - それ以外 → 等倍
/// 強い関係は 水→火→風→土→雷→水 の循環と、光⇄闇（互いに弱点。B8.3でユーザー判断により一方向から変更）だけ。
/// 光と闇の倍率は他と同じ（覚える値・調整する値を増やさないため）。
///
/// マス目の表（ScriptableObject）にしないのは、上の規則で全マスが決まり、
/// 表にすると規則から外れたマスを作れてしまうため。対戦で両者の表が食い違う心配もない。
/// 倍率はBalanceData.AffinityMultiplierが持つ。
/// </summary>
public static class ElementAffinity
{
    // 各要素は次の要素に強い（末尾は先頭に強い）
    private static readonly ElementType[] Cycle =
    {
        ElementType.Water,
        ElementType.Fire,
        ElementType.Wind,
        ElementType.Earth,
        ElementType.Thunder,
    };

    /// <summary>攻撃側から見た、受ける側との関係</summary>
    public static ElementRelation GetRelation(ElementType attacker, ElementType defender)
    {
        if (attacker == ElementType.None || defender == ElementType.None) return ElementRelation.Neutral;
        if (Beats(attacker, defender)) return ElementRelation.Advantage;
        if (attacker == defender || Beats(defender, attacker)) return ElementRelation.Disadvantage;
        return ElementRelation.Neutral;
    }

    /// <summary>
    /// 受ける側の弱点（その属性に強い属性）。ちょうど1つ決まる。
    /// 無には強い属性が無いので null（表示は「？」）
    /// </summary>
    public static ElementType? GetWeakness(ElementType defender)
    {
        if (defender == ElementType.Dark) return ElementType.Light;
        if (defender == ElementType.Light) return ElementType.Dark;

        int index = System.Array.IndexOf(Cycle, defender);
        if (index < 0) return null;
        return Cycle[(index + Cycle.Length - 1) % Cycle.Length];
    }

    private static bool Beats(ElementType attacker, ElementType defender)
    {
        if (attacker == ElementType.Light) return defender == ElementType.Dark;
        if (attacker == ElementType.Dark) return defender == ElementType.Light;

        int index = System.Array.IndexOf(Cycle, attacker);
        return index >= 0 && Cycle[(index + 1) % Cycle.Length] == defender;
    }
}
