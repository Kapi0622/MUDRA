using LitMotion;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイトル画面（B5）。スタートの要求を流すだけで、遷移はScreenPresenter→GameStateManagerが行う。
///
/// 見た目（UI改修 ステップ2）: 画面に入るたびにロゴを墨がにじむように出し（少し大きい状態から縮めつつ浮かび上がらせる）、
/// 出きったら開始ボタンの文言をゆっくり明滅させる。ロゴは遷移の覆いが晴れてから出るよう少し遅らせる
/// （ResultViewのランクの判子と同じ事情）。ポーズ中のリタイアから戻った時も動くよう実時間で進める。
/// </summary>
public class TitleView : ScreenView
{
    // --- ロゴの出方 ---
    private const float LogoDelay = 0.5f;
    private const float LogoDuration = 1.4f;
    private const float LogoScaleFrom = 1.08f;

    // --- 開始の文言の明滅 ---
    private const float BlinkDuration = 1.1f;
    private const float BlinkMinAlpha = 0.3f;

    [SerializeField] private Button _startButton;
    [SerializeField] private Button _settingsButton;

    [Header("演出")]
    [Tooltip("ロゴの画像と、その不透明度をまとめて動かすCanvasGroup")]
    [SerializeField] private RectTransform _logo;
    [SerializeField] private CanvasGroup _logoGroup;
    [Tooltip("明滅させる開始ボタンの文言")]
    [SerializeField] private TextMeshProUGUI _startLabel;

    private MotionHandle _logoHandle;
    private MotionHandle _blinkHandle;

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

    /// <summary>画面を出す（設定画面から戻った時にも呼ばれるので、演出はここでは流さない）</summary>
    public void Show()
    {
        SetVisible(true);
    }

    /// <summary>
    /// ロゴの登場と開始の文言の明滅を最初から流す。タイトルへ遷移してきた時だけ呼ぶ
    /// </summary>
    public void PlayIntro()
    {
        CancelMotions();
        _startLabel.alpha = 1f;
        _logoGroup.alpha = 0f;

        _logoHandle = LMotion.Create(0f, 1f, LogoDuration)
            .WithDelay(LogoDelay)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutCubic)
            .WithOnComplete(StartBlink)
            .Bind(t =>
            {
                float scale = Mathf.Lerp(LogoScaleFrom, 1f, t);
                _logo.localScale = new Vector3(scale, scale, 1f);
                // 不透明度は縮みより先に立ち上げ、墨が紙に染みていくように見せる
                _logoGroup.alpha = Mathf.Clamp01(t * 1.5f);
            });
    }

    private void StartBlink()
    {
        _blinkHandle = LMotion.Create(1f, BlinkMinAlpha, BlinkDuration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.InOutSine)
            .WithLoops(-1, LoopType.Yoyo)
            .Bind(a => _startLabel.alpha = a);
    }

    private void CancelMotions()
    {
        if (_logoHandle.IsActive()) _logoHandle.Cancel();
        if (_blinkHandle.IsActive()) _blinkHandle.Cancel();
    }

    private void Update()
    {
        if (!IsInteractable) return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            _onStartRequested.OnNext(Unit.Default);
    }

    private void OnDestroy()
    {
        CancelMotions();
        _onStartRequested.Dispose();
        _onSettingsRequested.Dispose();
    }
}
