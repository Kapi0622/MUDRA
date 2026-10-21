using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 設定画面（B5）。項目ごとのボタンを押すたびにオン/オフを切り替える要求を流し、
/// 現在の値はSettingsPresenterから受け取って表示する（値そのものは持たない）。
/// Title・ポーズメニューの両方から開くため、App側の専用キャンバスに置いて最前面に出す。
/// キー: Esc / Backspace で閉じる。
/// </summary>
public class SettingsView : MonoBehaviour
{
    private const string OnText = "オン";
    private const string OffText = "オフ";

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Button _fullScreenButton;
    [SerializeField] private TextMeshProUGUI _fullScreenLabel;
    [SerializeField] private Button _cameraPreviewButton;
    [SerializeField] private TextMeshProUGUI _cameraPreviewLabel;
    [SerializeField] private Button _autoPauseButton;
    [SerializeField] private TextMeshProUGUI _autoPauseLabel;
    [SerializeField] private Button _closeButton;

    private readonly Subject<Unit> _onFullScreenToggled = new();
    public Observable<Unit> OnFullScreenToggled => _onFullScreenToggled;

    private readonly Subject<Unit> _onCameraPreviewToggled = new();
    public Observable<Unit> OnCameraPreviewToggled => _onCameraPreviewToggled;

    private readonly Subject<Unit> _onAutoPauseToggled = new();
    public Observable<Unit> OnAutoPauseToggled => _onAutoPauseToggled;

    private readonly Subject<Unit> _onCloseRequested = new();
    public Observable<Unit> OnCloseRequested => _onCloseRequested;

    private void Awake()
    {
        _fullScreenButton.onClick.AddListener(() => _onFullScreenToggled.OnNext(Unit.Default));
        _cameraPreviewButton.onClick.AddListener(() => _onCameraPreviewToggled.OnNext(Unit.Default));
        _autoPauseButton.onClick.AddListener(() => _onAutoPauseToggled.OnNext(Unit.Default));
        _closeButton.onClick.AddListener(() => _onCloseRequested.OnNext(Unit.Default));
    }

    public void SetVisible(bool isVisible)
    {
        _canvasGroup.alpha = isVisible ? 1f : 0f;
        _canvasGroup.interactable = isVisible;
        _canvasGroup.blocksRaycasts = isVisible;
    }

    public void SetFullScreen(bool isOn) => _fullScreenLabel.text = $"フルスクリーン：{OnOff(isOn)}";
    public void SetCameraPreview(bool isOn) => _cameraPreviewLabel.text = $"カメラ映像の表示：{OnOff(isOn)}";
    public void SetAutoPause(bool isOn) => _autoPauseLabel.text = $"自動ポーズ：{OnOff(isOn)}";

    private static string OnOff(bool isOn) => isOn ? OnText : OffText;

    private void Update()
    {
        if (!_canvasGroup.interactable) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            _onCloseRequested.OnNext(Unit.Default);
    }

    private void OnDestroy()
    {
        _onFullScreenToggled.Dispose();
        _onCameraPreviewToggled.Dispose();
        _onAutoPauseToggled.Dispose();
        _onCloseRequested.Dispose();
    }
}
