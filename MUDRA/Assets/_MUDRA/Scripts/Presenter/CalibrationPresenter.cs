using System.Linq;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using MUDRA.HandTracking;
using R3;
using UnityEngine;

/// <summary>
/// Calibration画面を配線する（B6）。App側に置き、AppInitializerから注入を受ける。
///
/// 画面を出すのはScreenPresenter（遷移演出の途中でShowScreenから呼ばれる）で、ここはShow / Hide / Lockを受けて
/// 画面の中身（カメラの状態・認識テスト・次へを押せるか）を配線する。
///
/// 認識テスト用に、バトルとは別のHandTrackingServiceを持つ。バトル側のものはInGameの読み込み中しか存在しないため。
/// Providerは結果を引いてくる（pull）形なので、2つあっても互いに干渉しない。画面を出している間だけTickする。
/// </summary>
public class CalibrationPresenter : MonoBehaviour
{
    private const string StartingText = "カメラを起動しています…";
    private const string WaitingHandText = "カメラに手をかざしてください";
    private const string HandVisibleText = "手を認識しています。下の印を組んで試してみましょう（任意）";
    // 見出しの「手が認識されません」はガイド（HandGuideView）が出すので、ここは次にすることを促す
    private const string HandLostText = "下の項目を確かめて、もう一度手をかざしてください";

    private const string NoDeviceTitle = "カメラが見つかりません";
    private const string NoDeviceDetail = "Webカメラが接続されているか確認してから、再試行してください。";
    private const string NotRespondingTitle = "カメラの映像が届きません";
    private const string NotRespondingDetail =
        "他のアプリがカメラを使っていないか、Windowsの設定でカメラへのアクセスが許可されているか確認してから、再試行してください。";

    /// <summary>認識テストに並べる印。詠唱印 → 両手印 → 特殊印の順</summary>
    private static readonly HandSign[] TestSigns =
    {
        HandSign.Open, HandSign.Fist, HandSign.Point, HandSign.Scissors, HandSign.Palm,
        HandSign.Union, HandSign.DoubleScissors,
        HandSign.Release, HandSign.Cancel, HandSign.Guard,
    };

    [SerializeField] private CalibrationView _view;
    [SerializeField] private HandSignStatusView _handSignStatusView;
    [SerializeField] private CameraPreviewView _cameraPreviewView;

    [Tooltip("再試行でカメラと推論を起動し直す（Solution）")]
    [SerializeField] private HandLandmarkerRunner _runner;

    private GameStateManager _gameStateManager;
    private CameraStatusModel _cameraStatus;
    private HandPresenceModel _handPresence;
    private HandStatusPresenter _handStatusPresenter;
    private CalibrationModel _calibrationModel;
    private HandTrackingService _handTrackingService;

    private bool _isActive;

    private readonly CompositeDisposable _disposables = new();

    /// <summary>AppInitializerから呼ばれる</summary>
    public void Initialize(
        GameStateManager gameStateManager,
        IHandLandmarkProvider provider,
        CameraStatusModel cameraStatus,
        HandPresenceModel handPresence,
        HandStatusPresenter handStatusPresenter)
    {
        _gameStateManager = gameStateManager;
        _cameraStatus = cameraStatus;
        _handPresence = handPresence;
        _handStatusPresenter = handStatusPresenter;

        _calibrationModel = new CalibrationModel(cameraStatus, handPresence);
        _handTrackingService = new HandTrackingService(provider);

        _view.SetSigns(TestSigns.Select(SequenceGuideView.GetSignDisplayName).ToArray());

        // --- カメラ・手の状態 → 状態の一文とエラーパネル ---
        _cameraStatus.Status
            .CombineLatest(_handPresence.IsHandVisible, _handPresence.IsHandLost, (_, _, _) => Unit.Default)
            .Subscribe(_ => UpdateStatus())
            .AddTo(_disposables);

        _calibrationModel.CanProceed
            .Subscribe(_view.SetNextInteractable)
            .AddTo(_disposables);

        // --- 認識テスト ---
        _handTrackingService.Candidate
            .Subscribe(ShowSignCandidate)
            .AddTo(_disposables);

        _handTrackingService.OnHandSignRecognized
            .Subscribe(ShowSignConfirmed)
            .AddTo(_disposables);

        // --- 画面からの要求 ---
        _view.OnNextRequested
            .Where(_ => _calibrationModel.CanProceed.CurrentValue)
            .Subscribe(_ => Proceed())
            .AddTo(_disposables);

        _view.OnBackRequested
            .Subscribe(_ => _gameStateManager.ToTitle())
            .AddTo(_disposables);

        _view.OnRetryRequested
            .Subscribe(_ => RetryCamera())
            .AddTo(_disposables);

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        _view.OnDebugSkipRequested
            .Subscribe(_ => Proceed())
            .AddTo(_disposables);
    #endif
    }

