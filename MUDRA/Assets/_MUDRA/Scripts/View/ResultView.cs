using LitMotion;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Result画面の戦績1項目の表示用の値。数え上げるので文字列ではなく数値で持つ。
/// </summary>
public readonly struct ResultStat
{
    public readonly string Label;
    public readonly float Value;

    /// <summary>true: 秒数を「分:秒」で出す / false: 整数で出す</summary>
    public readonly bool IsTime;

    public ResultStat(string label, float value, bool isTime = false)
    {
        Label = label;
        Value = value;
        IsTime = isTime;
    }
}

/// <summary>
/// Result画面に出す内容（B5）。Modelの型を持ち込まないよう表示用の値だけにする。
/// </summary>
public readonly struct ResultScreenData
{
    public readonly bool IsWin;
    public readonly string StageName;

    /// <summary>ランクの文字（S/A/B/C）。敗北時は空</summary>
    public readonly string RankText;

    /// <summary>大きく出す主役の項目（時間・受けたダメージ）</summary>
    public readonly ResultStat[] PrimaryStats;

    /// <summary>小さく並べる脇役の項目</summary>
    public readonly ResultStat[] SecondaryStats;

    /// <summary>ランクの点数をゲージの長さ（0〜1）に直したもの。敗北時は使わない</summary>
    public readonly float GaugeRatio;

    /// <summary>B/A/Sの各しきい値のゲージ上の位置（0〜1）。この並びで目盛りに当てる</summary>
    public readonly float[] GaugeMarks;

    /// <summary>敗北時のヒント。クリア時は空</summary>
    public readonly string HintText;

    /// <summary>ベスト記録を更新したか</summary>
    public readonly bool IsNewRecord;

    public ResultScreenData(bool isWin, string stageName, string rankText,
        ResultStat[] primaryStats, ResultStat[] secondaryStats, float gaugeRatio, float[] gaugeMarks,
        string hintText, bool isNewRecord)
    {
        IsWin = isWin;
        StageName = stageName;
        RankText = rankText;
        PrimaryStats = primaryStats;
        SecondaryStats = secondaryStats;
        GaugeRatio = gaugeRatio;
        GaugeMarks = gaugeMarks;
        HintText = hintText;
        IsNewRecord = isNewRecord;
    }
}

/// <summary>
/// Result画面（B5）。勝敗・戦績・ランク（敗北時はヒント）を出し、次の行き先（もう一度 / ステージ選択 / タイトル）の要求を流す。
/// キー: Space / Enter = もう一度、Backspace / Esc = ステージ選択へ。
///
/// 出し方: 遷移の覆いが晴れてから、主役の項目（時間・受けたダメージ）を大きく1つずつ数え上げ、
/// 続けて脇役の項目を間隔を詰めて数え上げる。クリア時はそのあとランクの点数ゲージを伸ばし、
/// 伸びきったらランクの判子を押す（着地で画面を短く揺らし、判子の後ろに朱の墨の飛沫を広げる）。
/// 点数は文字では出さず、ゲージの長さとB/A/Sの目盛りだけで見せる（項目ごとの加減点を並べると紛らわしいため）。
/// 敗北時はゲージと判子を出さず、脇役が出きってからヒントを出す。
/// どれもポーズ中のリタイアから来た時に止まらないよう実時間で進める。
/// </summary>
public class ResultView : ScreenView
{
    private const string WinText = "討伐";
    private const string LoseText = "敗北";
    private static readonly Color WinColor = new Color(1f, 0.85f, 0.3f);
    private static readonly Color LoseColor = new Color(0.8f, 0.15f, 0.15f);

    // --- 戦績 ---
    // 遷移の覆いが晴れてから出し始める
    private const float StatDelay = 0.4f;
    private const float PrimaryInterval = 0.45f;
    private const float PrimaryCountDuration = 0.6f;
    // 主役が出きってから脇役に移るまで
    private const float SecondaryGap = 0.15f;
    private const float SecondaryInterval = 0.15f;
    private const float SecondaryCountDuration = 0.35f;

