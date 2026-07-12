using System;
using System.Collections.Generic;
using System.Linq;
using MUDRA.Data;
using R3;

/// <summary>
/// シーケンスがリセットされた理由
/// Misfire(暴発)は OnSpellCast(IsSuccess=false) に統合されたため削除。
/// 現状はCancelのみだが、将来的なリセット理由の追加を見据えenum型自体は残す。
/// </summary>
public enum SequenceResetReason
{
    Cancel,   // 解除印によるキャンセル
}

/// <summary>
/// 手印シーケンスの入力履歴を保持し、発動印・解除印を受けて判定するPure C#クラス
/// 複数のSpellDataを対象に前方一致で候補を絞り込み、Release時に完全一致を判定する
/// </summary>
public class SpellSequenceModel : IDisposable
{
    // 速度ボーナスの基準時間(印1つあたりの許容秒数)
    // 詠唱時間が「印数 × この値」以内なら速度ボーナス適用
    private const float SpeedBonusTimePerSign = 1.0f;
    private const float SpeedBonusMultiplier = 1.5f;
    private const float NormalSpeedMultiplier = 1.0f;

    private readonly IReadOnlyList<SpellData> _allSpells;
    private readonly List<HandSign> _inputHistory = new List<HandSign>();
    private List<SpellData> _matchCandidates;
    
    /// <summary>現在マッチ候補として残っている術のリスト(View向け読み取り専用)</summary>
    public IReadOnlyList<SpellData> MatchCandidates => _matchCandidates;
    /// <summary>現在の入力済み印数</summary>
    public int InputCount => _inputHistory.Count;

    // 時刻取得を外部注入する(テスト時に偽装可能にするため)
    private readonly Func<float> _getTime;

    // 詠唱開始時刻。AddSignで履歴が空→非空になった瞬間に記録する
    private float _chantStartTime;

    private readonly Subject<SpellCastResult> _onSpellCast = new();
    private readonly Subject<SequenceResetReason> _onSequenceReset = new();
    private readonly Subject<Unit> _onChantStarted = new();
    private readonly Subject<HandSign> _onSignAdded = new();

    public Observable<SpellCastResult> OnSpellCast => _onSpellCast;
    public Observable<SequenceResetReason> OnSequenceReset => _onSequenceReset;
    public Observable<Unit> OnChantStarted => _onChantStarted;
    /// <summary>詠唱印がシーケンスに追加された通知。候補絞り込み完了後に発火する。</summary>
    public Observable<HandSign> OnSignAdded => _onSignAdded;
    
    public SpellSequenceModel(IReadOnlyList<SpellData> allSpells, Func<float> getTime)
    {
        _allSpells = allSpells;
        _getTime = getTime;
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
            _chantStartTime = _getTime();
            _onChantStarted.OnNext(Unit.Default);
        }

        var signIndex = _inputHistory.Count - 1;

        _matchCandidates = _matchCandidates
            .Where(spell => signIndex < spell.sequence.Length && spell.sequence[signIndex] == sign)
            .ToList();
        
        // 候補絞り込み完了後に発火
        _onSignAdded.OnNext(sign);
    }

    /// <summary>
    /// 発動印を受けたときに呼ぶ
    /// 目標シーケンスと完全一致していれば成功、それ以外は暴発として扱う
    /// 成功・暴発いずれもSpellCastResultとしてOnSpellCastに一本化する
    /// </summary>
    public void Release()
    {
        var matched = _matchCandidates
            .FirstOrDefault(spell => spell.sequence.Length == _inputHistory.Count);

        if (matched != null)
        {
            var speedBonus = CalculateSpeedBonus(_inputHistory.Count);
            _onSpellCast.OnNext(new SpellCastResult(true, matched, speedBonus));
        }
        else
        {
            // 暴発。SpeedBonusは意味を持たないためNormal値で埋める
            _onSpellCast.OnNext(new SpellCastResult(false, null, NormalSpeedMultiplier));
        }

        ResetState();
    }

    /// <summary>
    /// 解除印を受けたときに呼ぶ
    /// 入力履歴・候補ともにリセットする
    /// </summary>
    public void Cancel()
    {
        ResetState();
        _onSequenceReset.OnNext(SequenceResetReason.Cancel);
    }

    /// <summary>
    /// 詠唱時間から速度ボーナス倍率を算出する
    /// 印数に比例した基準時間以内に発動できたかどうかで判定する
    /// </summary>
    private float CalculateSpeedBonus(int signCount)
    {
        var totalChantTime = _getTime() - _chantStartTime;
        var threshold = signCount * SpeedBonusTimePerSign;
        return totalChantTime <= threshold ? SpeedBonusMultiplier : NormalSpeedMultiplier;
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
        _onSignAdded.Dispose();
    }
}