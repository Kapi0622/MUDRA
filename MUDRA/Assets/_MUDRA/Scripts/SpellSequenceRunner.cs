using UnityEngine;

/// <summary>
/// HandSignDetectorの確定通知を受け取り、SpellSequenceTrackerへ振り分ける配線役
/// 将来的に仕様書のHandSignPresenterへ発展させる想定
/// </summary>
public class SpellSequenceRunner : MonoBehaviour
{
    [SerializeField] private HandSignDetector _handSignDetector;
    [SerializeField] private SpellEffectView _spellEffectView;

    // P3は1術限定のため仮でハードコード（将来的にSpellDataへ移行）
    private static readonly HandSign[] TargetSequence =
    {
        HandSign.Point,
        HandSign.Fist,
        HandSign.Open,
    };

    private SpellSequenceTracker _tracker;

    private void Awake()
    {
        // 本クラス自身が生成した専有オブジェクトのため、購読解除が不要(生存期間が一致)
        _tracker = new SpellSequenceTracker(TargetSequence);
        _tracker.OnSpellCast += HandleSpellCast;
        _tracker.OnSequenceReset += HandleSequenceReset;
    }

    private void OnEnable()
    {
        _handSignDetector.OnSignConfirmed += HandleSignConfirmed;
    }

    private void OnDisable()
    {
        // 他のGameObjectが持つコンポーネントのイベントのため、明示的に購読解除が必要
        _handSignDetector.OnSignConfirmed -= HandleSignConfirmed;
    }

    private void HandleSignConfirmed(HandSign sign)
    {
        switch (sign)
        {
            case HandSign.Release:
                _tracker.Release();
                break;
            case HandSign.Cancel:
                _tracker.Cancel();
                break;
            default:
                _tracker.AddSign(sign);
                break;
        }
    }

    private void HandleSpellCast()
    {
        Debug.Log("[SpellSequence] 発動成功");
        _spellEffectView.PlayEffect();
    }

    private void HandleSequenceReset(SequenceResetReason reason)
    {
        Debug.Log($"[SpellSequence] リセット: {reason}");
        // TODO: 必要ならリセット時の演出をここに繋ぐ
    }
}