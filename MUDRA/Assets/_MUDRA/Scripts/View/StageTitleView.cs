using UnityEngine;
using TMPro;
using LitMotion;

/// <summary>
/// ステージ名・ボス名を画面上部に表示するView（B4）。
/// フェードイン → 表示維持 → フェードアウト。組み立てはSpellTelopViewと同じ。
/// 術名テロップ（画面中央）と同時に出ても重ならないよう、別のテキストで上部に出す。
/// </summary>
public class StageTitleView : MonoBehaviour
{
    // --- 演出定数 ---
    private const float FadeInDuration = 0.3f;
    private const float DisplayDuration = 1.2f;
    private const float FadeOutDuration = 0.5f;
    private const float SlideDistance = 40f;    // 上から降りてくる距離（UI座標）

    private static readonly Color StageColor = Color.white;
    private static readonly Color BossColor = new Color(1f, 0.45f, 0.4f);

    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private CanvasGroup _canvasGroup;

    private Vector2 _basePosition;
    private MotionHandle _fadeHandle;
    private MotionHandle _slideHandle;

    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 初期化を1回だけ行う。最初のステージ名表示はBattleInitializer.Awake内のStartStageから流れてくるため、
    /// このViewのAwakeより先にShowが呼ばれることがある。後からAwakeが走って表示中のテロップを消さないようにする。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _basePosition = _titleText.rectTransform.anchoredPosition;
        _canvasGroup.alpha = 0f;
    }

    /// <summary>ステージ開始時にステージ名を表示する。</summary>
    public void ShowStageTitle(string stageName)
    {
        Show(stageName, StageColor);
    }

    /// <summary>ボス登場時にボス名を表示する。</summary>
    public void ShowBossTitle(string bossName)
    {
        Show(bossName, BossColor);
    }

    private void Show(string text, Color color)
    {
        EnsureInitialized();
        CancelCurrentMotions();

        _titleText.text = text;
        _titleText.color = color;

        var rect = _titleText.rectTransform;
        _slideHandle = LMotion.Create(SlideDistance, 0f, FadeInDuration)
            .WithEase(Ease.OutCubic)
            .Bind(y => rect.anchoredPosition = _basePosition + new Vector2(0f, y));

        _canvasGroup.alpha = 0f;
        _fadeHandle = LMotion.Create(0f, 1f, FadeInDuration)
            .WithEase(Ease.OutQuad)
            .WithOnComplete(() =>
            {
                _fadeHandle = LMotion.Create(1f, 0f, FadeOutDuration)
                    .WithEase(Ease.InQuad)
                    .WithDelay(DisplayDuration)
                    .Bind(a => _canvasGroup.alpha = a);
            })
            .Bind(a => _canvasGroup.alpha = a);
    }

    private void CancelCurrentMotions()
    {
        if (_fadeHandle.IsActive()) _fadeHandle.Cancel();
        if (_slideHandle.IsActive()) _slideHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelCurrentMotions();
    }
}
