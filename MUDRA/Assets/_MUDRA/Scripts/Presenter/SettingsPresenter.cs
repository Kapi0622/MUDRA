using MUDRA.Data;
using R3;
using UnityEngine;

/// <summary>
/// 設定画面と、設定値の反映を配線する（B5）。App側に置く。
///
/// 設定値はProgressModelが持ち、変わるたびに保存する。ここでは値の変化を
/// 画面表示（SettingsView）と実際の反映（フルスクリーン・カメラ映像・音量（B7））に流すだけ。
/// 自動ポーズの値はバトル側（PauseModel）が使うため、BattleContextでIsAutoPauseEnabledを渡す。
///
/// Titleとポーズメニューの両方から開く。開いている間は下の画面のキー操作を止める必要があるため、
/// 開閉の状態をIsOpenで公開する。
/// </summary>
public class SettingsPresenter : MonoBehaviour
{
    [SerializeField] private SettingsView _settingsView;
    [SerializeField] private DisplayModeView _displayModeView;
    [SerializeField] private CameraPreviewView _cameraPreviewView;
    [Tooltip("音量の反映役（B7）")]
    [SerializeField] private AudioVolumeView _audioVolumeView;

    [Header("サウンド（B7）。開く時の決定音は開いた側（タイトル・ポーズメニュー）が鳴らす")]
    [SerializeField] private SePlayer _se;
    [SerializeField] private SoundData _sound;

    private readonly ReactiveProperty<bool> _isOpen = new(false);
    /// <summary>設定画面を開いているか</summary>
    public ReadOnlyReactiveProperty<bool> IsOpen => _isOpen;

    private ProgressModel _progressModel;
    private readonly CompositeDisposable _disposables = new();

    /// <summary>AppInitializerから呼ばれる。起動時に保存済みの設定をすぐ反映する</summary>
    public void Initialize(ProgressModel progressModel)
    {
        _progressModel = progressModel;
        _settingsView.SetVisible(false);

        // --- 設定値 → 表示と反映 ---
        _progressModel.IsFullScreen
            .Subscribe(isOn =>
            {
                _settingsView.SetFullScreen(isOn);
                _displayModeView.SetFullScreen(isOn);
            })
            .AddTo(_disposables);

        _progressModel.ShowCameraPreview
            .Subscribe(isOn =>
            {
                _settingsView.SetCameraPreview(isOn);
                _cameraPreviewView.SetVisible(isOn);
            })
            .AddTo(_disposables);

        _progressModel.IsAutoPauseEnabled
            .Subscribe(_settingsView.SetAutoPause)
            .AddTo(_disposables);

        _progressModel.BgmVolume
            .Subscribe(volume =>
            {
                _settingsView.SetBgmVolume(volume);
                _audioVolumeView.SetBgmVolume(volume);
            })
            .AddTo(_disposables);

        _progressModel.SeVolume
            .Subscribe(volume =>
            {
                _settingsView.SetSeVolume(volume);
                _audioVolumeView.SetSeVolume(volume);
            })
            .AddTo(_disposables);

        // --- 画面の操作 → 設定値の切り替え ---
        _settingsView.OnFullScreenToggled
            .Subscribe(_ => _progressModel.SetFullScreen(!_progressModel.IsFullScreen.CurrentValue))
            .AddTo(_disposables);

        _settingsView.OnCameraPreviewToggled
            .Subscribe(_ => _progressModel.SetShowCameraPreview(!_progressModel.ShowCameraPreview.CurrentValue))
            .AddTo(_disposables);

        _settingsView.OnAutoPauseToggled
            .Subscribe(_ => _progressModel.SetAutoPauseEnabled(!_progressModel.IsAutoPauseEnabled.CurrentValue))
            .AddTo(_disposables);

        Observable.Merge(
                _settingsView.OnFullScreenToggled,
                _settingsView.OnCameraPreviewToggled,
                _settingsView.OnAutoPauseToggled)
            .Subscribe(_ => _se.Play(_sound.uiToggle))
            .AddTo(_disposables);

        _settingsView.OnBgmVolumeChanged
            .Subscribe(_progressModel.SetBgmVolume)
            .AddTo(_disposables);

        // 効果音は動かしても今鳴っている音が無いので、確認用に短い音を鳴らす（新しい音量で鳴るよう、反映してから）
        _settingsView.OnSeVolumeChanged
            .Subscribe(volume =>
            {
                _progressModel.SetSeVolume(volume);
                _se.Play(_sound.uiCursor);
            })
            .AddTo(_disposables);

        _settingsView.OnCloseRequested
            .Subscribe(_ =>
            {
                _se.Play(_sound.uiBack);
                Close();
            })
            .AddTo(_disposables);
    }

    public void Open()
    {
        if (_isOpen.Value) return;
        _settingsView.SetVisible(true);
        _isOpen.Value = true;
    }

    public void Close()
    {
        if (!_isOpen.Value) return;
        _settingsView.SetVisible(false);
        _isOpen.Value = false;
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
        _isOpen.Dispose();
    }
}