    // --- 点数ゲージ（クリア時） ---
    private const float GaugeGap = 0.25f;
    private const float GaugeDuration = 0.9f;
    // 届いていない目盛りの濃さ
    private const float GaugeMarkDimAlpha = 0.35f;

    // --- ヒント（敗北時） ---
    private const float HintGap = 0.25f;
    private const float HintFadeDuration = 0.4f;

    // --- ランクの判子 ---
    // ゲージが伸びきってから、大きい状態から縮めて押す
    private const float RankStampGap = 0.15f;
    private const float RankStampDuration = 0.3f;
    private const float RankStampScaleFrom = 2.5f;

    // --- 判子の着地 ---
    private const float ShakeStrength = 14f;
    private const float ShakeDuration = 0.3f;
    private const int ShakeFrequency = 20;
    private const float SplashDuration = 0.35f;
    private const float SplashScaleFrom = 0.4f;
    // 金のランク文字が映える朱色
    private static readonly Color SplashColor = new Color(0.7f, 0.1f, 0.06f, 0.9f);

    [SerializeField] private TextMeshProUGUI _resultText;
    [SerializeField] private TextMeshProUGUI _stageNameText;

    [Header("戦績")]
    [Tooltip("大きく出す主役の項目（時間・受けたダメージ）。ResultScreenData.PrimaryStatsと同じ並び")]
    [SerializeField] private ResultStatItemView[] _primaryItems;
    [Tooltip("小さく並べる脇役の項目。ResultScreenData.SecondaryStatsと同じ並び")]
    [SerializeField] private ResultStatItemView[] _secondaryItems;

    [Header("点数ゲージ（クリア時）")]
    [Tooltip("ゲージ一式の親。敗北時は隠す")]
    [SerializeField] private GameObject _gaugeGroup;
    [Tooltip("Image Type = Filled / Horizontal")]
    [SerializeField] private Image _gaugeFill;
    [Tooltip("B/A/Sの目盛り。この並びでしきい値の位置へ動かし、ゲージが届いたら濃くする")]
    [SerializeField] private CanvasGroup[] _gaugeMarks;

    [Header("ランク")]
    [Tooltip("ランクと「評価」の見出しをまとめた親。敗北時は隠す")]
    [SerializeField] private GameObject _rankGroup;
    [SerializeField] private TextMeshProUGUI _rankText;
    [SerializeField] private TextMeshProUGUI _hintText;
    [SerializeField] private GameObject _newRecordLabel;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _stageSelectButton;
    [SerializeField] private Button _titleButton;

    [Header("演出（UI改修 ステップ4）")]
    [Tooltip("判子の着地で揺らす範囲（Result画面のルート）")]
    [SerializeField] private RectTransform _shakeTarget;
    [Tooltip("判子の後ろに広がる墨の飛沫。RankGroupの中、Rankより奥に置く")]
    [SerializeField] private Image _inkSplash;

    private MotionHandle _gaugeWaitHandle;
    private MotionHandle _gaugeHandle;
    private MotionHandle _hintHandle;
    private MotionHandle _rankHandle;
    private MotionHandle _shakeHandle;
    private MotionHandle _splashHandle;

    private readonly Subject<Unit> _onRetryRequested = new();
    public Observable<Unit> OnRetryRequested => _onRetryRequested;

    private readonly Subject<Unit> _onStageSelectRequested = new();
    public Observable<Unit> OnStageSelectRequested => _onStageSelectRequested;

    private readonly Subject<Unit> _onTitleRequested = new();
    public Observable<Unit> OnTitleRequested => _onTitleRequested;

    private readonly Subject<Unit> _onStatRevealed = new();
    /// <summary>戦績の1項目が数え終わった。間合いはこのViewが決めるので、音はこの合図で鳴らしてもらう</summary>
    public Observable<Unit> OnStatRevealed => _onStatRevealed;

