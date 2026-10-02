/// <summary>
/// ステージ進行のフェーズ。SectionProgressManagerが保持する。
/// </summary>
public enum SectionPhase
{
    /// <summary>現在のセクションの敵と交戦中</summary>
    InBattle,

    /// <summary>セクション撃破後、次の敵が出現するまでの遷移中</summary>
    Transitioning,

    /// <summary>全セクション撃破。ステージクリア</summary>
    StageCleared,

    /// <summary>プレイヤー敗北</summary>
    GameOver,
}
