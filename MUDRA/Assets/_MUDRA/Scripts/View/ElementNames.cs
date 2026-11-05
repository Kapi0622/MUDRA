using MUDRA.Data;

/// <summary>
/// 属性の表示名（B5）。StageSelectの弱点ヒント・Resultの敗北ヒント・HPバー・図鑑で共有する。
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
            ElementType.Dark => "闇",
            ElementType.None => "無",
            _ => "？"
        };
    }

    /// <summary>弱点が無い（ElementAffinity.GetWeakness が null）時は「？」</summary>
    public static string Get(ElementType? element) => element.HasValue ? Get(element.Value) : "？";
}
