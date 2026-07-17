/// <summary>
/// ガード受付窓を管理する。
/// Guard印確定 → 0.5秒の受付窓が開く → 自動終了。
/// PlayerPhaseとは独立して動作し、詠唱中でもガードできる。
/// BattlePresenter.Update()から毎フレームTick()で駆動する。
/// </summary>
public class GuardWindowManager
{
    private const float WindowDuration = 1f;

    private float _remainingTime;

    /// <summary>現在ガード受付中かどうか</summary>
    public bool IsGuarding => _remainingTime > 0f;

    /// <summary>Guard印が確定した時に呼ぶ。受付窓を開く。</summary>
    public void Activate()
    {
        _remainingTime = WindowDuration;
    }

    /// <summary>毎フレーム呼ぶ。残り時間を減算する。</summary>
    public void Tick(float deltaTime)
    {
        if (_remainingTime > 0f)
            _remainingTime -= deltaTime;
    }
}