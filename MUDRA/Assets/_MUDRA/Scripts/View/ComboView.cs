using UnityEngine;
using TMPro;
using LitMotion;

/// <summary>
/// コンボ数の表示（B4）。2コンボ以上で「n 連」を出し、増えるたびに弾ませる。
/// コンボが切れた（0に戻った）時は下に落ちながら消し、暴発でコンボを失ったことを伝える。
/// 1コンボはまだ倍率が乗っていない（コンボ倍率は2発目から効く）ので出さない。
/// </summary>
public class ComboView : MonoBehaviour
{
    // --- 演出定数 ---
    private const int MinDisplayCount = 2;
    private const string Suffix = " 連";
    private const float PunchScale = 1.4f;
    private const float PunchDuration = 0.25f;
    private const float ShowFadeDuration = 0.1f;
    private const float BreakDuration = 0.5f;
    private const float BreakFallDistance = 60f;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _comboText;

    private Vector2 _basePosition;
    private bool _isShowing;
    private MotionHandle _scaleHandle;
    private MotionHandle _fadeHandle;
    private MotionHandle _moveHandle;
    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 初期化を1回だけ行う。ComboCountは購読した瞬間に現在値を流すため、
    /// BattleInitializer.Awake内の配線でこのViewのAwakeより先にSetComboが呼ばれることがある。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _basePosition = _comboText.rectTransform.anchoredPosition;
        _canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// コンボ数を反映する。増えたら弾ませ、0に戻ったら崩して消す。
    /// </summary>
    public void SetCombo(int count)
    {
        EnsureInitialized();

        if (count >= MinDisplayCount)
        {
            Show(count);
            return;
        }

        if (_isShowing && count == 0)
        {
            Break();
            return;
        }

        // デバッグのリセット等で1以下に戻った場合は静かに消す
        if (_isShowing) Hide();
    }

    private void Show(int count)
    {
        CancelAllMotions();
        _isShowing = true;

        _comboText.text = count + Suffix;
        var rect = _comboText.rectTransform;
        rect.anchoredPosition = _basePosition;

        _fadeHandle = LMotion.Create(_canvasGroup.alpha, 1f, ShowFadeDuration)
            .Bind(a => _canvasGroup.alpha = a);

        _scaleHandle = LMotion.Create(PunchScale, 1f, PunchDuration)
            .WithEase(Ease.OutBack)
            .Bind(s => rect.localScale = new Vector3(s, s, 1f));
    }

    /// <summary>コンボが切れた演出。下に落ちながらフェードアウトする。</summary>
    private void Break()
    {
        CancelAllMotions();
        _isShowing = false;

        var rect = _comboText.rectTransform;
        _moveHandle = LMotion.Create(0f, -BreakFallDistance, BreakDuration)
            .WithEase(Ease.InQuad)
            .Bind(y => rect.anchoredPosition = _basePosition + new Vector2(0f, y));

        _fadeHandle = LMotion.Create(_canvasGroup.alpha, 0f, BreakDuration)
            .WithEase(Ease.InQuad)
            .Bind(a => _canvasGroup.alpha = a);
    }

    private void Hide()
    {
        CancelAllMotions();
        _isShowing = false;
        _canvasGroup.alpha = 0f;
    }

    private void CancelAllMotions()
    {
        if (_scaleHandle.IsActive()) _scaleHandle.Cancel();
        if (_fadeHandle.IsActive()) _fadeHandle.Cancel();
        if (_moveHandle.IsActive()) _moveHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelAllMotions();
    }
}
