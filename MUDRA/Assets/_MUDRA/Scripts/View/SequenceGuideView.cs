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

    // --- 連続減衰の表示（B8）。DamageNumberView の「連続↓」と同じ灰色 ---
    private const string RepeatedSuffix = "（連続↓）";
    private static readonly Color RepeatedNameColor = new Color(0.62f, 0.64f, 0.68f);

    // --- 色定義(調整済みならSerializeFieldのまま残してOK) ---
    [SerializeField] private Color ConfirmedColor = new Color(0.3f, 1f, 0.5f);
    [SerializeField] private Color PendingColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color ConfirmedBgColor = new Color(0.05f, 0.2f, 0.05f, 0.7f);
    [SerializeField] private Color PendingBgColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("ガイド表示のルート")]
    [SerializeField] private RectTransform _guideRoot;

    [Header("Prefab")]
    [SerializeField] private GameObject _candidateRowPrefab;
    [SerializeField] private GameObject _signSlotPrefab;

    // 動的に生成した行を保持
    private readonly List<GameObject> _activeRows = new();

    // 確定エフェクト用(複数候補を同時にハイライトするためリスト化)
    private readonly List<RectTransform> _lastConfirmedSlots = new();
    private readonly List<MotionHandle> _confirmEffectHandles = new();

    /// <summary>
    /// マッチ候補と入力済み印数に基づいてガイド表示を更新する。
    /// </summary>
    /// <param name="repeatedElement">
    /// 連続減衰がかかる属性（直前に撃った攻撃術の属性・B8）。null なら減衰なし。
    /// この属性の攻撃術は名前を暗くして「（連続↓）」を付ける。詠唱中のどの術を選ぶか決める場面で見えるようにするため
    /// </param>
    public void UpdateGuide(IReadOnlyList<SpellData> candidates, int confirmedCount, ElementType? repeatedElement)
    {
        Clear();

        int displayCount = Mathf.Min(candidates.Count, MaxDisplayCount);

        for (int i = 0; i < displayCount; i++)
        {
            var spell = candidates[i];
            // 回復術（威力0）は減衰しないので印を付けない（BattleModel の判定と揃える）
            bool isRepeated = spell.basePower > 0f && spell.element == repeatedElement;
            var row = CreateCandidateRow(spell, confirmedCount, isRepeated);
            _activeRows.Add(row);
        }

        // LayoutGroupに再計算を強制する
        LayoutRebuilder.ForceRebuildLayoutImmediate(_guideRoot);
    }

    /// <summary>
    /// ガイドを非表示にする。
    /// </summary>
    public void Clear()
    {
        // 実行中の確定エフェクトをキャンセルしてからDestroyする
        CancelConfirmEffect();

        foreach (var row in _activeRows)
        {
            Destroy(row);
        }
        _activeRows.Clear();
        _lastConfirmedSlots.Clear();
    }

    /// <summary>
    /// 最後に確定した印に該当する、全候補のスロットにScalePunchエフェクトを再生する。
    /// </summary>
    public void PlayConfirmEffect()
    {
        if (_lastConfirmedSlots.Count == 0) return;

        CancelConfirmEffect();

        foreach (var target in _lastConfirmedSlots)
        {
            // ローカル変数にキャプチャしてラムダ内でnullチェックする
            var capturedTarget = target;

            var handle = LMotion.Create(PunchScaleAmount, 1f, PunchDuration)
                .WithEase(Ease.OutBack)
                .Bind(s =>
                {
                    if (capturedTarget != null)
                    {
                        capturedTarget.localScale = new Vector3(s, s, 1f);
                    }
                });

            _confirmEffectHandles.Add(handle);
        }
    }

    private void CancelConfirmEffect()
    {
        foreach (var handle in _confirmEffectHandles)
        {
            if (handle.IsActive())
            {
                handle.Cancel();
            }
        }
        _confirmEffectHandles.Clear();
    }

    private GameObject CreateCandidateRow(SpellData spell, int confirmedCount, bool isRepeated)
    {
        var row = Instantiate(_candidateRowPrefab, _guideRoot);
        row.name = $"Row_{spell.spellName}";

        var spellNameText = row.GetComponentInChildren<TextMeshProUGUI>();
        // 行はプレハブを毎回複製するので、色を変えても次の行に残らない
        spellNameText.text = isRepeated ? spell.spellName + RepeatedSuffix : spell.spellName;
        if (isRepeated) spellNameText.color = RepeatedNameColor;

        var slotContainer = row.transform.GetChild(1);

        for (int i = 0; i < spell.sequence.Length; i++)
        {
            bool isConfirmed = i < confirmedCount;
            var slot = CreateSignSlot(slotContainer, spell.sequence[i], isConfirmed);

            if (i == confirmedCount - 1)
            {
                _lastConfirmedSlots.Add(slot.GetComponent<RectTransform>());
            }
        }

        return row;
    }

    private GameObject CreateSignSlot(Transform parent, HandSign sign, bool isConfirmed)
    {
        var slot = Instantiate(_signSlotPrefab, parent);
        slot.name = $"Slot_{sign}";

        var bgImage = slot.GetComponent<Image>();
        bgImage.color = isConfirmed ? ConfirmedBgColor : PendingBgColor;

        var label = slot.GetComponentInChildren<TextMeshProUGUI>();
        label.text = GetSignDisplayName(sign);
        label.color = isConfirmed ? ConfirmedColor : PendingColor;

        return slot;
    }

    /// <summary>印の表示名（1文字）。術一覧（B5）・敗北時のヒント・判定中の印の表示でも使う</summary>
    public static string GetSignDisplayName(HandSign sign)
    {
        return sign switch
        {
            HandSign.Open => "開",
            HandSign.Fist => "握",
            HandSign.Point => "指",
            HandSign.Scissors => "刃",
            HandSign.Palm => "掌",
            HandSign.Union => "合",
            HandSign.DoubleScissors => "双",
            // 特殊印。術の sequence には入らないが、判定中の印の表示（HandSignStatusView）で使う
            HandSign.Release => "発",
            HandSign.Cancel => "散",
            HandSign.Guard => "盾",
            _ => "？"
        };
    }

    private void OnDestroy()
    {
        CancelConfirmEffect();
    }
}