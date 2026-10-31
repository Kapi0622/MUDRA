using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 図鑑の絵の円い台紙（和紙 T_UI_IconPlate）。術アイコン（白地に黒い墨）は台紙に載せ、円で切り抜いて四角い白地の角を隠す。
/// 妖の絵は透過済みなので台紙を出さない（Maskも切って、子の絵をそのまま見せる）。
/// 一覧の行と詳細の両方で使う。
/// </summary>
[RequireComponent(typeof(Image), typeof(Mask))]
public class CodexImagePlate : MonoBehaviour
{
    [SerializeField] private Image _plate;
    [SerializeField] private Mask _mask;

    [Tooltip("台紙に載せる絵に掛ける色。和紙の平均の色にしておくと、白地が残ったアイコンの白が和紙になじむ（黒い墨は黒のまま）")]
    [SerializeField] private Color _imageTint = new(0.94f, 0.91f, 0.85f, 1f);

    /// <summary>台紙と、その子の絵を項目に合わせて出す。絵が無ければ台紙ごと隠す</summary>
    public void Show(Image image, in CodexEntry entry)
    {
        bool hasImage = entry.Image != null;
        gameObject.SetActive(hasImage);
        if (!hasImage) return;

        _plate.enabled = entry.HasPlate;
        _mask.enabled = entry.HasPlate;

        image.sprite = entry.Image;
        image.preserveAspect = true;
        image.color = entry.HasPlate ? entry.ImageColor * _imageTint : entry.ImageColor;
    }
}
