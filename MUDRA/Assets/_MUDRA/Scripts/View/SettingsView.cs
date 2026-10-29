using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 設定画面（B5）。項目ごとのボタンを押すたびにオン/オフを切り替える要求を流し、
/// 現在の値はSettingsPresenterから受け取って表示する（値そのものは持たない）。
/// Title・ポーズメニューの両方から開くため、App側の専用キャンバスに置いて最前面に出す。
/// キー: Esc / Backspace で閉じる。
///
/// 音量（B7）はスライダーで 0〜VolumeSteps の整数で選ぶ。外とやり取りする値は 0〜1 にそろえる。
/// </summary>
public class SettingsView : MonoBehaviour
{
    private const string OnText = "オン";
    private const string OffText = "オフ";
    /// <summary>音量スライダーの段階数（0〜10）</summary>
    private const int VolumeSteps = 10;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Button _fullScreenButton;
    [SerializeField] private TextMeshProUGUI _fullScreenLabel;
    [SerializeField] private Button _cameraPreviewButton;
    [SerializeField] private TextMeshProUGUI _cameraPreviewLabel;
    [SerializeField] private Button _autoPauseButton;
    [SerializeField] private TextMeshProUGUI _autoPauseLabel;
    [SerializeField] private Button _closeButton;

    [Header("音量（B7）")]
    [SerializeField] private Slider _bgmVolumeSlider;
    [SerializeField] private TextMeshProUGUI _bgmVolumeLabel;
    [SerializeField] private Slider _seVolumeSlider;
    [SerializeField] private TextMeshProUGUI _seVolumeLabel;

    private readonly Subject<Unit> _onFullScreenToggled = new();
    public Observable<Unit> OnFullScreenToggled => _onFullScreenToggled;

    private readonly Subject<Unit> _onCameraPreviewToggled = new();
    public Observable<Unit> OnCameraPreviewToggled => _onCameraPreviewToggled;

    private readonly Subject<Unit> _onAutoPauseToggled = new();
    public Observable<Unit> OnAutoPauseToggled => _onAutoPauseToggled;

    private readonly Subject<Unit> _onCloseRequested = new();
    public Observable<Unit> OnCloseRequested => _onCloseRequested;

    private readonly Subject<float> _onBgmVolumeChanged = new();
    /// <summary>BGMのスライダーが操作された（0〜1）。SetBgmVolumeで表示を合わせた時は流さない</summary>
    public Observable<float> OnBgmVolumeChanged => _onBgmVolumeChanged;

    private readonly Subject<float> _onSeVolumeChanged = new();
    /// <summary>効果音のスライダーが操作された（0〜1）。SetSeVolumeで表示を合わせた時は流さない</summary>
    public Observable<float> OnSeVolumeChanged => _onSeVolumeChanged;

    private void Awake()
    {
        _fullScreenButton.onClick.AddListener(() => _onFullScreenToggled.OnNext(Unit.Default));
        _cameraPreviewButton.onClick.AddListener(() => _onCameraPreviewToggled.OnNext(Unit.Default));
        _autoPauseButton.onClick.AddListener(() => _onAutoPauseToggled.OnNext(Unit.Default));
        _closeButton.onClick.AddListener(() => _onCloseRequested.OnNext(Unit.Default));

        SetUpVolumeSlider(_bgmVolumeSlider);
        SetUpVolumeSlider(_seVolumeSlider);
        _bgmVolumeSlider.onValueChanged.AddListener(step => _onBgmVolumeChanged.OnNext(step / VolumeSteps));
        _seVolumeSlider.onValueChanged.AddListener(step => _onSeVolumeChanged.OnNext(step / VolumeSteps));
    }

    private static void SetUpVolumeSlider(Slider slider)
    {
        slider.minValue = 0f;
        slider.maxValue = VolumeSteps;
        slider.wholeNumbers = true;
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

    /// <summary>BGMの音量（0〜1）を表示に反映する。操作の合図は流さない</summary>
    public void SetBgmVolume(float volume) => ShowVolume(_bgmVolumeSlider, _bgmVolumeLabel, "BGM", volume);

    /// <summary>効果音の音量（0〜1）を表示に反映する。操作の合図は流さない</summary>
    public void SetSeVolume(float volume) => ShowVolume(_seVolumeSlider, _seVolumeLabel, "効果音", volume);

    private static void ShowVolume(Slider slider, TextMeshProUGUI label, string name, float volume)
    {
        int step = Mathf.RoundToInt(volume * VolumeSteps);
        slider.SetValueWithoutNotify(step);
        label.text = $"{name}：{step}";
    }

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
        _onBgmVolumeChanged.Dispose();
        _onSeVolumeChanged.Dispose();
    }
}
