using System;
using R3;

/// <summary>
/// ガード受付窓を管理する。
/// Guard印確定 → 1秒の受付窓が開く → 自動終了。
/// PlayerPhaseとは独立して動作し、詠唱中でもガードできる。
/// BattlePresenter.Update()から毎フレームTick()で駆動する。
/// </summary>
public class GuardWindowManager : IDisposable
{
    private const float WindowDuration = 1f;

    private float _remainingTime;

    private readonly ReactiveProperty<bool> _isGuarding = new(false);
    /// <summary>
    /// 現在ガード受付中かどうか。
    /// ReactivePropertyにしているのは、受付窓が開いている間の「構え」表示をViewに出すため（B4）。
    /// 値が変わった時だけ通知されるので、毎フレームのTickでも購読側には開閉の2回しか届かない。
    /// </summary>
    public ReadOnlyReactiveProperty<bool> IsGuarding => _isGuarding;

    /// <summary>Guard印が確定した時に呼ぶ。受付窓を開く。</summary>
    public void Activate()
    {
        _remainingTime = WindowDuration;
        _isGuarding.Value = true;
    }

    /// <summary>毎フレーム呼ぶ。残り時間を減算する。</summary>
    public void Tick(float deltaTime)
    {
        if (_remainingTime <= 0f) return;

        _remainingTime -= deltaTime;
        if (_remainingTime <= 0f)
            _isGuarding.Value = false;
    }

    public void Dispose()
    {
        _isGuarding.Dispose();
    }
}
