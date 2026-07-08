/// <summary>
/// プレイヤーの行動フェーズごとの振る舞いを表すState
/// </summary>
public interface IPlayerState
{
    PlayerPhase Phase { get; }

    /// <summary>この状態に入った直後の処理</summary>
    void Enter();

    /// <summary>この状態から抜ける直前の処理</summary>
    void Exit();
}