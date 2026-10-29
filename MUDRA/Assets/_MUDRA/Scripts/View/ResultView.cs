using LitMotion;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Result画面に出す内容（B5）。Modelの型を持ち込まないよう表示用の値だけにする。
/// </summary>
public readonly struct ResultScreenData
{
    public readonly bool IsWin;
    public readonly string StageName;

    /// <summary>ランクの文字（S/A/B/C）。敗北時は空</summary>
    public readonly string RankText;

    /// <summary>戦績の項目名と値。同じ並びで1行ずつ出す</summary>
    public readonly string[] StatLabels;
    public readonly string[] StatValues;

    /// <summary>敗北時のヒント。クリア時は空</summary>
    public readonly string HintText;

    /// <summary>ベスト記録を更新したか</summary>
    public readonly bool IsNewRecord;

    public ResultScreenData(bool isWin, string stageName, string rankText,
        string[] statLabels, string[] statValues, string hintText, bool isNewRecord)
    {
        IsWin = isWin;
        StageName = stageName;
        RankText = rankText;
        StatLabels = statLabels;
        StatValues = statValues;
        HintText = hintText;
        IsNewRecord = isNewRecord;
    }
}

/// <summary>
/// Result画面（B5）。勝敗・戦績・ランク（敗北時はヒント）を出し、次の行き先（もう一度 / ステージ選択 / タイトル）の要求を流す。
/// キー: Space / Enter = もう一度、Backspace / Esc = ステージ選択へ。
///
/// 出し方（UI改修 ステップ4）: 遷移の覆いが晴れてから戦績を1行ずつ出し、出きったらランクの判子を押す。
/// 判子が着地した瞬間に画面を短く揺らし、判子の後ろに朱の墨の飛沫を広げる。
/// どれもポーズ中のリタイアから来た時に止まらないよう実時間で進める。
/// </summary>
public class ResultView : ScreenView
{
    private const string WinText = "討伐";
    private const string LoseText = "敗北";
    private static readonly Color WinColor = new Color(1f, 0.85f, 0.3f);
    private static readonly Color LoseColor = new Color(0.8f, 0.15f, 0.15f);

    // --- 戦績を1行ずつ出す ---
    // 遷移の覆いが晴れてから出し始める
    private const float StatDelay = 0.4f;
    private const float StatLineInterval = 0.1f;

    // --- ランクの判子 ---
    // 戦績が出きってから、大きい状態から縮めて押す
    private const float RankStampGap = 0.2f;
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
    [SerializeField] private TextMeshProUGUI _statLabelsText;
    [SerializeField] private TextMeshProUGUI _statValuesText;
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

    private MotionHandle _rankHandle;
    private MotionHandle _statHandle;
    private MotionHandle _shakeHandle;
    private MotionHandle _splashHandle;

    private readonly Subject<Unit> _onRetryRequested = new();
    public Observable<Unit> OnRetryRequested => _onRetryRequested;

    private readonly Subject<Unit> _onStageSelectRequested = new();
    public Observable<Unit> OnStageSelectRequested => _onStageSelectRequested;

    private readonly Subject<Unit> _onTitleRequested = new();
    public Observable<Unit> OnTitleRequested => _onTitleRequested;

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
        _statLabelsText.text = string.Join("\n", data.StatLabels);
        _statValuesText.text = string.Join("\n", data.StatValues);
        float statsDuration = PlayStatLines(data.StatLabels.Length);

        bool hasRank = !string.IsNullOrEmpty(data.RankText);
        _rankGroup.SetActive(hasRank);
        _rankText.text = data.RankText;
        if (hasRank) PlayRankStamp(StatDelay + statsDuration + RankStampGap);

        _hintText.text = data.HintText;
        _newRecordLabel.SetActive(data.IsNewRecord);
        SetVisible(true);
    }

    /// <summary>戦績の項目名と値を、上から1行ずつ見せる。出しきるまでの秒数（遅らせる分を除く）を返す</summary>
    private float PlayStatLines(int lineCount)
    {
        SetVisibleStatLines(0);
        float duration = lineCount * StatLineInterval;
        if (lineCount == 0) return 0f;

        _statHandle = LMotion.Create(0f, lineCount, duration)
            .WithDelay(StatDelay)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            // 1行目を遅れの直後に出すため、切り捨てではなく切り上げで数える
            .Bind(n => SetVisibleStatLines(Mathf.CeilToInt(n)));
        return duration;
    }

    private void SetVisibleStatLines(int count)
    {
        _statLabelsText.maxVisibleLines = count;
        _statValuesText.maxVisibleLines = count;
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
        if (_statHandle.IsActive()) _statHandle.Cancel();
        if (_rankHandle.IsActive()) _rankHandle.Cancel();
        if (_shakeHandle.IsActive()) _shakeHandle.Cancel();
        if (_splashHandle.IsActive()) _splashHandle.Cancel();
        // 終了時の破棄順によっては先に消えていることがある
        if (_shakeTarget != null) _shakeTarget.anchoredPosition = Vector2.zero;
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
        _onRankStamped.Dispose();
    }
}
