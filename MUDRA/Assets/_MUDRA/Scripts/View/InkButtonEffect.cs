using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 墨の帯のボタンの反応（UI改修 ボタン ステップ2）。ボタン本体に付け、マウスを乗せる・押す・離すに合わせて
/// 大きさと帯の色を短く補間する。
///
/// Button本来の色の変化（ColorTint）と二重にならないよう、付けるボタンのTransitionはNoneにしておく。
/// 主要ボタン（朱）とその他（淡い帯）の違いは、Inspectorで設定する色の値だけで付ける。
///
/// 画面はアクティブのままCanvasGroupで隠す作り（ScreenView）なので、隠れた時にポインタが外れた扱いにならないことがある。
/// 操作できなくなったら（Selectable.IsInteractableがCanvasGroupも見る）元の状態へ戻す。
/// ポーズ中のメニューでも動くよう実時間で進める。
/// </summary>
[RequireComponent(typeof(Selectable))]
public class InkButtonEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const float HoverScale = 1.05f;
    private const float PressScale = 0.97f;
    private const float HoverDuration = 0.12f;
    private const float PressDuration = 0.06f;

    [Tooltip("色を変える帯（ボタンのImage）")]
    [SerializeField] private Graphic _band;
    [SerializeField] private Color _normalColor = new(0.95f, 0.9f, 0.78f, 0.15f);
    [SerializeField] private Color _hoverColor = new(0.95f, 0.9f, 0.78f, 0.3f);

    private Selectable _selectable;
    private MotionHandle _handle;
    private bool _isHovered;
    private bool _isPressed;
    private float _scale = 1f;
    private Color _color;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        ApplyImmediate(1f, _normalColor);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_selectable.IsInteractable()) return;
        _isHovered = true;
        AnimateTo(HoverScale, _hoverColor, HoverDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _isPressed = false;
        AnimateTo(1f, _normalColor, HoverDuration);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_selectable.IsInteractable()) return;
        _isPressed = true;
        AnimateTo(PressScale, _hoverColor, PressDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isPressed) return;
        _isPressed = false;
        if (_isHovered) AnimateTo(HoverScale, _hoverColor, HoverDuration);
        else AnimateTo(1f, _normalColor, HoverDuration);
    }

    private void Update()
    {
        // 画面が隠れた・ボタンが押せなくなった時は、乗せたままの見た目を残さない
        if ((_isHovered || _isPressed) && !_selectable.IsInteractable()) ResetState();
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void OnDestroy()
    {
        CancelMotion();
    }

    private void ResetState()
    {
        _isHovered = false;
        _isPressed = false;
        ApplyImmediate(1f, _normalColor);
    }

    private void AnimateTo(float scale, Color color, float duration)
    {
        CancelMotion();
        float fromScale = _scale;
        Color fromColor = _color;
        _handle = LMotion.Create(0f, 1f, duration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutCubic)
            .Bind(t => Apply(Mathf.Lerp(fromScale, scale, t), Color.Lerp(fromColor, color, t)));
    }

    private void ApplyImmediate(float scale, Color color)
    {
        CancelMotion();
        Apply(scale, color);
    }

    private void Apply(float scale, Color color)
    {
        _scale = scale;
        _color = color;
        transform.localScale = new Vector3(scale, scale, 1f);
        if (_band != null) _band.color = color;
    }

    private void CancelMotion()
    {
        if (_handle.IsActive()) _handle.Cancel();
    }
}
