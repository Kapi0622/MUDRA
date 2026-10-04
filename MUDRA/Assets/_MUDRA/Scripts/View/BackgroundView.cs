using UnityEngine;
using LitMotion;

/// <summary>
/// バトル背景のView（B4）。道中/ボスの背景切替と、セクション間の前進演出を担う。
///
/// 前進演出は「奥へズームしながら暗転 → 元の画角で明転」の2段。
/// 背景は1枚絵（奥行きのある廊下・部屋の絵を想定）なので、横スクロールではなく
/// ズームで「奥へ進んだ」ことを表す。暗転を挟むことで、次の部屋に着いたように見せる。
///
/// 背景スプライトが未設定のステージでは既定の背景を使う。
/// 未設定のままだと道中とボス戦の区別が付かなくなるため、ボス戦では既定背景を暗い赤に沈めて区別する。
/// </summary>
public class BackgroundView : MonoBehaviour
{
    // --- 前進演出 ---
    /// <summary>前進演出の全体の長さ。SectionProgressManager.TransitionDurationはこれと撃破演出の合計以上にすること</summary>
    public const float AdvanceDuration = 1.2f;
    private const float AdvanceZoomPortion = 0.75f;     // 全体のうちズーム＋暗転に使う割合（残りが明転）
    private const float AdvanceZoomScale = 1.3f;
    private const float AdvanceDimBrightness = 0.15f;

    // --- 背景切替 ---
    private const float SwitchFadeDuration = 0.3f;

    private static readonly Color BossFallbackTint = new Color(0.65f, 0.4f, 0.4f);

    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Camera _camera;
    [Tooltip("StageDataの背景が未設定のときに使う既定背景")]
    [SerializeField] private Sprite _fallbackSprite;

    // --- 合成前の各成分 ---
    private Color _tint = Color.white;
    private float _brightness = 1f;
    private float _zoom = 1f;
    private float _coverScale = 1f;

    private MotionHandle _advanceHandle;
    private MotionHandle _switchHandle;

    /// <summary>
    /// 背景を切り替える。前と違う背景になる場合だけ暗い状態から明転させる。
    /// </summary>
    public void SetBackground(Sprite sprite, bool isBoss)
    {
        var nextSprite = sprite != null ? sprite : _fallbackSprite;
        var nextTint = sprite == null && isBoss ? BossFallbackTint : Color.white;

        bool changed = nextSprite != _spriteRenderer.sprite || nextTint != _tint;

        _spriteRenderer.sprite = nextSprite;
        _tint = nextTint;
        _coverScale = CalculateCoverScale(nextSprite);

        if (!changed)
        {
            ApplyVisual();
            return;
        }

        TryCancel(ref _advanceHandle);
        TryCancel(ref _switchHandle);
        _zoom = 1f;
        _switchHandle = LMotion.Create(AdvanceDimBrightness, 1f, SwitchFadeDuration)
            .WithEase(Ease.OutQuad)
            .Bind(b =>
            {
                _brightness = b;
                ApplyVisual();
            });
    }

    /// <summary>
    /// 前進演出を再生する。撃破演出の後に始めたいので開始を遅らせられるようにしている。
    /// </summary>
    public void PlayAdvance(float delay)
    {
        TryCancel(ref _advanceHandle);

        _advanceHandle = LMotion.Create(0f, 1f, AdvanceDuration)
            .WithDelay(delay)
            .Bind(t =>
            {
                if (t < AdvanceZoomPortion)
                {
                    // 前半: 奥へズームしながら暗くなる
                    float v = t / AdvanceZoomPortion;
                    float eased = v * v;
                    _zoom = Mathf.Lerp(1f, AdvanceZoomScale, eased);
                    _brightness = Mathf.Lerp(1f, AdvanceDimBrightness, eased);
                }
                else
                {
                    // 後半: 元の画角に戻して明るくする（次の場所に着いた）
                    float v = (t - AdvanceZoomPortion) / (1f - AdvanceZoomPortion);
                    _zoom = 1f;
                    _brightness = Mathf.Lerp(AdvanceDimBrightness, 1f, v);
                }
                ApplyVisual();
            });
    }

    private void ApplyVisual()
    {
        var color = _tint * _brightness;
        color.a = 1f;
        _spriteRenderer.color = color;

        float scale = _coverScale * _zoom;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// カメラの表示範囲を隙間なく覆う拡大率（cover fit）を求める。
    /// はみ出した分は上下か左右が切れる。
    /// </summary>
    private float CalculateCoverScale(Sprite sprite)
    {
        if (sprite == null || _camera == null) return 1f;

        float viewHeight = _camera.orthographicSize * 2f;
        float viewWidth = viewHeight * _camera.aspect;
        var size = sprite.bounds.size;
        return Mathf.Max(viewWidth / size.x, viewHeight / size.y);
    }

    /// <summary>
    /// MotionHandleは構造体のため、IsActive()で有効性を確認してからCancelする。
    /// </summary>
    private static void TryCancel(ref MotionHandle handle)
    {
        if (handle.IsActive()) handle.Cancel();
    }

    private void OnDestroy()
    {
        TryCancel(ref _advanceHandle);
        TryCancel(ref _switchHandle);
    }
}