    /// <summary>画面を出す。ScreenPresenterから呼ばれる</summary>
    public void Show()
    {
        _isActive = true;
        // タイトルなどで既に見失っていても、画面を開いてから数え直す（開いた途端にガイドを出さない）
        _handPresence.RestartLostTimer();
        _calibrationModel.Begin(_handPresence.IsHandVisible.CurrentValue);
        _view.ClearTested();
        UpdateStatus();
        _view.Show();
        _cameraPreviewView.SetEmphasized(true);
    }

    /// <summary>画面を消す。ScreenPresenterから呼ばれる（どの画面へ遷移する時も一度呼ばれる）</summary>
    public void Hide()
    {
        // 起動直後にも呼ばれるので、まだInitializeされていない場合は画面だけ消す
        if (_calibrationModel != null && _isActive)
        {
            _calibrationModel.End();
            _cameraPreviewView.SetEmphasized(false);
        }
        _isActive = false;
        _handSignStatusView.Hide();
        _view.Hide();
    }

    /// <summary>遷移が始まったら、覆われるまでの操作を止める</summary>
    public void Lock()
    {
        _view.Lock();
    }

    private void Update()
    {
        if (!_isActive) return;
        _handTrackingService.Tick(Time.unscaledDeltaTime);
    }

    private void Proceed()
    {
        _gameStateManager.CompleteCalibration();
    }

    private void RetryCamera()
    {
        // Play は動いていれば止めてから起動し直す（VisionTaskApiRunner.Play）
        _runner.Play();
        _handStatusPresenter.RecheckCamera();
    }

    private void UpdateStatus()
    {
        if (!_isActive) return;

        var status = _cameraStatus.Status.CurrentValue;
        switch (status)
        {
            case CameraStatus.NoDevice:
                _view.SetHandGuideVisible(false);
                _view.ShowCameraError(NoDeviceTitle, NoDeviceDetail);
                return;
            case CameraStatus.NotResponding:
                _view.SetHandGuideVisible(false);
                _view.ShowCameraError(NotRespondingTitle, NotRespondingDetail);
                return;
        }

        _view.HideCameraError();
        // カメラが動いているのに手が映らない時だけ、映り方のガイドを出す
        _view.SetHandGuideVisible(status == CameraStatus.Running && _handPresence.IsHandLost.CurrentValue);

        if (status == CameraStatus.Starting)
            _view.SetStatus(StartingText);
        else if (_handPresence.IsHandVisible.CurrentValue)
            _view.SetStatus(HandVisibleText);
        else if (_handPresence.IsHandLost.CurrentValue)
            _view.SetStatus(HandLostText);
        else
            _view.SetStatus(WaitingHandText);
    }

    private void ShowSignCandidate(HandSignCandidate candidate)
    {
        // 進み具合1は確定済みの印を保持している状態。確定演出は ShowSignConfirmed が出す（HandSignPresenterと同じ）
        if (!_isActive || candidate.Sign is not { } sign || candidate.Progress >= 1f)
        {
            _handSignStatusView.Hide();
            return;
        }

        _handSignStatusView.ShowCandidate(SequenceGuideView.GetSignDisplayName(sign), candidate.Progress);
    }

    private void ShowSignConfirmed(HandSign sign)
    {
        if (!_isActive) return;

        _handSignStatusView.PlayConfirm(SequenceGuideView.GetSignDisplayName(sign));
        _view.MarkTested(System.Array.IndexOf(TestSigns, sign));
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
        _calibrationModel?.Dispose();
        _handTrackingService?.Dispose();
    }
}