    private readonly Subject<Unit> _onGaugeStarted = new();
    /// <summary>点数ゲージが伸び始めた（クリア時のみ）</summary>
    public Observable<Unit> OnGaugeStarted => _onGaugeStarted;

    private readonly Subject<Unit> _onRankStamped = new();
    /// <summary>ランクのハンコが押された（縮みきった瞬間）。遅れと尺はこのViewが決めるので、音はこの合図で鳴らしてもらう（B7）</summary>
    public Observable<Unit> OnRankStamped => _onRankStamped;

    private void Awake()
    {
        _retryButton.onClick.AddListener(() => _onRetryRequested.OnNext(Unit.Default));
        _stageSelectButton.onClick.AddListener(() => _onStageSelectRequested.OnNext(Unit.Default));
        _titleButton.onClick.AddListener(() => _onTitleRequested.OnNext(Unit.Default));
    }

    public void Show(ResultScreenData data)
    {
        CancelMotions();

        _resultText.text = data.IsWin ? WinText : LoseText;
        _resultText.color = data.IsWin ? WinColor : LoseColor;
        _stageNameText.text = data.StageName;

        float t = StatDelay;
        t = PlayStats(_primaryItems, data.PrimaryStats, t, PrimaryInterval, PrimaryCountDuration);
        t = PlayStats(_secondaryItems, data.SecondaryStats, t + SecondaryGap, SecondaryInterval, SecondaryCountDuration);

        bool hasRank = !string.IsNullOrEmpty(data.RankText);
        _rankGroup.SetActive(hasRank);
        _gaugeGroup.SetActive(hasRank);
        _rankText.text = data.RankText;
        if (hasRank)
        {
            float gaugeStart = t + GaugeGap;
            PlayGauge(data.GaugeRatio, data.GaugeMarks, gaugeStart);
            PlayRankStamp(gaugeStart + GaugeDuration + RankStampGap);
        }

        _hintText.text = data.HintText;
        _hintText.alpha = 0f;
        if (!string.IsNullOrEmpty(data.HintText)) PlayHint(t + HintGap);

        _newRecordLabel.SetActive(data.IsNewRecord);
        SetVisible(true);
    }

