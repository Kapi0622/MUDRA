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
/// </summary>
public class ResultView : ScreenView
{
    private const string WinText = "討伐";
    private const string LoseText = "敗北";
    private static readonly Color WinColor = new Color(1f, 0.85f, 0.3f);
    private static readonly Color LoseColor = new Color(0.8f, 0.15f, 0.15f);

    // --- ランクの判子 ---
    // 遷移の覆いが晴れきってから押されるよう、少し遅らせて大きい状態から縮める（実時間で進める）
    private const float RankStampDelay = 0.6f;
    private const float RankStampDuration = 0.3f;
    private const float RankStampScaleFrom = 2.5f;

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

    private MotionHandle _rankHandle;

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
        _resultText.text = data.IsWin ? WinText : LoseText;
        _resultText.color = data.IsWin ? WinColor : LoseColor;
        _stageNameText.text = data.StageName;
        _statLabelsText.text = string.Join("\n", data.StatLabels);
        _statValuesText.text = string.Join("\n", data.StatValues);

        bool hasRank = !string.IsNullOrEmpty(data.RankText);
        _rankGroup.SetActive(hasRank);
        _rankText.text = data.RankText;
        if (hasRank) PlayRankStamp();

        _hintText.text = data.HintText;
        _newRecordLabel.SetActive(data.IsNewRecord);
        SetVisible(true);
    }

    private void PlayRankStamp()
    {
        if (_rankHandle.IsActive()) _rankHandle.Cancel();

        var rect = _rankText.rectTransform;
        var color = _rankText.color;
        // 遅らせている間は見せない
        _rankText.color = new Color(color.r, color.g, color.b, 0f);
        _rankHandle = LMotion.Create(0f, 1f, RankStampDuration)
            .WithDelay(RankStampDelay)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutCubic)
            .WithOnComplete(() => _onRankStamped.OnNext(Unit.Default))
            .Bind(t =>
            {
                float scale = Mathf.Lerp(RankStampScaleFrom, 1f, t);
                rect.localScale = new Vector3(scale, scale, 1f);
                _rankText.color = new Color(color.r, color.g, color.b, t);
            });
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
        if (_rankHandle.IsActive()) _rankHandle.Cancel();
        _onRetryRequested.Dispose();
        _onStageSelectRequested.Dispose();
        _onTitleRequested.Dispose();
        _onRankStamped.Dispose();
    }
}
