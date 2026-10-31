using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 図鑑の一覧の1行（術1つ・妖1体）。名前と小さな絵を出し、選択中の印を切り替えるだけ。
/// CodexViewがテンプレートから複製して使う（StageCardViewと同じ作り）。
/// </summary>
public class CodexListItemView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _nameText;

    [Tooltip("術のアイコン・妖の絵。絵が無い項目では隠す")]
    [SerializeField] private Image _image;
    [Tooltip("絵の台紙（円・Mask付き）。_imageはこの子に置く")]
    [SerializeField] private CodexImagePlate _plate;

    [Tooltip("選択中だけ出す印（下線・枠など）")]
    [SerializeField] private GameObject _selectedMark;

    public void SetClickHandler(Action onClick)
    {
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick());
    }

    /// <summary>内容を設定する。未解放の項目はPresenterが名前を伏せ、絵の色を黒にして渡してくる</summary>
    public void SetData(in CodexEntry entry)
    {
        _nameText.text = entry.Name;
        _plate.Show(_image, entry);
    }

    public void SetSelected(bool isSelected)
    {
        if (_selectedMark != null) _selectedMark.SetActive(isSelected);
    }
}
