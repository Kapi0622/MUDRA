/// <summary>
/// GameStateManagerのフェーズ遷移1回分。
/// リトライ（InGame→InGame）は値が変わらずReactivePropertyでは通知されないため、
/// 遷移はこの型のイベントとして別に流す。
/// </summary>
public readonly struct GameTransition
{
    public readonly GamePhase From;
    public readonly GamePhase To;

    /// <summary>同じフェーズへの遷移。InGameのやり直し（リトライ）</summary>
    public bool IsReload => From == To;

    public GameTransition(GamePhase from, GamePhase to)
    {
        From = from;
        To = to;
    }
}
