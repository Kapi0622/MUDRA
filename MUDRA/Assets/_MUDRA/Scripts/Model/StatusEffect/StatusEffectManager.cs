using System.Collections.Generic;

/// <summary>
/// アクティブな時限効果をコレクションで管理し、毎フレームTickで駆動する。
/// BattlePresenter.Update()からTick(deltaTime)を呼ぶ。
/// </summary>
public class StatusEffectManager
{
    private readonly List<IStatusEffect> _activeEffects = new();

    /// <summary>
    /// 効果を登録して開始する。
    /// 同種の効果が既にアクティブな場合は無視する（A5方針: 重複不可）。
    /// </summary>
    public void ApplyEffect(IStatusEffect effect)
    {
        // 同種効果の重複チェック
        for (int i = 0; i < _activeEffects.Count; i++)
        {
            if (_activeEffects[i].Type == effect.Type)
                return;
        }

        effect.OnApply();
        _activeEffects.Add(effect);
    }

    /// <summary>
    /// 毎フレーム呼ばれる。各効果の時間を進め、期限切れを除去する。
    /// </summary>
    public void Tick(float deltaTime)
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            // ClearAll等で外部からリストが変更された場合の安全ガード
            if (i >= _activeEffects.Count) continue;
            
            _activeEffects[i].OnTick(deltaTime);

            if (i < _activeEffects.Count && _activeEffects[i].IsExpired)
            {
                _activeEffects[i].OnExpire();
                _activeEffects.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 全効果を即時終了する。バトル終了時に呼ぶ。
    /// </summary>
    public void ClearAll()
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            _activeEffects[i].OnExpire();
        }
        _activeEffects.Clear();
    }
}