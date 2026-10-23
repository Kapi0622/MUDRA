using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Calibration画面（B6）。カメラ映像（CameraPreviewViewが左側に大きく出す）の右側に、
/// 状態の一文・認識テストの印一覧・「次へ」「戻る」を並べる。カメラが使えない時はエラーパネルで覆う。
///
/// 何を表示するか（状態の文言・どの印を並べるか・次へを押せるか）はPresenterが決める。ここは表示と要求の通知だけ。
/// </summary>
public class CalibrationView : ScreenView
{
    // --- 認識テストの印の色 ---
    private static readonly Color UntestedCellColor = new(1f, 1f, 1f, 0.08f);
    private static readonly Color UntestedTextColor = new(1f, 1f, 1f, 0.35f);
    // SequenceGuideView / HandSignStatusView の ConfirmedColor と同じ値
    private static readonly Color TestedColor = new(0.3f, 1f, 0.5f);
    private static readonly Color TestedCellColor = new(0.3f, 1f, 0.5f, 0.25f);

    [Header("状態")]
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("認識テスト")]
    [SerializeField] private RectTransform _signCellContainer;
    [Tooltip("印1つ分のマス。非アクティブにしておき、印の数だけ複製する。子にTextMeshProUGUIを1つ持つ")]
    [SerializeField] private Image _signCellTemplate;

    [Header("ボタン")]
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _backButton;

    [Header("カメラのエラー")]
    [SerializeField] private CanvasGroup _errorPanel;
    [SerializeField] private TextMeshProUGUI _errorTitleText;
    [SerializeField] private TextMeshProUGUI _errorDetailText;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _errorBackButton;

    [Header("手のガイド")]
    [Tooltip("「手が認識されません」ガイド（B6）。認識テストの上に重ねる")]
    [SerializeField] private HandGuideView _handGuide;

    private readonly List<(Image cell, TextMeshProUGUI label)> _cells = new();
    private bool _isErrorShown;

    private readonly Subject<Unit> _onNextRequested = new();
    /// <summary>「次へ」ボタン、またはSpace / Enter（押せる時だけ）</summary>
    public Observable<Unit> OnNextRequested => _onNextRequested;

    private readonly Subject<Unit> _onBackRequested = new();
    /// <summary>「戻る」ボタン、エラーパネルの「タイトルへ」、またはEsc / Backspace</summary>
    public Observable<Unit> OnBackRequested => _onBackRequested;

    private readonly Subject<Unit> _onRetryRequested = new();
    /// <summary>エラーパネルの「再試行」</summary>
    public Observable<Unit> OnRetryRequested => _onRetryRequested;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private readonly Subject<Unit> _onDebugSkipRequested = new();
    /// <summary>F2。カメラが無くても先へ進む（キーボードモードで通しの確認をするため）</summary>
    public Observable<Unit> OnDebugSkipRequested => _onDebugSkipRequested;
#endif

    private void Awake()
    {
        _signCellTemplate.gameObject.SetActive(false);

        _nextButton.onClick.AddListener(() => _onNextRequested.OnNext(Unit.Default));
        _backButton.onClick.AddListener(() => _onBackRequested.OnNext(Unit.Default));
        _retryButton.onClick.AddListener(() => _onRetryRequested.OnNext(Unit.Default));
        _errorBackButton.onClick.AddListener(() => _onBackRequested.OnNext(Unit.Default));
    }

    public void Show()
    {
        SetVisible(true);
    }

    /// <summary>認識テストに並べる印。並べ直すと点灯は消える</summary>
    public void SetSigns(IReadOnlyList<string> signNames)
    {
        foreach (var (cell, _) in _cells) Destroy(cell.gameObject);
        _cells.Clear();

        foreach (var signName in signNames)
        {
            var cell = Instantiate(_signCellTemplate, _signCellContainer);
            cell.gameObject.SetActive(true);
            var label = cell.GetComponentInChildren<TextMeshProUGUI>();
            label.text = signName;
            _cells.Add((cell, label));
        }
        ClearTested();
    }

    /// <summary>認識テストの印を点灯させる（確定した時）</summary>
    public void MarkTested(int index)
    {
        if (index < 0 || index >= _cells.Count) return;

        var (cell, label) = _cells[index];
        cell.color = TestedCellColor;
        label.color = TestedColor;
    }

    /// <summary>認識テストの点灯を全て消す</summary>
    public void ClearTested()
    {
        foreach (var (cell, label) in _cells)
        {
            cell.color = UntestedCellColor;
            label.color = UntestedTextColor;
        }
    }

    public void SetStatus(string text)
    {
        _statusText.text = text;
    }

    public void SetNextInteractable(bool isInteractable)
    {
        _nextButton.interactable = isInteractable;
    }

    /// <summary>カメラのエラーパネルを出す。出している間は「次へ」と下の操作を受け付けない</summary>
    public void ShowCameraError(string title, string detail)
    {
        _isErrorShown = true;
        _errorTitleText.text = title;
        _errorDetailText.text = detail;
        SetErrorPanelVisible(true);
    }

    /// <summary>「手が認識されません」ガイドを出すか</summary>
    public void SetHandGuideVisible(bool isVisible)
    {
        _handGuide.SetVisible(isVisible);
    }

    public void HideCameraError()
    {
        _isErrorShown = false;
        SetErrorPanelVisible(false);
    }

    private void SetErrorPanelVisible(bool isVisible)
    {
        _errorPanel.alpha = isVisible ? 1f : 0f;
        _errorPanel.interactable = isVisible;
        _errorPanel.blocksRaycasts = isVisible;
    }

    private void Update()
    {
        if (!IsInteractable) return;

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F2))
        {
            _onDebugSkipRequested.OnNext(Unit.Default);
            return;
        }
    #endif

        if (!_isErrorShown && _nextButton.interactable
            && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            _onNextRequested.OnNext(Unit.Default);
        else if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
            _onBackRequested.OnNext(Unit.Default);
    }

    private void OnDestroy()
    {
        _onNextRequested.Dispose();
        _onBackRequested.Dispose();
        _onRetryRequested.Dispose();
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        _onDebugSkipRequested.Dispose();
    #endif
    }
}
