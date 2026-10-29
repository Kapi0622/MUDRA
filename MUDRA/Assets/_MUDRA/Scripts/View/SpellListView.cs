using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>術一覧の1行に出す内容（B5）。Modelの型を持ち込まないよう表示用の値だけにする</summary>
public readonly struct SpellListRow
{
    public readonly string SpellName;
    public readonly string ElementName;
    public readonly Color ElementColor;

    /// <summary>印の並び（例: 「開→掌」）</summary>
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
/// 術が11種に増えて1列では下の印一覧に被ったため、B8で2列にした（GridLayoutGroup・上から下へ埋めて右の列へ）。
/// </summary>
public class SpellListView : MonoBehaviour
{
    // 一覧の末尾に添える注意書き（B8）。連続減衰は術ごとの性質ではなく全術共通の決まりなので、行ではなく注記にする。
    // 半分の幅のセルに収まるよう短くしている（詳しい説明はチュートリアルの属性の段で出す）
    private const string RepeatNote = "<size=80%>※ 同じ属性の連続は威力↓</size>";
    private const int ColumnCount = 2;

    [Tooltip("非アクティブのテンプレート。術の数だけ複製する")]
    [SerializeField] private TextMeshProUGUI _rowTemplate;

    private readonly List<TextMeshProUGUI> _rows = new();
    private TextMeshProUGUI _noteRow;

    public void SetSpells(IReadOnlyList<SpellListRow> spells)
    {
        _rowTemplate.gameObject.SetActive(false);

        while (_rows.Count < spells.Count)
        {
            var row = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
            row.gameObject.SetActive(true);
            _rows.Add(row);
        }

        // 注記は術の行と同じテンプレートから作り、常に最後に並べる
        if (_noteRow == null)
        {
            _noteRow = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
            _noteRow.gameObject.SetActive(true);
            _noteRow.text = RepeatNote;
        }
        _noteRow.transform.SetAsLastSibling();

        // 行数（術＋注記）に合わせて1列の段数を決め、常に ColumnCount 列に収める。
        // 術を増減してもインスペクタを触らずに済むよう、段数はここで入れる
        var grid = GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = Mathf.Max(1, Mathf.CeilToInt((spells.Count + 1) / (float)ColumnCount));
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            bool hasData = i < spells.Count;
            _rows[i].gameObject.SetActive(hasData);
            if (!hasData) continue;

            var spell = spells[i];
            string colorHex = ColorUtility.ToHtmlStringRGB(spell.ElementColor);
            _rows[i].text = $"{spell.SpellName}　<color=#{colorHex}>{spell.ElementName}</color>　{spell.SequenceText}→発";
        }
    }
}