    /// <summary>
    /// 項目を順に出して数え上げる。start秒後に1つ目を出し、interval秒ずつずらす。
    /// 最後の項目が数え終わる時刻を返す（項目が無ければstartのまま）
    /// </summary>
    private float PlayStats(ResultStatItemView[] items, ResultStat[] stats, float start, float interval, float countDuration)
    {
        int count = stats != null ? Mathf.Min(items.Length, stats.Length) : 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (i < count)
                items[i].Play(stats[i], start + i * interval, countDuration, () => _onStatRevealed.OnNext(Unit.Default));
            else
                items[i].Clear();
        }
        return count > 0 ? start + (count - 1) * interval + countDuration : start;
    }

    /// <summary>目盛りをしきい値の位置に置き、start秒後にゲージを点数まで伸ばす。届いた目盛りから濃くする</summary>
    private void PlayGauge(float ratio, float[] marks, float start)
    {
        int markCount = marks != null ? Mathf.Min(_gaugeMarks.Length, marks.Length) : 0;
        for (int i = 0; i < _gaugeMarks.Length; i++)
        {
            bool used = i < markCount;
            _gaugeMarks[i].gameObject.SetActive(used);
            if (!used) continue;

            var rect = (RectTransform)_gaugeMarks[i].transform;
            rect.anchorMin = new Vector2(marks[i], rect.anchorMin.y);
            rect.anchorMax = new Vector2(marks[i], rect.anchorMax.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
        }
        SetGaugeFill(0f, marks, markCount);

        // 伸び始めに合図を出すため、待ちと伸びを分ける
        _gaugeWaitHandle = LMotion.Create(0f, 1f, start)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() =>
            {
                _onGaugeStarted.OnNext(Unit.Default);
                _gaugeHandle = LMotion.Create(0f, Mathf.Clamp01(ratio), GaugeDuration)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .WithEase(Ease.OutCubic)
                    .Bind(fill => SetGaugeFill(fill, marks, markCount));
            })
            .Bind(_ => { });
    }

    private void SetGaugeFill(float fill, float[] marks, int markCount)
    {
        _gaugeFill.fillAmount = fill;
        for (int i = 0; i < markCount; i++)
            _gaugeMarks[i].alpha = fill >= marks[i] ? 1f : GaugeMarkDimAlpha;
    }

    private void PlayHint(float delay)
    {
        _hintHandle = LMotion.Create(0f, 1f, HintFadeDuration)
            .WithDelay(delay)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(a => _hintText.alpha = a);
    }

    private void PlayRankStamp(float delay)
    {
        var rect = _rankText.rectTransform;
        var color = _rankText.color;
        // 遅らせている間は見せない
        _rankText.color = new Color(color.r, color.g, color.b, 0f);
        _inkSplash.color = new Color(SplashColor.r, SplashColor.g, SplashColor.b, 0f);
        _rankHandle = LMotion.Create(0f, 1f, RankStampDuration)
            .WithDelay(delay)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutCubic)
            .WithOnComplete(() =>
            {
                PlayStampImpact();
                _onRankStamped.OnNext(Unit.Default);
            })
            .Bind(t =>
            {
                float scale = Mathf.Lerp(RankStampScaleFrom, 1f, t);
                rect.localScale = new Vector3(scale, scale, 1f);
                _rankText.color = new Color(color.r, color.g, color.b, t);
            });
    }

    /// <summary>判子の着地: 画面を短く揺らし、判子の後ろに飛沫を広げる</summary>
    private void PlayStampImpact()
    {
        _shakeHandle = LMotion.Shake.Create(Vector2.zero, new Vector2(ShakeStrength, ShakeStrength), ShakeDuration)
            .WithFrequency(ShakeFrequency)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() => _shakeTarget.anchoredPosition = Vector2.zero)
            .Bind(offset => _shakeTarget.anchoredPosition = offset);

        var splashRect = _inkSplash.rectTransform;
        _splashHandle = LMotion.Create(0f, 1f, SplashDuration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutExpo)
            .Bind(t =>
            {
                float scale = Mathf.Lerp(SplashScaleFrom, 1f, t);
                splashRect.localScale = new Vector3(scale, scale, 1f);
                _inkSplash.color = new Color(SplashColor.r, SplashColor.g, SplashColor.b, SplashColor.a * Mathf.Clamp01(t * 3f));
            });
    }

    private void CancelMotions()
    {
        CancelItems(_primaryItems);
        CancelItems(_secondaryItems);
        if (_gaugeWaitHandle.IsActive()) _gaugeWaitHandle.Cancel();
        if (_gaugeHandle.IsActive()) _gaugeHandle.Cancel();
        if (_hintHandle.IsActive()) _hintHandle.Cancel();
        if (_rankHandle.IsActive()) _rankHandle.Cancel();
        if (_shakeHandle.IsActive()) _shakeHandle.Cancel();
        if (_splashHandle.IsActive()) _splashHandle.Cancel();
        // 終了時の破棄順によっては先に消えていることがある
        if (_shakeTarget != null) _shakeTarget.anchoredPosition = Vector2.zero;
    }

    protected override void OnHidden()
    {
        CancelMotions();
    }

    private static void CancelItems(ResultStatItemView[] items)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            // 終了時の破棄順によっては先に消えていることがある
            if (item != null) item.Cancel();
        }
    }

    private void Update()
    {
        if (!IsInteractable) return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            _onRetryRequested.OnNext(Unit.Default);
        else if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
            _onStageSelectRequested.OnNext(Unit.Default);
    }

    private void OnDestroy()
    {
        CancelMotions();
        _onRetryRequested.Dispose();
        _onStageSelectRequested.Dispose();
        _onTitleRequested.Dispose();
        _onStatRevealed.Dispose();
        _onGaugeStarted.Dispose();
        _onRankStamped.Dispose();
    }
}
