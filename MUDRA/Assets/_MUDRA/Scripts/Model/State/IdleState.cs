/// <summary>
/// 待機状態 印を組んでいない
/// </summary>
public class IdleState : IPlayerState
{
    public PlayerPhase Phase => PlayerPhase.Idle;

    public void Enter() { }
    public void Exit() { }
}
