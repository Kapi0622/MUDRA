using UnityEngine;

/// <summary>
/// 右下のカメラ映像（MediaPipeのMain Canvas）の表示を切り替える（B5）。
/// Canvasの描画だけを止め、GameObjectは止めない。推論（Solution）や映像の更新は続ける必要があるため。
/// </summary>
public class CameraPreviewView : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;

    public void SetVisible(bool isVisible)
    {
        _canvas.enabled = isVisible;
    }
}
