using LitMotion;
using UnityEngine;

/// <summary>
/// 右下のカメラ映像（MediaPipeのMain Canvas）の表示を切り替える（B5）。
/// Canvasの描画だけを止め、GameObjectは止めない。推論（Solution）や映像の更新は続ける必要があるため。
///
/// B6で「強調表示」を追加した。Calibration画面の間だけ映像を大きくし、画面（ScreenCanvas）より手前に出す。
/// 映像のCanvasは画面より奥（order 0 &lt; 10）にあり、そのままだと画面の背景に隠れるため、並び順も一時的に上げる。
/// 強調表示の間は、設定でカメラ映像を非表示にしていても表示する（手が映っているかを確かめる画面なので）。
/// </summary>
public class CameraPreviewView : MonoBehaviour
{
    /// <summary>強調表示の間の並び順。画面（10）より手前、設定画面（30）・遷移（100）より奥</summary>
    private const int EmphasizedSortingOrder = 15;

    /// <summary>強調表示の映像の倍率（通常は0.3。Main Canvasの基準解像度に対する比）</summary>
    private const float EmphasizedScale = 0.55f;

    /// <summary>強調表示の映像の位置（Main Canvasの座標）。画面の左寄りに置き、右側をCalibrationのUIに空ける</summary>
    private static readonly Vector2 EmphasizedPosition = new(-400f, 30f);

    private const float MoveDuration = 0.3f;

    [SerializeField] private Canvas _canvas;
    [Tooltip("映像のRectTransform（Main Canvas/Body/Annotatable Screen）")]
    [SerializeField] private RectTransform _screen;

    private bool _isVisibleBySetting = true;
    private bool _isEmphasized;

    // 通常（右下）の配置。シーンに保存された値を、最初に強調表示する時に覚える
    private bool _hasCornerLayout;
    private Vector2 _cornerPosition;
    private float _cornerScale;
    private int _cornerSortingOrder;

    private MotionHandle _moveHandle;

    /// <summary>設定画面の「カメラ映像の表示」</summary>
    public void SetVisible(bool isVisible)
    {
        _isVisibleBySetting = isVisible;
        ApplyVisibility();
    }

    /// <summary>映像を大きく手前に出す（Calibration画面の間だけtrue）</summary>
    public void SetEmphasized(bool isEmphasized)
    {
        if (_isEmphasized == isEmphasized) return;
        _isEmphasized = isEmphasized;

        RememberCornerLayout();
        _canvas.sortingOrder = isEmphasized ? EmphasizedSortingOrder : _cornerSortingOrder;
        ApplyVisibility();

        var fromPosition = _screen.anchoredPosition;
        float fromScale = _screen.localScale.x;
        var toPosition = isEmphasized ? EmphasizedPosition : _cornerPosition;
        float toScale = isEmphasized ? EmphasizedScale : _cornerScale;

        CancelMove();
        _moveHandle = LMotion.Create(0f, 1f, MoveDuration)
            .WithEase(Ease.OutCubic)
            .Bind(t =>
            {
                _screen.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
                float s = Mathf.LerpUnclamped(fromScale, toScale, t);
                _screen.localScale = new Vector3(s, s, 1f);
            });
    }

    private void RememberCornerLayout()
    {
        if (_hasCornerLayout) return;
        _hasCornerLayout = true;
        _cornerPosition = _screen.anchoredPosition;
        _cornerScale = _screen.localScale.x;
        _cornerSortingOrder = _canvas.sortingOrder;
    }

    private void ApplyVisibility()
    {
        _canvas.enabled = _isVisibleBySetting || _isEmphasized;
    }

    private void CancelMove()
    {
        if (_moveHandle.IsActive()) _moveHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelMove();
    }
}
