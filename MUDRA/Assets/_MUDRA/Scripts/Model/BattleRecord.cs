/// <summary>
/// 1回のバトル（ステージ挑戦）の結果。Result画面とクリア記録に渡す。
/// </summary>
public readonly struct BattleRecord
{
    /// <summary>挑戦したステージ</summary>
    public readonly StageData Stage;

    /// <summary>true: ステージクリア / false: 敗北</summary>
    public readonly bool IsWin;

    public readonly BattleStats Stats;

    /// <summary>クリア時のランク。敗北時はnull</summary>
    public readonly BattleRank? Rank;

    /// <summary>決着の時に戦っていた敵。敗北時のヒント（弱点）に使う</summary>
    public readonly EnemyData LastEnemy;

    public BattleRecord(StageData stage, bool isWin, BattleStats stats, BattleRank? rank, EnemyData lastEnemy)
    {
        Stage = stage;
        IsWin = isWin;
        Stats = stats;
        Rank = rank;
        LastEnemy = lastEnemy;
    }
}
