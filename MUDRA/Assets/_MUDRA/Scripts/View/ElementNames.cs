using MUDRA.Data;

/// <summary>
/// 属性の表示名（B5）。StageSelectの弱点ヒントとResultの敗北ヒントで共有する。
/// </summary>
public static class ElementNames
{
    public static string Get(ElementType element)
    {
        return element switch
        {
            ElementType.Wind => "風",
            ElementType.Earth => "土",
            ElementType.Thunder => "雷",
            ElementType.Water => "水",
            ElementType.Fire => "火",
            ElementType.Light => "光",
            _ => "？"
        };
    }
}
