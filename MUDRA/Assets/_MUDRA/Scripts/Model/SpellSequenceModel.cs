using System;
using System.Collections.Generic;
using System.Linq;
using MUDRA.Data;
using R3;

/// <summary>
/// シーケンスがリセットされた理由
/// </summary>
public enum SequenceResetReason
{
    Cancel,   // 解除印によるキャンセル
    Misfire,  // 間違ったまま発動印を押した（暴発）、または空queueでの発動印
}

/// <summary>
/// 手印シーケンスの入力履歴を保持し、発動印・解除印を受けて判定するPure C#クラス
/// 複数のSpellDataを対象に前方一致で候補を絞り込み、Release時に完全一致を判定する
/// </summary>
public class SpellSequenceModel : IDisposable
{
    private readonly IReadOnlyList<SpellData> _allSpells;
    private readonly List<HandSign> _inputHistory = new List<HandSign>();
    private List<SpellData> _matchCandidates;

    private readonly Subject<SpellData> _onSpellCast = new();
    private readonly Subject<SequenceResetReason> _onSequenceReset = new();
    private readonly Subject<Unit> _onChantStarted = new();

    public Observable<SpellData> OnSpellCast => _onSpellCast;
    public Observable<SequenceResetReason> OnSequenceReset => _onSequenceReset;
    public Observable<Unit> OnChantStarted => _onChantStarted;

    public SpellSequenceModel(IReadOnlyList<SpellData> allSpells)
    {
        _allSpells = allSpells;
        _matchCandidates = new List<SpellData>(_allSpells);
    }

    /// <summary>
    /// 詠唱印を1つ積む（Release・Cancelはここに渡さない）
    /// </summary>
    public void AddSign(HandSign sign)
    {
        var wasEmpty = _inputHistory.Count == 0;
        _inputHistory.Add(sign);

        if (wasEmpty)
        {
            _onChantStarted.OnNext(Unit.Default);
        }
        
        var signIndex = _inputHistory.Count - 1;
        
        _matchCandidates =  _matchCandidates
            .Where(spell => signIndex < spell.sequence.Length && spell.sequence[signIndex] == sign)
            .ToList();
    }

    /// <summary>
    /// 発動印を受けたときに呼ぶ
    /// 目標シーケンスと完全一致していれば成功、それ以外は暴発として扱う
    /// </summary>
    public void Release()
    {
        var matched = _matchCandidates
            .FirstOrDefault(spell => spell.sequence.Length == _inputHistory.Count);
        
        if (matched != null)
        {
            _onSpellCast.OnNext(matched);
        }
        else
        {
            _onSequenceReset.OnNext(SequenceResetReason.Misfire);
        }

        ResetState();
    }

    /// <summary>
    /// 解除印を受けたときに呼ぶ
    /// 即座に入力履歴をクリアする
    /// </summary>
    public void Cancel()
    {
        _inputHistory.Clear();
        _onSequenceReset.OnNext(SequenceResetReason.Cancel);
    }

    private void ResetState()
    {
        _inputHistory.Clear();
        _matchCandidates = new List<SpellData>(_allSpells);
    }

    public void Dispose()
    {
        _onSpellCast.Dispose();
        _onSequenceReset.Dispose();
        _onChantStarted.Dispose();
    }
}