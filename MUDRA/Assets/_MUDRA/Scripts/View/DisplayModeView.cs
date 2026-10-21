using UnityEngine;

/// <summary>
/// ウィンドウ / フルスクリーンを切り替える（B5）。エディタでは効果が無い（ビルドでのみ切り替わる）。
/// </summary>
public class DisplayModeView : MonoBehaviour
{
    public void SetFullScreen(bool isFullScreen)
    {
        Screen.fullScreenMode = isFullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }
}
