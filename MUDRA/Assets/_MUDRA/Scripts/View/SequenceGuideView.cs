using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LitMotion;
using MUDRA.Data;

/// <summary>
/// 印シーケンスガイドのView。
/// 詠唱開始時にマッチ候補の術を一覧表示し、
/// 印が確定するたびにハイライト+候補の絞り込みを反映する。
/// </summary>
public class SequenceGuideView : MonoBehaviour
{
    // --- 表示制限 ---
    private const int MaxDisplayCount = 4;

    // --- 確定エフェクト定数 ---
    private const float PunchScaleAmount = 1.3f;
    private const float PunchDuration = 0.15f;

    // --- 色定義 ---
    [Header("色設定")]
    [SerializeField] private Color _confirmedColor = new Color(0.3f, 1f, 0.5f);
    [SerializeField] private Color _pendingColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color _confirmedBgColor = new Color(0.05f, 0.2f, 0.05f, 0.7f);
    [SerializeField] private Color _pendingBgColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("ガイド表示のルート")]
    [SerializeField] private RectTransform _guideRoot;

    [Header("Prefab")]
    [SerializeField] private GameObject _candidateRowPrefab;
    [SerializeField] private GameObject _signSlotPrefab;

    // 動的に生成した行を保持
    private readonly List<GameObject> _activeRows = new();

    // 確定エフェクト用
    private RectTransform _lastConfirmedSlot;

    /// <summary>
    /// マッチ候補と入力済み印数に基づいてガイド表示を更新する。
    /// </summary>
    public void UpdateGuide(IReadOnlyList<SpellData> candidates, int confirmedCount)
    {
        Clear();

        int displayCount = Mathf.Min(candidates.Count, MaxDisplayCount);

        for (int i = 0; i < displayCount; i++)
        {
            var row = CreateCandidateRow(candidates[i], confirmedCount);
            _activeRows.Add(row);
        }
    }

    /// <summary>
    /// ガイドを非表示にする。
    /// </summary>
    public void Clear()
    {
        foreach (var row in _activeRows)
        {
            Destroy(row);
        }
        _activeRows.Clear();
        _lastConfirmedSlot = null;
    }

    /// <summary>
    /// 最後に確定した印のスロットにScalePunchエフェクトを再生する。
    /// </summary>
    public void PlayConfirmEffect()
    {
        if (_lastConfirmedSlot == null) return;

        LMotion.Create(PunchScaleAmount, 1f, PunchDuration)
            .WithEase(Ease.OutBack)
            .Bind(s => _lastConfirmedSlot.localScale = new Vector3(s, s, 1f));
    }

    /// <summary>
    /// 候補1術分の行を生成する。
    /// PrefabからInstantiateし、術名と各印スロットを設定する。
    /// </summary>
    private GameObject CreateCandidateRow(SpellData spell, int confirmedCount)
    {
        var row = Instantiate(_candidateRowPrefab, _guideRoot);
        row.name = $"Row_{spell.spellName}";

        // 術名テキストを設定
        var spellNameText = row.GetComponentInChildren<TextMeshProUGUI>();
        spellNameText.text = spell.spellName;

        // SignSlotの親となるコンテナを取得(術名テキストの次の子要素)
        var slotContainer = row.transform.GetChild(1);

        // 各印のスロットを生成
        for (int i = 0; i < spell.sequence.Length; i++)
        {
            bool isConfirmed = i < confirmedCount;
            var slot = CreateSignSlot(slotContainer, spell.sequence[i], isConfirmed);

            if (i == confirmedCount - 1)
            {
                _lastConfirmedSlot = slot.GetComponent<RectTransform>();
            }
        }

        return row;
    }

    /// <summary>
    /// 印1つ分のスロットをPrefabから生成し、状態に応じて色を設定する。
    /// </summary>
    private GameObject CreateSignSlot(Transform parent, HandSign sign, bool isConfirmed)
    {
        var slot = Instantiate(_signSlotPrefab, parent);
        slot.name = $"Slot_{sign}";

        // 背景色の設定
        var bgImage = slot.GetComponent<Image>();
        bgImage.color = isConfirmed ? _confirmedBgColor : _pendingBgColor;

        // 印名テキストの設定
        var label = slot.GetComponentInChildren<TextMeshProUGUI>();
        label.text = GetSignDisplayName(sign);
        label.color = isConfirmed ? _confirmedColor : _pendingColor;

        return slot;
    }

    /// <summary>
    /// HandSign enumを表示用の漢字名に変換する。
    /// β版でSpriteに差し替える場合、Prefab内のTextをImageに変え、
    /// ここをSprite参照に置き換える。
    /// </summary>
    private static string GetSignDisplayName(HandSign sign)
    {
        return sign switch
        {
            HandSign.Open => "開",
            HandSign.Fist => "握",
            HandSign.Point => "指",
            HandSign.Scissors => "刃",
            HandSign.Palm => "掌",
            _ => "？"
        };
    }
}