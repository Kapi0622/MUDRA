using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LitMotion;
using MUDRA.Data;

/// <summary>
/// 術名カットイン・暴発フィードバックのView。
/// 画面中央に短時間テキストを表示し、フェードアウトする。
/// 術発動と暴発は排他的なので、1つのTextMeshProUGUIを共用する。
///
/// 術発動時は属性色の帯が左から滑り込むカットインにする（B4）。
/// 帯の色で「どの属性の術を撃ったか」が分かり、敵の弱点と見比べられる。
/// SpellData.cutInSpriteがあれば帯に載せる（本番の絵が入るまでは帯と術名だけ）。
/// </summary>
public class SpellTelopView : MonoBehaviour
{
    // --- 演出定数 ---
    private const float FadeInDuration = 0.15f;
    private const float DisplayDuration = 0.8f;
    private const float FadeOutDuration = 0.4f;
    private const float ScaleFrom = 0.6f;
    private const float ScaleTo = 1f;
    private const float BandSlideDuration = 0.2f;
    private const float BandSlideDistance = 2400f;  // 画面幅（参照解像度1920）より大きく取り、画面外から入れる
    private const float BandAlpha = 0.75f;

    // --- 色定義 ---
    private static readonly Color CutInTextColor = Color.white;
    private static readonly Color MisfireColor = new Color(1f, 0.3f, 0.3f);    // 暴発: 赤系

    [Header("テロップ表示用テキスト")]
    [SerializeField] private TextMeshProUGUI _telopText;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("カットイン")]
    [SerializeField] private Image _cutInBand;
    [Tooltip("SpellData.cutInSpriteを表示する。未設定の術では非表示")]
    [SerializeField] private Image _cutInImage;

    private MotionHandle _fadeHandle;
    private MotionHandle _scaleHandle;
    private MotionHandle _bandHandle;

    private void Awake()
    {
        // 初期状態は非表示
        _canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 術発動成功時のカットインを表示する。
    /// </summary>
    public void ShowCutIn(string spellName, ElementType element, Sprite cutInSprite)
    {
        var bandColor = ElementColor(element);
        bandColor.a = BandAlpha;
        _cutInBand.color = bandColor;
        _cutInBand.enabled = true;

        _cutInImage.sprite = cutInSprite;
        _cutInImage.enabled = cutInSprite != null;

        Show(spellName, CutInTextColor);

        var bandRect = _cutInBand.rectTransform;
        _bandHandle = LMotion.Create(-BandSlideDistance, 0f, BandSlideDuration)
            .WithEase(Ease.OutCubic)
            .Bind(x => bandRect.anchoredPosition = new Vector2(x, bandRect.anchoredPosition.y));
    }

    /// <summary>
    /// 暴発時のフィードバックを表示する。カットインの帯は出さない。
    /// </summary>
    public void ShowMisfire()
    {
        _cutInBand.enabled = false;
        _cutInImage.enabled = false;

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

    /// <summary>
    /// 属性ごとの帯の色。術エフェクト（パーティクル）の色と揃えている。
    /// </summary>
    private static Color ElementColor(ElementType element)
    {
        return element switch
        {
            ElementType.Wind => new Color(0.35f, 0.8f, 0.45f),
            ElementType.Earth => new Color(0.7f, 0.5f, 0.25f),
            ElementType.Thunder => new Color(0.85f, 0.75f, 0.1f),
            ElementType.Water => new Color(0.2f, 0.45f, 0.9f),
            ElementType.Fire => new Color(0.9f, 0.3f, 0.1f),
            ElementType.Light => new Color(0.95f, 0.9f, 0.7f),
            _ => Color.gray,
        };
    }

    private void CancelCurrentMotions()
    {
        if (_fadeHandle.IsActive()) _fadeHandle.Cancel();
        if (_scaleHandle.IsActive()) _scaleHandle.Cancel();
        if (_bandHandle.IsActive()) _bandHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelCurrentMotions();
    }
}
