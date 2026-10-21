using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StageSelectに並ぶステージのカード1枚に出す内容（B5）。Modelの型を持ち込まないよう表示用の値だけにする。
/// </summary>
public readonly struct StageCardData
{
    public readonly int StageNumber;
    public readonly string StageName;
    public readonly string BossName;
    public readonly string WeakElementName;
    public readonly Color WeakElementColor;

    /// <summary>クリア済みの記録。未クリアなら空文字</summary>
    public readonly string RecordText;

    public StageCardData(int stageNumber, string stageName, string bossName,
        string weakElementName, Color weakElementColor, string recordText)
    {
        StageNumber = stageNumber;
        StageName = stageName;
        BossName = bossName;
        WeakElementName = weakElementName;
        WeakElementColor = weakElementColor;
        RecordText = recordText;
    }
}

/// <summary>
/// StageSelectのカード1枚（B5）。StageSelectViewがテンプレートから複製して並べる。
/// </summary>
public class StageCardView : MonoBehaviour
{
    // --- 見た目 ---
    private static readonly Color NormalFrameColor = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color SelectedFrameColor = new Color(1f, 0.85f, 0.3f, 1f);
    private const float NormalScale = 1f;
    private const float SelectedScale = 1.06f;

    // 「其の一」のように幕の番号を漢数字で出す
    private static readonly string[] KanjiNumbers = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

    [SerializeField] private Button _button;
    [SerializeField] private Image _frame;
    [SerializeField] private TextMeshProUGUI _numberText;
    [SerializeField] private TextMeshProUGUI _stageNameText;
    [SerializeField] private TextMeshProUGUI _bossText;
    [SerializeField] private TextMeshProUGUI _weaknessText;
    [SerializeField] private TextMeshProUGUI _recordText;

    /// <summary>クリックされた時の通知先。StageSelectViewが設定する</summary>
    public void SetClickHandler(Action onClick)
    {
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick());
    }

    public void SetData(StageCardData data)
    {
        _numberText.text = $"其の{ToKanji(data.StageNumber)}";
        _stageNameText.text = data.StageName;
        _bossText.text = $"主　{data.BossName}";
        _weaknessText.text = $"弱点　{data.WeakElementName}";
        _weaknessText.color = data.WeakElementColor;
        _recordText.text = data.RecordText;
    }

    public void SetSelected(bool isSelected)
    {
        _frame.color = isSelected ? SelectedFrameColor : NormalFrameColor;
        float scale = isSelected ? SelectedScale : NormalScale;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static string ToKanji(int number)
    {
        return number >= 0 && number < KanjiNumbers.Length ? KanjiNumbers[number] : number.ToString();
    }
}
