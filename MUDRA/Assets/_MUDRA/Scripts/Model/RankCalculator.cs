using System;

/// <summary>
/// 戦績からランクを決める（B5）。クリアした時だけ使う。
///
/// 100点からの加減点で決める。重視するのは「被弾を抑えたか」で、次いで暴発の少なさ。
/// コンボ・弱点・ガードは加点にして、攻め方の上手さも拾う。
/// 時間はステージごとに長さが違うため点数には入れない（Result画面に表示だけする）。
/// しきい値と重みは仮の値で、B8のバランス調整で詰める。
/// </summary>
public static class RankCalculator
{
    private const float BaseScore = 100f;

    /// <summary>最大HPと同じだけ被弾した時の減点</summary>
    private const float DamagePenaltyPerMaxHp = 60f;
    private const float MisfirePenalty = 5f;

    private const float ComboBonusPerCount = 2f;
    private const int ComboBonusCap = 10;
    private const float WeaknessBonusPerHit = 1f;
    private const int WeaknessBonusCap = 10;
    private const float GuardBonusPerSuccess = 2f;
    private const int GuardBonusCap = 5;

    private const float RankSThreshold = 100f;
    private const float RankAThreshold = 80f;
    private const float RankBThreshold = 60f;

    public static BattleRank Calculate(BattleStats stats, int playerMaxHp)
    {
        float score = BaseScore;

        if (playerMaxHp > 0)
            score -= (float)stats.DamageTaken / playerMaxHp * DamagePenaltyPerMaxHp;
        score -= stats.Misfires * MisfirePenalty;

        score += Math.Min(stats.MaxCombo, ComboBonusCap) * ComboBonusPerCount;
        score += Math.Min(stats.WeaknessHits, WeaknessBonusCap) * WeaknessBonusPerHit;
        score += Math.Min(stats.GuardSuccesses, GuardBonusCap) * GuardBonusPerSuccess;

        if (score >= RankSThreshold) return BattleRank.S;
        if (score >= RankAThreshold) return BattleRank.A;
        if (score >= RankBThreshold) return BattleRank.B;
        return BattleRank.C;
    }
}
