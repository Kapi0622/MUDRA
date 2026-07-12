using UnityEngine;
using TMPro;
using LitMotion;

/// <summary>
/// 術名テロップ・暴発フィードバックのView。
/// 画面中央に短時間テキストを表示し、フェードアウトする。
/// 術発動と暴発は排他的なので、1つのTextMeshProUGUIを共用する。
/// </summary>
public class SpellTelopView : MonoBehaviour
{
    // --- 演出定数 ---
    private const float FadeInDuration = 0.15f;
    private const float DisplayDuration = 0.8f;
    private const float FadeOutDuration = 0.4f;
    private const float ScaleFrom = 0.6f;
    private const float ScaleTo = 1f;

    // --- 色定義 ---
    private static readonly Color SuccessColor = new Color(1f, 0.95f, 0.6f);   // 術名: 金色系
    private static readonly Color MisfireColor = new Color(1f, 0.3f, 0.3f);    // 暴発: 赤系

    [Header("テロップ表示用テキスト")]
    [SerializeField] private TextMeshProUGUI _telopText;
    [SerializeField] private CanvasGroup _canvasGroup;

    private MotionHandle _fadeHandle;
    private MotionHandle _scaleHandle;

    private void Awake()
    {
        // 初期状態は非表示
        _canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 術発動成功時のテロップを表示する。
    /// </summary>
    public void ShowSpellName(string spellName)
    {
        Show(spellName, SuccessColor);
    }

    /// <summary>
    /// 暴発時のフィードバックを表示する。
    /// </summary>
    public void ShowMisfire()
    {
        Show("暴発", MisfireColor);
    }

    /// <summary>
    /// テロップ演出の本体。
    /// フェードイン → 表示維持 → フェードアウト を LMotion で実行する。
    /// 前の演出が残っていればキャンセルして上書きする。
    /// </summary>
    private void Show(string text, Color color)
    {
        CancelCurrentMotions();

        _telopText.text = text;
        _telopText.color = color;

        // スケールアニメーション: 小さい状態から通常サイズへ
        var telopTransform = _telopText.rectTransform;
        _scaleHandle = LMotion.Create(ScaleFrom, ScaleTo, FadeInDuration)
            .WithEase(Ease.OutBack)
            .Bind(s => telopTransform.localScale = new Vector3(s, s, 1f));

        // フェード: In → 維持 → Out を1本のシーケンスで実行
        // フェードイン
        _canvasGroup.alpha = 0f;
        _fadeHandle = LMotion.Create(0f, 1f, FadeInDuration)
            .WithEase(Ease.OutQuad)
            .WithOnComplete(() =>
            {
                // 表示維持後にフェードアウト開始
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
        if (_scaleHandle.IsActive()) _scaleHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelCurrentMotions();
    }
}