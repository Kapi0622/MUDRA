using System;
using LitMotion;
using TMPro;
using UnityEngine;

/// <summary>
/// Result画面の戦績1項目（項目名と値）。フェードインしながら値を0から数え上げ、数え終わりで値を小さく弾ませる。
/// 主役（大きい）と脇役（小さい）は同じ部品で、出す順と速さはResultViewが決める。
/// ポーズ中のリタイアから来た時に止まらないよう実時間で進める。
/// </summary>
public class ResultStatItemView : MonoBehaviour
{
    private const float FadeDuration = 0.15f;

    // --- 数え終わりの弾み ---
    private const float LandDuration = 0.2f;
    private const float LandScaleFrom = 1.35f;

    private const int SecondsPerMinute = 60;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _labelText;
    [SerializeField] private TextMeshProUGUI _valueText;

    private MotionHandle _fadeHandle;
    private MotionHandle _countHandle;
    private MotionHandle _landHandle;

    /// <summary>
    /// delay秒後に出し、countDuration秒かけて値を数え上げる。数え終わった瞬間にonLandedを呼ぶ（効果音の合図に使う）。
    /// </summary>
    public void Play(ResultStat stat, float delay, float countDuration, Action onLanded)
    {
        Clear();
        _labelText.text = stat.Label;
        _valueText.text = Format(0f, stat.IsTime);

        _fadeHandle = LMotion.Create(0f, 1f, FadeDuration)
            .WithDelay(delay)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(a => _canvasGroup.alpha = a);

        _countHandle = LMotion.Create(0f, stat.Value, countDuration)
            .WithDelay(delay)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutQuad)
            .WithOnComplete(() =>
            {
                _valueText.text = Format(stat.Value, stat.IsTime);
                PlayLand();
                onLanded?.Invoke();
            })
            .Bind(v => _valueText.text = Format(v, stat.IsTime));
    }

    /// <summary>演出を止めて見えなくする</summary>
    public void Clear()
    {
        Cancel();
        _canvasGroup.alpha = 0f;
        _valueText.rectTransform.localScale = Vector3.one;
    }

    /// <summary>演出だけ止める（表示はそのまま）</summary>
    public void Cancel()
    {
        if (_fadeHandle.IsActive()) _fadeHandle.Cancel();
        if (_countHandle.IsActive()) _countHandle.Cancel();
        if (_landHandle.IsActive()) _landHandle.Cancel();
    }

    private void PlayLand()
    {
        var rect = _valueText.rectTransform;
        _landHandle = LMotion.Create(LandScaleFrom, 1f, LandDuration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutBack)
            .Bind(s => rect.localScale = new Vector3(s, s, 1f));
    }

    /// <summary>時間は「分:秒」、それ以外は整数。数え上げの途中は切り捨てて、最後の値を早く見せない</summary>
    private static string Format(float value, bool isTime)
    {
        int total = Mathf.FloorToInt(value);
        return isTime ? $"{total / SecondsPerMinute}:{total % SecondsPerMinute:00}" : total.ToString();
    }

    private void OnDestroy()
    {
        Cancel();
    }
}
