using UnityEngine;
using TMPro;
using LitMotion;

/// <summary>
/// ガードの視覚フィードバック（B4）。2種類を分けて出す。
///   構え: ガード受付窓が開いている間、画面の四辺に青い枠を出す（ガード印を組めたことが分かる）
///   成功: 受付中に攻撃を受けたら「防」をポップさせる（ガードが効いたことが分かる）
/// 構えが出ているのに成功が出なければ「タイミングが早すぎた」と読み取れるようにしている。
/// </summary>
public class GuardView : MonoBehaviour
{
    // --- 構え ---
    private const float StanceFadeInDuration = 0.08f;
    private const float StanceFadeOutDuration = 0.2f;

    // --- 成功 ---
    private const float SuccessPopDuration = 0.2f;
    private const float SuccessHoldDuration = 0.35f;
    private const float SuccessFadeOutDuration = 0.3f;
    private const float SuccessScaleFrom = 1.8f;

    [Header("構え（四辺の枠）")]
    [SerializeField] private CanvasGroup _stanceGroup;

    [Header("成功（「防」の文字）")]
    [SerializeField] private CanvasGroup _successGroup;
    [SerializeField] private TextMeshProUGUI _successText;

    private MotionHandle _stanceHandle;
    private MotionHandle _successFadeHandle;
    private MotionHandle _successScaleHandle;

    private void Awake()
    {
        _stanceGroup.alpha = 0f;
        _successGroup.alpha = 0f;
    }

    /// <summary>
    /// 構え枠の表示を切り替える。受付窓の開閉に合わせてPresenterが呼ぶ。
    /// </summary>
    public void SetStance(bool isGuarding)
    {
        if (_stanceHandle.IsActive()) _stanceHandle.Cancel();

        float target = isGuarding ? 1f : 0f;
        float duration = isGuarding ? StanceFadeInDuration : StanceFadeOutDuration;
        _stanceHandle = LMotion.Create(_stanceGroup.alpha, target, duration)
            .Bind(a => _stanceGroup.alpha = a);
    }

    /// <summary>
    /// ガード成功の演出。「防」が大きい状態から縮んで定位置に収まり、少し残って消える。
    /// </summary>
    public void PlayGuardSuccess()
    {
        CancelSuccessMotions();

        var textTransform = _successText.rectTransform;
        _successScaleHandle = LMotion.Create(SuccessScaleFrom, 1f, SuccessPopDuration)
            .WithEase(Ease.OutBack)
            .Bind(s => textTransform.localScale = new Vector3(s, s, 1f));

        _successGroup.alpha = 1f;
        _successFadeHandle = LMotion.Create(1f, 0f, SuccessFadeOutDuration)
            .WithEase(Ease.InQuad)
            .WithDelay(SuccessPopDuration + SuccessHoldDuration)
            .Bind(a => _successGroup.alpha = a);
    }

    private void CancelSuccessMotions()
    {
        if (_successFadeHandle.IsActive()) _successFadeHandle.Cancel();
        if (_successScaleHandle.IsActive()) _successScaleHandle.Cancel();
    }

    private void OnDestroy()
    {
        if (_stanceHandle.IsActive()) _stanceHandle.Cancel();
        CancelSuccessMotions();
    }
}
