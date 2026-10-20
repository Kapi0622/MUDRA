using R3;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイトル画面（B5）。スタートの要求を流すだけで、遷移はScreenPresenter→GameStateManagerが行う。
/// </summary>
public class TitleView : ScreenView
{
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _settingsButton;

    private readonly Subject<Unit> _onStartRequested = new();
    /// <summary>スタートボタン、またはSpace / Enter</summary>
    public Observable<Unit> OnStartRequested => _onStartRequested;

    private readonly Subject<Unit> _onSettingsRequested = new();
    public Observable<Unit> OnSettingsRequested => _onSettingsRequested;

    private void Awake()
    {
        _startButton.onClick.AddListener(() => _onStartRequested.OnNext(Unit.Default));
        _settingsButton.onClick.AddListener(() => _onSettingsRequested.OnNext(Unit.Default));
    }

    public void Show()
    {
        SetVisible(true);
    }

    private void Update()
    {
        if (!IsInteractable) return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            _onStartRequested.OnNext(Unit.Default);
    }

    private void OnDestroy()
    {
        _onStartRequested.Dispose();
        _onSettingsRequested.Dispose();
    }
}
