using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LitMotion;

/// <summary>
/// 判定中の手印の表示。カメラ映像の近くに「今読まれている印の名前」と「確定までのゲージ」を出す。
/// 確定するまで何の形として読まれているか分からず、組み直すべきか待つべきか判断できなかったため追加した。
///   候補: 候補色の印名と、静止して積算した分だけ溜まるゲージ
///   確定: ゲージを満タン・確定色にして印名をポップさせ、少し残してから消える
/// 確定後に同じ印を保持し続けても再表示しない（Presenterが進み具合1の候補をHideに回す）。
/// </summary>
public class HandSignStatusView : MonoBehaviour
{
    // --- 色 ---
    private static readonly Color CandidateColor = new Color(0.85f, 0.85f, 0.85f);
    // SequenceGuideView の ConfirmedColor と同じ値
    private static readonly Color ConfirmedColor = new Color(0.3f, 1f, 0.5f);

    // --- 候補 ---
    private const float ShowFadeInDuration = 0.05f;
    private const float HideFadeOutDuration = 0.1f;

    /// <summary>
    /// ゲージが目標値へ追いつく速さ（満タン分/秒）。
    /// 確定時間は0.15秒と短く、進み具合は推論ごと（約35回/秒）にしか届かないため、
    /// 段差が見えない程度に補間しつつ、遅れて見えないよう速めにしている。
    /// </summary>
    private const float GaugeFollowSpeed = 12f;

    // --- 確定 ---
    private const float PunchScaleAmount = 1.3f;
    private const float PunchDuration = 0.15f;
    private const float ConfirmHoldDuration = 0.5f;
    private const float ConfirmFadeOutDuration = 0.2f;

    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TextMeshProUGUI _signText;
    [Tooltip("Image Type = Filled（Horizontal）にしておく")]
    [SerializeField] private Image _gaugeFill;

    private float _gaugeTarget;

    private MotionHandle _fadeHandle;
    private MotionHandle _punchHandle;

    /// <summary>確定演出（ポップ→保持→フェード）の再生中か。再生中はHideで切らない</summary>
    private bool IsConfirmPlaying => _fadeHandle.IsActive() && _isConfirmFade;
    private bool _isConfirmFade;

    private void Awake()
    {
        _group.alpha = 0f;
        _gaugeFill.fillAmount = 0f;
    }

    private void Update()
    {
        _gaugeFill.fillAmount = Mathf.MoveTowards(_gaugeFill.fillAmount, _gaugeTarget, GaugeFollowSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 確定前の候補を表示する。候補が切り替わったときと、進み具合が変わったときにPresenterが呼ぶ。
    /// </summary>
    /// <param name="signName">印の表示名</param>
    /// <param name="progress">確定までの進み具合（0〜1）</param>
    public void ShowCandidate(string signName, float progress)
    {
        // 別の印に切り替わったらゲージは0から溜め直す（前の印の残りから減っていくように見せない）
        if (_signText.text != signName || _signText.color != CandidateColor)
        {
            _gaugeFill.fillAmount = 0f;
        }

        CancelPunch();
        _signText.rectTransform.localScale = Vector3.one;
        _signText.text = signName;
        _signText.color = CandidateColor;
        _gaugeFill.color = CandidateColor;
        _gaugeTarget = progress;

        FadeTo(1f, ShowFadeInDuration, isConfirmFade: false);
    }

    /// <summary>
    /// 確定の演出。ゲージを満タン・確定色にし、印名をポップさせ、少し残して消える。
    /// </summary>
    public void PlayConfirm(string signName)
    {
        CancelPunch();

        _signText.text = signName;
        _signText.color = ConfirmedColor;
        _gaugeFill.color = ConfirmedColor;
        _gaugeTarget = 1f;
        _gaugeFill.fillAmount = 1f;

        var textTransform = _signText.rectTransform;
        _punchHandle = LMotion.Create(PunchScaleAmount, 1f, PunchDuration)
            .WithEase(Ease.OutBack)
            .Bind(s => textTransform.localScale = new Vector3(s, s, 1f));

        CancelFade();
        _group.alpha = 1f;
        _isConfirmFade = true;
        _fadeHandle = LMotion.Create(1f, 0f, ConfirmFadeOutDuration)
            .WithEase(Ease.InQuad)
            .WithDelay(PunchDuration + ConfirmHoldDuration)
            .Bind(a => _group.alpha = a);
    }

    /// <summary>
    /// 表示を消す。確定演出の再生中は何もしない（確定の表示を途中で切らないため）。
    /// </summary>
    public void Hide()
    {
        if (IsConfirmPlaying) return;
        FadeTo(0f, HideFadeOutDuration, isConfirmFade: false);
    }

    private void FadeTo(float target, float duration, bool isConfirmFade)
    {
        CancelFade();
        _isConfirmFade = isConfirmFade;
        if (Mathf.Approximately(_group.alpha, target)) return;

        _fadeHandle = LMotion.Create(_group.alpha, target, duration)
            .Bind(a => _group.alpha = a);
    }

    private void CancelFade()
    {
        if (_fadeHandle.IsActive()) _fadeHandle.Cancel();
    }

    private void CancelPunch()
    {
        if (_punchHandle.IsActive()) _punchHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelFade();
        CancelPunch();
    }
}
