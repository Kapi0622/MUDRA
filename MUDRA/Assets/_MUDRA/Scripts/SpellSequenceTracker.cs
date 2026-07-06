using System;
using System.Collections.Generic;

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
/// P3では単一の目標シーケンスとの完全一致のみを扱う
/// </summary>
public class SpellSequenceTracker
{
    private readonly HandSign[] _targetSequence;
    private readonly List<HandSign> _inputHistory = new List<HandSign>();

    public event Action OnSpellCast;
    public event Action<SequenceResetReason> OnSequenceReset;

    public SpellSequenceTracker(HandSign[] targetSequence)
    {
        _targetSequence = targetSequence;
    }

    /// <summary>
    /// 詠唱印を1つ積む（Release・Cancelはここに渡さない）
    /// </summary>
    public void AddSign(HandSign sign)
    {
        _inputHistory.Add(sign);
    }

    /// <summary>
    /// 発動印を受けたときに呼ぶ
    /// 目標シーケンスと完全一致していれば成功、それ以外は暴発として扱う
    /// </summary>
    public void Release()
    {
        if (IsSequenceMatched())
        {
            OnSpellCast?.Invoke();
        }
        else
        {
            OnSequenceReset?.Invoke(SequenceResetReason.Misfire);
        }

        _inputHistory.Clear();
    }

    /// <summary>
    /// 解除印を受けたときに呼ぶ
    /// 即座に入力履歴をクリアする
    /// </summary>
    public void Cancel()
    {
        _inputHistory.Clear();
        OnSequenceReset?.Invoke(SequenceResetReason.Cancel);
    }

    private bool IsSequenceMatched()
    {
        if (_inputHistory.Count != _targetSequence.Length)
            return false;

        for (int i = 0; i < _targetSequence.Length; i++)
        {
            if (_inputHistory[i] != _targetSequence[i])
                return false;
        }

        return true;
    }
}