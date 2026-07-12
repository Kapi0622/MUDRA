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
/// 将来的にON/OFFトグル(難易度設定)に対応する想定。
/// </summary>
public class SequenceGuideView : MonoBehaviour
{
    // --- 表示制限 ---
    // 術が増えた際に画面を圧迫しないよう、表示する候補数の上限を設ける
    private const int MaxDisplayCount = 4;

    // --- 確定エフェクト定数 ---
    private const float PunchScaleAmount = 1.3f;
    private const float PunchDuration = 0.15f;

    // --- 色定義 ---
    private static readonly Color ConfirmedColor = new Color(0.2f, 1f, 0.4f);   // 確定済みの印
    private static readonly Color PendingColor = new Color(0.6f, 0.6f, 0.6f);   // 未入力の印
    private static readonly Color SpellNameColor = new Color(1f, 0.9f, 0.5f);   // 術名の色

    [Header("ガイド表示のルート")]
    [SerializeField] private RectTransform _guideRoot;

    // 動的に生成した行を保持(Clear時に破棄する)
    private readonly List<GameObject> _activeRows = new();

    // 確定エフェクト用: 最後に確定した印のTransformを保持
    private RectTransform _lastConfirmedSlot;

    /// <summary>
    /// マッチ候補と入力済み印数に基づいてガイド表示を更新する。
    /// AddSignのたびにPresenterから呼ばれる。
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
    /// ガイドを非表示にする。術発動・キャンセル・暴発時に呼ばれる。
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
    /// UpdateGuideの後にPresenterから呼ばれる。
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
    /// 術名 + 各印のスロット(確定済み/未入力で色分け)を横並びで表示する。
    ///
    /// Hierarchy:
    ///   CandidateRow (HorizontalLayoutGroup)
    ///     ├─ SpellNameText (術名)
    ///     ├─ SignSlot_0 (Background + Label)
    ///     ├─ SignSlot_1
    ///     └─ ...
    /// </summary>
    private GameObject CreateCandidateRow(SpellData spell, int confirmedCount)
    {
        // --- 行のルート ---
        var row = new GameObject($"Row_{spell.spellName}", typeof(RectTransform));
        row.transform.SetParent(_guideRoot, false);

        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        // ContentSizeFitter で中身に合わせて行サイズを自動調整
        var rowFitter = row.AddComponent<ContentSizeFitter>();
        rowFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // --- 術名ラベル ---
        CreateTextElement(row.transform, spell.spellName, SpellNameColor, 20f);

        // --- 各印のスロット ---
        for (int i = 0; i < spell.sequence.Length; i++)
        {
            bool isConfirmed = i < confirmedCount;
            var slot = CreateSignSlot(
                row.transform,
                spell.sequence[i],
                isConfirmed
            );

            // 最後に確定した印(confirmedCount - 1)のTransformを記憶する
            if (i == confirmedCount - 1)
            {
                _lastConfirmedSlot = slot.GetComponent<RectTransform>();
            }
        }

        return row;
    }

    /// <summary>
    /// 印1つ分のスロットを生成する。
    /// 背景Image + 印名テキストの構成。
    /// </summary>
    private static GameObject CreateSignSlot(Transform parent, HandSign sign, bool isConfirmed)
    {
        // 背景
        var slot = new GameObject($"Slot_{sign}", typeof(RectTransform), typeof(Image));
        slot.transform.SetParent(parent, false);

        var slotImage = slot.GetComponent<Image>();
        slotImage.color = isConfirmed
            ? new Color(ConfirmedColor.r, ConfirmedColor.g, ConfirmedColor.b, 0.3f)
            : new Color(0f, 0f, 0f, 0.4f);
        slotImage.raycastTarget = false;

        var slotLayout = slot.AddComponent<LayoutElement>();
        slotLayout.preferredWidth = 44f;
        slotLayout.preferredHeight = 44f;

        // 印名テキスト
        var label = GetSignDisplayName(sign);
        var labelColor = isConfirmed ? ConfirmedColor : PendingColor;
        var textObj = CreateTextElement(slot.transform, label, labelColor, 18f);
        var textRect = textObj.GetComponent<RectTransform>();
        // スロット全体に広げて中央揃え
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        return slot;
    }

    /// <summary>
    /// TextMeshProUGUIを持つGameObjectを生成するヘルパー。
    /// </summary>
    private static GameObject CreateTextElement(
        Transform parent, string text, Color color, float fontSize)
    {
        var obj = new GameObject("Text", typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return obj;
    }

    /// <summary>
    /// HandSign enumを表示用の漢字名に変換する。
    /// β版でSpriteに差し替える場合、この変換をSprite参照に置き換えるだけで済む。
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