/// <summary>
/// 1回のバトルの戦績（B5）。BattleStatsModelが集計し、Result画面とランク評価に使う。
/// </summary>
public readonly struct BattleStats
{
    /// <summary>ステージ開始から決着までの時間（秒）。ゲーム内時間なのでポーズ中は含まない</summary>
    public readonly float ElapsedSeconds;

    public readonly int MaxCombo;

    /// <summary>弱点属性で当てた回数（術1回につき1回。連撃の段数は数えない）</summary>
    public readonly int WeaknessHits;

    /// <summary>ガード受付中に敵の攻撃を受けた回数</summary>
    public readonly int GuardSuccesses;

    /// <summary>発動に成功した術の回数</summary>
    public readonly int SpellsCast;

    public readonly int Misfires;

    /// <summary>受けたダメージの合計（暴発のセルフダメージを含む。回復では減らない）</summary>
    public readonly int DamageTaken;

    public BattleStats(float elapsedSeconds, int maxCombo, int weaknessHits, int guardSuccesses,
        int spellsCast, int misfires, int damageTaken)
    {
        ElapsedSeconds = elapsedSeconds;
        MaxCombo = maxCombo;
        WeaknessHits = weaknessHits;
        GuardSuccesses = guardSuccesses;
        SpellsCast = spellsCast;
        Misfires = misfires;
        DamageTaken = damageTaken;
    }
}
