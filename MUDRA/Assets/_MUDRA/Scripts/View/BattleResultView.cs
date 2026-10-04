using UnityEngine;
using TMPro;
using LitMotion;

/// <summary>
/// ステージの決着（討伐・敗北）を画面中央に出すView（B4）。
/// 出したまま残す。リザルト画面と画面遷移はB5で作るので、それまでの仮の決着表示。
/// デバッグのステージ/セクションジャンプで戦闘が再開した時はHideで消す。
/// </summary>
public class BattleResultView : MonoBehaviour
{
    // --- 演出定数 ---
    private const float ShowDuration = 0.4f;
    private const float ScaleFrom = 2.0f;

    private const string ClearText = "討伐";
    private const string GameOverText = "敗北";

    private static readonly Color ClearColor = new Color(1f, 0.85f, 0.3f);
    private static readonly Color GameOverColor = new Color(0.8f, 0.15f, 0.15f);

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _resultText;

    private MotionHandle _fadeHandle;
    private MotionHandle _scaleHandle;
    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 初期化を1回だけ行う。最初のステージ開始時のHideはBattleInitializer.Awake内から流れてくるため、
    /// このViewのAwakeより先に呼ばれることがある。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _canvasGroup.alpha = 0f;
    }

    /// <summary>ステージクリア（最後の敵を討伐）。</summary>
    public void ShowClear()
    {
        Show(ClearText, ClearColor);
    }

    /// <summary>プレイヤーの敗北。</summary>
    public void ShowGameOver()
    {
        Show(GameOverText, GameOverColor);
    }

    /// <summary>決着表示を消す。戦闘が再開した時に呼ぶ。</summary>
    public void Hide()
    {
        EnsureInitialized();
        CancelMotions();
        _canvasGroup.alpha = 0f;
    }

    /// <summary>大きい状態から縮みながらフェードインし、そのまま残す。</summary>
    private void Show(string text, Color color)
    {
        EnsureInitialized();
        CancelMotions();

        _resultText.text = text;
        _resultText.color = color;

        var rect = _resultText.rectTransform;
        _scaleHandle = LMotion.Create(ScaleFrom, 1f, ShowDuration)
            .WithEase(Ease.OutCubic)
            .Bind(s => rect.localScale = new Vector3(s, s, 1f));

        _fadeHandle = LMotion.Create(0f, 1f, ShowDuration)
            .WithEase(Ease.OutQuad)
            .Bind(a => _canvasGroup.alpha = a);
    }

    private void CancelMotions()
    {
        if (_fadeHandle.IsActive()) _fadeHandle.Cancel();
        if (_scaleHandle.IsActive()) _scaleHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelMotions();
    }
}
