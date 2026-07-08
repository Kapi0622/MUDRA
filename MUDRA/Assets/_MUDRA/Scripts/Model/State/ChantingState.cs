/// <summary>
/// 詠唱中 シーケンス入力中
/// </summary>
public class ChantingState : IPlayerState
{
    public PlayerPhase Phase => PlayerPhase.Chanting;

    public void Enter() { }
    public void Exit() { }
}
