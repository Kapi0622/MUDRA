using UnityEngine;

/// <summary>
/// クリア記録の表示の書式。ステージ選択のカードと図鑑（妖の出現ステージ）で同じ書き方にするため共有する。
/// </summary>
public static class StageRecordFormat
{
    /// <summary>「討伐済　S　3:12」。未クリア（null）なら空</summary>
    public static string Format(StageRecordData record)
    {
        if (record == null) return string.Empty;
        return $"討伐済　{(BattleRank)record.bestRank}　{FormatTime(record.bestTimeSeconds)}";
    }

    /// <summary>「分:秒」。1時間を超えることは想定しない</summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60}:{total % 60:00}";
    }
}
