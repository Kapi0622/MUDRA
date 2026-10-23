using TMPro;
using UnityEngine;

/// <summary>
/// 「手が認識されません」ガイド（B6）。手を見失った時に、映り方を直すための確認項目を出す。
/// プレハブにして、Calibration画面とバトルのポーズメニューの両方に置く（どこで見失っても同じ案内を出すため）。
///
/// いつ出すかはPresenterが決める。確認項目は固定の文なので、ここに定数で持つ（どの画面でも同じにするため）。
/// カメラ自体が使えない時（未接続・映像が届かない）は原因が違うので、このガイドではなくCalibrationのエラーパネルを出す。
/// </summary>
public class HandGuideView : MonoBehaviour
{
    private const string TitleText = "手が認識されません";

    /// <summary>確認項目。上から順に、直しやすく効き目の大きいもの</summary>
    private static readonly string[] CheckItems =
    {
        "明るい場所で",
        "カメラから30cm〜1mほど離す",
        "手のひらをカメラに向ける",
        "手首まで映像に入れる",
    };

    private const string ItemBullet = "・";

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _itemsText;

    private void Awake()
    {
        _titleText.text = TitleText;
        _itemsText.text = ItemBullet + string.Join("\n" + ItemBullet, CheckItems);
    }

    public void Show()
    {
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    /// <summary>表示だけを切り替える。ボタンを持たないので、下の操作の邪魔をしないよう常にレイキャストを通す</summary>
    public void SetVisible(bool isVisible)
    {
        _canvasGroup.alpha = isVisible ? 1f : 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }
}
