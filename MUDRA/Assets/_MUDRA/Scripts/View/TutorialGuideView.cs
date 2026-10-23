using LitMotion;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// チュートリアルの指示パネル（B6）。バトル画面に重ね、今の段階・指示・進み具合と「スキップ」ボタンを出す。
/// 段階を達成した時は「よし！」をポップさせる。
///
/// 何を書くか（指示文・術名・印の並び）はPresenterが組み立てる。ここは表示と、スキップの要求の通知だけ。
/// 通常のバトルでは使わないので、表示するまでは消しておく。
///
/// チュートリアルは毎回通るので、慣れた人がすぐ抜けられるようTabキーでもスキップできる。
/// Esc（ポーズ）と、キーボードモードの印のキー（Space・Backspace・数字など）と被らないキーにしている。
/// </summary>
public class TutorialGuideView : MonoBehaviour
{
    private const float ClearedPopScale = 1.4f;
    private const float ClearedPopDuration = 0.25f;

    private const KeyCode SkipKey = KeyCode.Tab;

    // SequenceGuideView / HandSignStatusView の ConfirmedColor と同じ値
    private static readonly Color ClearedColor = new(0.3f, 1f, 0.5f);

    [SerializeField] private CanvasGroup _canvasGroup;
    [Tooltip("「1 / 5」")]
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _detailText;
    [Tooltip("補足（暴発した時など）。空なら出さない")]
    [SerializeField] private TextMeshProUGUI _noteText;
    [Tooltip("達成した時の「よし！」")]
    [SerializeField] private TextMeshProUGUI _clearedText;
    [SerializeField] private Button _skipButton;

    private MotionHandle _popHandle;
    private bool _isInputEnabled = true;
    private bool _isLocked;

    private readonly Subject<Unit> _onSkipRequested = new();
    /// <summary>「スキップ」ボタン、またはTabキー</summary>
    public Observable<Unit> OnSkipRequested => _onSkipRequested;

    private void Awake()
    {
        _skipButton.onClick.AddListener(() => _onSkipRequested.OnNext(Unit.Default));
        _clearedText.color = ClearedColor;
        HideCleared();
        SetVisible(false);
    }

    public void Show()
    {
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    /// <summary>段階の表示を切り替える。補足と「よし！」は消す</summary>
    public void SetStep(string progress, string title, string detail)
    {
        _progressText.text = progress;
        _titleText.text = title;
        _detailText.text = detail;
        SetNote(string.Empty);
        HideCleared();
    }

    public void SetNote(string note)
    {
        _noteText.text = note;
        _noteText.gameObject.SetActive(!string.IsNullOrEmpty(note));
    }

    /// <summary>達成の表示を出す。大きく出してから元の大きさに縮める</summary>
    public void PlayCleared(string text)
    {
        CancelPop();
        _clearedText.text = text;
        _clearedText.gameObject.SetActive(true);

        var rect = _clearedText.rectTransform;
        _popHandle = LMotion.Create(ClearedPopScale, 1f, ClearedPopDuration)
            .WithEase(Ease.OutBack)
            .Bind(s => rect.localScale = new Vector3(s, s, 1f));
    }

    public void HideCleared()
    {
        CancelPop();
        _clearedText.gameObject.SetActive(false);
    }

    /// <summary>スキップを受け付けるか。ポーズ中は止める（ポーズメニューの操作と混ざらないように）</summary>
    public void SetInputEnabled(bool isEnabled)
    {
        _isInputEnabled = isEnabled;
        _skipButton.interactable = isEnabled && !_isLocked;
    }

    /// <summary>遷移が始まったら、二重にスキップされないよう止める</summary>
    public void Lock()
    {
        _isLocked = true;
        _skipButton.interactable = false;
    }

    private void Update()
    {
        if (_isLocked || !_isInputEnabled || _canvasGroup.alpha <= 0f) return;

        if (Input.GetKeyDown(SkipKey))
            _onSkipRequested.OnNext(Unit.Default);
    }

    private void SetVisible(bool isVisible)
    {
        _canvasGroup.alpha = isVisible ? 1f : 0f;
        _canvasGroup.interactable = isVisible;
        _canvasGroup.blocksRaycasts = isVisible;
    }

    private void CancelPop()
    {
        if (_popHandle.IsActive()) _popHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelPop();
        _onSkipRequested.Dispose();
    }
}
