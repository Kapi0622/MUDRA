using System;
using R3;

/// <summary>
/// プレイヤーの行動フェーズ（State）を管理する Pure C#クラス
/// SpellSequenceModelからのイベントを受けて遷移を判断する
/// </summary>
public class PlayerStateManager : IDisposable
{
    private readonly IdleState _idleState;
    private readonly ChantingState _chantingState;
    private readonly ReleasingState _releasingState;

    private IPlayerState _currentState;

    private readonly ReactiveProperty<PlayerPhase> _currentPhase;

    /// <summary>フェーズが変わるたびに通知（View側のデバッグ表示等に利用可能）</summary>
    public ReadOnlyReactiveProperty<PlayerPhase> CurrentPhase => _currentPhase;

    public PlayerStateManager()
    {
        _idleState = new IdleState();
        _chantingState = new ChantingState();
        _releasingState = new ReleasingState(this);

        _currentState = _idleState;
        _currentPhase = new ReactiveProperty<PlayerPhase>(_currentState.Phase);
        _currentState.Enter();
    }

    /// <summary>SpellSequenceModel.OnChantStartedを受けて呼ぶ</summary>
    public void HandleChantStarted()
    {
        if (_currentPhase.CurrentValue == PlayerPhase.Idle)
        {
            TransitionTo(_chantingState);
        }
    }

    /// <summary>SpellSequenceModel.OnSpellCastを受けて呼ぶ</summary>
    public void HandleSpellCast()
    {
        if (_currentPhase.CurrentValue == PlayerPhase.Chanting)
        {
            TransitionTo(_releasingState);
        }
    }

    /// <summary>SpellSequenceModel.OnSequenceResetを受けて呼ぶ</summary>
    public void HandleSequenceReset()
    {
        if (_currentPhase.CurrentValue == PlayerPhase.Chanting)
        {
            TransitionTo(_idleState);
        }
    }

    /// <summary>ReleasingStateの硬直タイマー完了時に呼ばれる</summary>
    public void TransitionToIdle()
    {
        TransitionTo(_idleState);
    }

    private void TransitionTo(IPlayerState newState)
    {
        if (_currentState == newState) return;

        _currentState.Exit();
        _currentState = newState;
        _currentState.Enter();

        _currentPhase.Value = _currentState.Phase; // 代入するだけで購読者に通知される
    }
    
    public void Dispose()
    {
        _currentPhase.Dispose();
    }
}