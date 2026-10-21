using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>術一覧の1行に出す内容（B5）。Modelの型を持ち込まないよう表示用の値だけにする</summary>
public readonly struct SpellListRow
{
    public readonly string SpellName;
    public readonly string ElementName;
    public readonly Color ElementColor;

    /// <summary>印の並び（例: 「開 → 掌」）</summary>
    public readonly string SequenceText;

    public SpellListRow(string spellName, string elementName, Color elementColor, string sequenceText)
    {
        SpellName = spellName;
        ElementName = elementName;
        ElementColor = elementColor;
        SequenceText = sequenceText;
    }
}

/// <summary>
/// ポーズ画面に出す術の一覧（B5）。遊びながら印の並びを確かめられるようにする。
/// 1行を1つのテキストで組み、属性名だけ属性色にする。
/// </summary>
public class SpellListView : MonoBehaviour
{
    [Tooltip("非アクティブのテンプレート。術の数だけ複製する")]
    [SerializeField] private TextMeshProUGUI _rowTemplate;

    private readonly List<TextMeshProUGUI> _rows = new();

    public void SetSpells(IReadOnlyList<SpellListRow> spells)
    {
        _rowTemplate.gameObject.SetActive(false);

        while (_rows.Count < spells.Count)
        {
            var row = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
            row.gameObject.SetActive(true);
            _rows.Add(row);
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            bool hasData = i < spells.Count;
            _rows[i].gameObject.SetActive(hasData);
            if (!hasData) continue;

            var spell = spells[i];
            string colorHex = ColorUtility.ToHtmlStringRGB(spell.ElementColor);
            _rows[i].text = $"{spell.SpellName}　<color=#{colorHex}>{spell.ElementName}</color>　{spell.SequenceText} → 発";
        }
    }
}
