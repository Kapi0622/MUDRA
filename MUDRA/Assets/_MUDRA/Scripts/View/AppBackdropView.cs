using LitMotion;
using UnityEngine;

/// <summary>
/// App側の画面（Title / Calibration / StageSelect / Result）の背景（UI改修 ステップ1）。
///
/// AppCameraの子に置いたスプライト2枚を、カメラの映す範囲を隙間なく覆う大きさに合わせ、ゆっくり拡大・横移動させる。
/// 画面のCanvasはOverlayなのでパーティクルも画面効果も乗らない。背景をカメラ側に置くのはそのため
/// （火の粉のパーティクルとVolumeも同じくAppCameraの子に置く）。
/// バトル中はGameFlowPresenterがAppCameraごと止めるので、この背景がInGameに映り込むことはない。
///
/// どの背景を出すかはScreenPresenterが画面ごとに決めて呼ぶ。画面の切替は遷移の覆いの裏で起きるので即座に切り替え、
/// 画面を出したまま絵を変える時（ステージ選択のカーソル移動）だけクロスフェードする。
/// </summary>
public class AppBackdropView : MonoBehaviour
{
    // --- クロスフェード ---
    private const float CrossFadeDuration = 0.4f;

    // --- ゆっくりした動き（実時間で進める） ---
    /// <summary>横移動が1往復する秒数</summary>
    private const float DriftPeriod = 40f;
    /// <summary>拡大が1往復する秒数。横移動とずらして単調に見せない</summary>
    private const float BreathePeriod = 27f;
    /// <summary>横移動の振れ幅（画面幅に対する割合）</summary>
    private const float DriftAmplitude = 0.02f;
    /// <summary>拡大の振れ幅（覆う大きさに対する上乗せの最大値）</summary>
    private const float BreatheAmplitude = 0.04f;

    // --- 画面ごとの色味（絵に掛ける色。白で元の絵のまま） ---
    private static readonly Color TitleTint = Color.white;
    private static readonly Color MenuTint = new(0.8f, 0.8f, 0.8f);
    private static readonly Color StageTint = new(0.55f, 0.55f, 0.6f);
    private static readonly Color WinTint = new(1f, 0.85f, 0.6f);
    private static readonly Color LoseTint = new(0.7f, 0.3f, 0.3f);

    [Tooltip("背景を映すカメラ（AppCamera）。正射影を前提に、映す範囲から絵の大きさを決める")]
    [SerializeField] private Camera _camera;
    [Tooltip("手前の絵。新しい絵はこちらに入れてフェードインさせる")]
    [SerializeField] private SpriteRenderer _front;
    [Tooltip("奥の絵。クロスフェードの間、直前の絵を残しておく")]
    [SerializeField] private SpriteRenderer _back;

    [Header("絵")]
    [SerializeField] private Sprite _titleSprite;
    [Tooltip("Calibration・StageSelect・Result で共通に使う絵")]
    [SerializeField] private Sprite _menuSprite;

    private MotionHandle _fadeHandle;

    /// <summary>タイトル画面の背景</summary>
    public void ShowTitle() => SetImmediate(_titleSprite, TitleTint);

    /// <summary>Calibration・StageSelect の背景</summary>
    public void ShowMenu() => SetImmediate(_menuSprite, MenuTint);

    /// <summary>Result の背景。勝ちは金寄り、負けは赤黒く</summary>
    public void ShowResult(bool isWin) => SetImmediate(_menuSprite, isWin ? WinTint : LoseTint);

    /// <summary>
    /// 選択中のステージの絵へクロスフェードする（ステージ選択のカーソル移動用）。
    /// 絵を渡されなかったステージは共通の絵に戻す
    /// </summary>
    public void ShowStage(Sprite stageSprite)
    {
        if (stageSprite == null) CrossFade(_menuSprite, MenuTint);
        else CrossFade(stageSprite, StageTint);
    }

    private void SetImmediate(Sprite sprite, Color tint)
    {
        CancelFade();
        _front.sprite = sprite;
        _front.color = tint;
        _back.sprite = null;
    }

    private void CrossFade(Sprite sprite, Color tint)
    {
        if (_front.sprite == sprite && _front.color == tint) return;

        CancelFade();
        // 今の絵を奥へ回し、新しい絵を手前で透明から出す
        _back.sprite = _front.sprite;
        _back.color = _front.color;
        _front.sprite = sprite;
        _front.color = new Color(tint.r, tint.g, tint.b, 0f);

        _fadeHandle = LMotion.Create(0f, 1f, CrossFadeDuration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutCubic)
            .WithOnComplete(() => _back.sprite = null)
            .Bind(a => _front.color = new Color(tint.r, tint.g, tint.b, a));
    }

    private void LateUpdate()
    {
        float viewHeight = _camera.orthographicSize * 2f;
        float viewWidth = viewHeight * _camera.aspect;

        float time = Time.unscaledTime;
        float drift = Mathf.Sin(time * 2f * Mathf.PI / DriftPeriod);
        float breathe = 0.5f + 0.5f * Mathf.Sin(time * 2f * Mathf.PI / BreathePeriod);

        float offsetX = drift * DriftAmplitude * viewWidth;
        // 横へずらしても端が見えないよう、振れ幅の分だけ広く覆う
        float coverWidth = viewWidth * (1f + 2f * DriftAmplitude);
        float zoom = 1f + breathe * BreatheAmplitude;

        Fit(_front, coverWidth, viewHeight, zoom, offsetX);
        Fit(_back, coverWidth, viewHeight, zoom, offsetX);
    }

    /// <summary>絵の縦横比を保ったまま、指定の範囲を隙間なく覆う大きさにする</summary>
    private static void Fit(SpriteRenderer renderer, float width, float height, float zoom, float offsetX)
    {
        var sprite = renderer.sprite;
        if (sprite == null) return;

        Vector2 size = sprite.bounds.size;
        float scale = Mathf.Max(width / size.x, height / size.y) * zoom;
        var t = renderer.transform;
        t.localScale = new Vector3(scale, scale, 1f);
        t.localPosition = new Vector3(offsetX, 0f, t.localPosition.z);
    }

    private void CancelFade()
    {
        if (_fadeHandle.IsActive()) _fadeHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelFade();
    }
}
