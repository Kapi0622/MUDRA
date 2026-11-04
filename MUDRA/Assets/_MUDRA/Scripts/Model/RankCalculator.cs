using System;

/// <summary>
/// 戦績からランクを決める（B5、B9で作り直し）。クリアした時だけ使う。
///
/// 評価の軸は「クリア時間」と「被弾」の2つだけで、時間0〜70点・被弾0〜30点にして足す。
/// コンボ・弱点・ガード・暴発は数えない。数えるとその行動を強いられる感じが出るうえ、
/// 上手い行動はどれも時間（早く倒せる）か被弾（ダメージが減る）に表れるため。
/// 2軸を足すので片方だけでは満点にならない。時間を主役にするため、時間のほうを重くしている。
///
/// ステージごとに長さも敵の攻撃の重さも違うので、満点になる目標タイムと目標ダメージは
/// StageDataに持たせ、ここでは数値として受け取る。
/// 被ダメージは回復で減らさない（回復術は生き残る手段で、点数を稼ぐ道具ではない）。
///
/// Result画面のゲージ（点数の見える化）に使うため、点数としきい値・上限も公開する。
/// </summary>
public static class RankCalculator
{
    /// <summary>時間点の満点。評価の主役なので被弾点より重くする</summary>
    private const float TimeMaxScore = 70f;

    /// <summary>被弾点の満点</summary>
    private const float DamageMaxScore = 30f;

    /// <summary>目標タイムの何倍で時間点が0になるか</summary>
    private const float TimeZeroRatio = 2f;

    private const float RankSThreshold = 90f;
    private const float RankAThreshold = 70f;
    private const float RankBThreshold = 50f;

    /// <summary>Sランクになる最低点</summary>
    public static float ThresholdS => RankSThreshold;

    /// <summary>Aランクになる最低点</summary>
    public static float ThresholdA => RankAThreshold;

    /// <summary>Bランクになる最低点</summary>
    public static float ThresholdB => RankBThreshold;

    /// <summary>取りうる最高点（両方の軸が満点）。ゲージの右端に使う</summary>
    public static float MaxScore => TimeMaxScore + DamageMaxScore;

    /// <summary>戦績の点数（0〜MaxScore）</summary>
    /// <param name="targetSeconds">この時間以内なら時間点が満点。0以下（未設定）なら時間点は常に満点</param>
    /// <param name="targetDamage">この被ダメージ以下なら被弾点が満点。これに最大HPぶん上乗せすると0点</param>
    public static float Score(BattleStats stats, int playerMaxHp, float targetSeconds, int targetDamage)
    {
        var (time, damage) = ScoreBreakdown(stats, playerMaxHp, targetSeconds, targetDamage);
        return time + damage;
    }

    /// <summary>
    /// 時間点と被弾点を別々に返す。目標値の調整でどちらの軸が足りないかを見るため
    /// </summary>
    public static (float Time, float Damage) ScoreBreakdown(
        BattleStats stats, int playerMaxHp, float targetSeconds, int targetDamage)
    {
        return (TimeScore(stats.ElapsedSeconds, targetSeconds),
                DamageScore(stats.DamageTaken, playerMaxHp, targetDamage));
    }

    /// <summary>点数からランクを決める</summary>
    public static BattleRank RankFromScore(float score)
    {
        if (score >= RankSThreshold) return BattleRank.S;
        if (score >= RankAThreshold) return BattleRank.A;
        if (score >= RankBThreshold) return BattleRank.B;
        return BattleRank.C;
    }

    /// <summary>目標タイム以内で満点、目標タイムのTimeZeroRatio倍で0点。間は直線</summary>
    private static float TimeScore(float elapsedSeconds, float targetSeconds)
    {
        if (targetSeconds <= 0f) return TimeMaxScore;

        float zeroSeconds = targetSeconds * TimeZeroRatio;
        float ratio = (zeroSeconds - elapsedSeconds) / (zeroSeconds - targetSeconds);
        return TimeMaxScore * Clamp01(ratio);
    }

    /// <summary>目標ダメージ以下で満点、目標ダメージ＋最大HPで0点。間は直線</summary>
    private static float DamageScore(int damageTaken, int playerMaxHp, int targetDamage)
    {
        if (playerMaxHp <= 0) return DamageMaxScore;

        int overDamage = Math.Max(0, damageTaken - targetDamage);
        return DamageMaxScore * Clamp01(1f - (float)overDamage / playerMaxHp);
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
}
