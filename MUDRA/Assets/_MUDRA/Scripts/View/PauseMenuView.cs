using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ポーズメニューと再開のカウントダウン（B5）。InGameの最前面のキャンバスに置く。
///
/// timeScale = 0 の間に表示・操作するため、時間を使う処理（カウントダウン）は全て実時間で進める。
/// キー: Esc = ポーズ / 再開の切替、Space / Enter = 再開（メニュー表示中のみ）。
/// </summary>
public class PauseMenuView : MonoBehaviour
{
    // --- カウントダウン ---
    private const string ResumeCallText = "結印！";
    private const float ResumeCallDuration = 0.6f;

    [Header("メニュー")]
    [SerializeField] private CanvasGroup _menuGroup;
    [SerializeField] private TextMeshProUGUI _reasonText;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _stageSelectButton;
    [Tooltip("設定画面を開くボタン。未設定なら出さない")]
    [SerializeField] private Button _settingsButton;
    [Tooltip("「手が認識されません」ガイド（B6）。手を見失ってポーズした時だけ出す。未設定なら出さない")]
    [SerializeField] private HandGuideView _handGuide;

    [Header("バトル中")]
    [Tooltip("バトル中に右上へ出しておくポーズボタン（マウス操作用）")]
    [SerializeField] private Button _pauseButton;

    [Header("カウントダウン")]
    [SerializeField] private CanvasGroup _countdownGroup;
    [SerializeField] private TextMeshProUGUI _countdownText;

    private readonly Subject<Unit> _onPauseToggleRequested = new();
    /// <summary>Escキー、またはポーズボタン</summary>
    public Observable<Unit> OnPauseToggleRequested => _onPauseToggleRequested;

    private readonly Subject<Unit> _onResumeRequested = new();
    public Observable<Unit> OnResumeRequested => _onResumeRequested;

    private readonly Subject<Unit> _onRetryRequested = new();
    public Observable<Unit> OnRetryRequested => _onRetryRequested;

    private readonly Subject<Unit> _onStageSelectRequested = new();
    public Observable<Unit> OnStageSelectRequested => _onStageSelectRequested;

    private readonly Subject<Unit> _onSettingsRequested = new();
    public Observable<Unit> OnSettingsRequested => _onSettingsRequested;

    private CancellationTokenSource _countdownCts;
    private bool _isLocked;
    private bool _isInputEnabled = true;
    private int _inputEnabledFrame;

    private void Awake()
    {
        _resumeButton.onClick.AddListener(() => _onResumeRequested.OnNext(Unit.Default));
        _retryButton.onClick.AddListener(() => _onRetryRequested.OnNext(Unit.Default));
        _stageSelectButton.onClick.AddListener(() => _onStageSelectRequested.OnNext(Unit.Default));
        _pauseButton.onClick.AddListener(() => _onPauseToggleRequested.OnNext(Unit.Default));
        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(() => _onSettingsRequested.OnNext(Unit.Default));

        SetMenuVisible(false);
        _countdownGroup.alpha = 0f;
    }

    /// <summary>設定ボタンを出すか。設定画面がつながっている時だけ出す</summary>
    public void SetSettingsAvailable(bool isAvailable)
    {
        if (_settingsButton != null) _settingsButton.gameObject.SetActive(isAvailable);
    }

    /// <summary>ポーズメニューを出す。reasonはポーズした理由の説明（手動なら空）</summary>
    /// <param name="showsHandGuide">「手が認識されません」ガイドを出すか（手を見失ってポーズした時）</param>
    public void ShowMenu(string reason, bool showsHandGuide)
    {
        StopCountdown();
        _reasonText.text = reason;
        if (_handGuide != null) _handGuide.SetVisible(showsHandGuide);
        SetMenuVisible(true);
    }

    public void HideMenu()
    {
        SetMenuVisible(false);
    }

    /// <summary>
    /// キー操作とボタンを受け付けるか。設定画面を重ねている間は止める。
    /// 設定画面をEscで閉じた同じフレームに、こちらがそのEscでポーズを解除しないよう、再開は次のフレームからにする
    /// </summary>
    public void SetInputEnabled(bool isEnabled)
    {
        _isInputEnabled = isEnabled;
        _inputEnabledFrame = Time.frameCount;
        _menuGroup.interactable = _menuGroup.alpha > 0f && isEnabled && !_isLocked;
        _pauseButton.interactable = isEnabled && !_isLocked;
    }

    /// <summary>
    /// 画面の行き先（リトライ・ステージ選択）を選んだ後、遷移で覆われるまでの間の操作を止める。
    /// 二重に押されて同じ遷移が2回走るのを防ぐ。
    /// </summary>
    public void Lock()
    {
        _isLocked = true;
        _menuGroup.interactable = false;
        _pauseButton.interactable = false;
    }

    /// <summary>再開のカウントダウンを出す。seconds秒かけて「3・2・1」と数え、最後に「結印！」を出す</summary>
    public void PlayCountdown(float seconds)
    {
        StopCountdown();
        HideMenu();
        _countdownCts = new CancellationTokenSource();
        CountdownAsync(seconds, _countdownCts.Token).Forget();
    }

    /// <summary>カウントダウンを打ち切って消す（カウントダウン中にもう一度ポーズした時）</summary>
    public void StopCountdown()
    {
        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        _countdownCts = null;
        _countdownGroup.alpha = 0f;
    }

    private async UniTaskVoid CountdownAsync(float seconds, CancellationToken token)
    {
        try
        {
            _countdownGroup.alpha = 1f;
            float remaining = seconds;
            while (remaining > 0f)
            {
                _countdownText.text = Mathf.CeilToInt(remaining).ToString();
                float step = Mathf.Min(1f, remaining);
                await UniTask.Delay(TimeSpan.FromSeconds(step), ignoreTimeScale: true, cancellationToken: token);
                remaining -= step;
            }

            // ここで時間が動き出す。「結印！」は少しだけ残してから消す
            _countdownText.text = ResumeCallText;
            await UniTask.Delay(TimeSpan.FromSeconds(ResumeCallDuration), ignoreTimeScale: true, cancellationToken: token);
            _countdownGroup.alpha = 0f;
        }
        catch (OperationCanceledException)
        {
            // StopCountdownで消している
        }
    }

    private void SetMenuVisible(bool isVisible)
    {
        _menuGroup.alpha = isVisible ? 1f : 0f;
        _menuGroup.interactable = isVisible && _isInputEnabled && !_isLocked;
        _menuGroup.blocksRaycasts = isVisible;
    }

    private void Update()
    {
        if (_isLocked || !_isInputEnabled || Time.frameCount == _inputEnabledFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _onPauseToggleRequested.OnNext(Unit.Default);
            return;
        }

        if (_menuGroup.interactable && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            _onResumeRequested.OnNext(Unit.Default);
    }

    private void OnDestroy()
    {
        StopCountdown();
        _onPauseToggleRequested.Dispose();
        _onResumeRequested.Dispose();
        _onRetryRequested.Dispose();
        _onStageSelectRequested.Dispose();
        _onSettingsRequested.Dispose();
    }
}
