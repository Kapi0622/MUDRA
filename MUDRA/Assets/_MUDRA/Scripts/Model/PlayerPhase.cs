/// <summary>
/// プレイヤーの行動フェーズ
/// A1範囲ではIdle/Chanting/Releasingの3つのみ（Guardingは未実装）
/// </summary>
public enum PlayerPhase
{
    Idle,
    Chanting,
    Releasing,
}