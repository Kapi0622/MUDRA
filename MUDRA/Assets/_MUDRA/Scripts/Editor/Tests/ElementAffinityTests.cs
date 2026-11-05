using MUDRA.Data;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 相性の規則（ElementAffinity）と倍率（BalanceData）の EditMode テスト（B8.3）。
/// 期待値は規則から計算せず、変更書（docs/spec_change_element_affinity.md §2-3）の表を手で書き写している。
/// 規則のコードを書き間違えた時に、同じ間違いで期待値まで変わってしまわないようにするため。
/// </summary>
public class ElementAffinityTests
{
    // 列の並び（ElementType の値の順）
    private static readonly ElementType[] Order =
    {
        ElementType.Wind, ElementType.Earth, ElementType.Thunder, ElementType.Water,
        ElementType.Fire, ElementType.Light, ElementType.Dark, ElementType.None,
    };

    // 行 = 攻撃側、列 = 受ける側（Order の順）。A = 有利、D = 不利、N = 等倍
    //                          風 土 雷 水 火 光 闇 無
    private static readonly string[] Table =
    {
        /* 風 */ "DANNDNNN",
        /* 土 */ "DDANNNNN",
        /* 雷 */ "NDDANNNN",
        /* 水 */ "NNDDANNN",
        /* 火 */ "ANNDDNNN",
        /* 光 */ "NNNNNDAN", // 光と闇は互いに弱点
        /* 闇 */ "NNNNNADN",
        /* 無 */ "NNNNNNNN", // 無は常に等倍
    };

    [Test]
    public void 全マスが相性表と一致する()
    {
        for (int a = 0; a < Order.Length; a++)
        {
            for (int d = 0; d < Order.Length; d++)
            {
                var expected = Table[a][d] switch
                {
                    'A' => ElementRelation.Advantage,
                    'D' => ElementRelation.Disadvantage,
                    _ => ElementRelation.Neutral,
                };
                Assert.AreEqual(expected, ElementAffinity.GetRelation(Order[a], Order[d]),
                    $"攻撃 {Order[a]} → 受ける側 {Order[d]}");
            }
        }
    }

    [TestCase(ElementType.Wind, ElementType.Fire)]
    [TestCase(ElementType.Earth, ElementType.Wind)]
    [TestCase(ElementType.Thunder, ElementType.Earth)]
    [TestCase(ElementType.Water, ElementType.Thunder)]
    [TestCase(ElementType.Fire, ElementType.Water)]
    [TestCase(ElementType.Light, ElementType.Dark)]
    [TestCase(ElementType.Dark, ElementType.Light)]
    public void 弱点は有利になる属性(ElementType defender, ElementType weakness)
    {
        Assert.AreEqual(weakness, ElementAffinity.GetWeakness(defender));
        Assert.AreEqual(ElementRelation.Advantage, ElementAffinity.GetRelation(weakness, defender));
    }

    [Test]
    public void 無属性には弱点が無い()
    {
        Assert.IsNull(ElementAffinity.GetWeakness(ElementType.None));
    }

    [Test]
    public void どの属性にも有利になる属性はちょうど1つ()
    {
        foreach (var defender in Order)
        {
            if (defender == ElementType.None) continue;
            int count = 0;
            foreach (var attacker in Order)
                if (ElementAffinity.GetRelation(attacker, defender) == ElementRelation.Advantage) count++;
            Assert.AreEqual(1, count, defender.ToString());
        }
    }

    [Test]
    public void 倍率は有利1_5_不利0_75_等倍1()
    {
        var balance = ScriptableObject.CreateInstance<BalanceData>();
        try
        {
            Assert.AreEqual(1.5f, balance.AffinityMultiplier(ElementRelation.Advantage));
            Assert.AreEqual(0.75f, balance.AffinityMultiplier(ElementRelation.Disadvantage));
            Assert.AreEqual(1f, balance.AffinityMultiplier(ElementRelation.Neutral));
        }
        finally
        {
            Object.DestroyImmediate(balance);
        }
    }
}
