using UnityEngine;
using R3;

/// <summary>
/// BattleModel・EnemyStateManagerの通知を購読し、
/// バトル状態の変化をViewに反映する配線役。
/// A2ではデバッグログでの確認が中心。
/// </summary>
public class BattlePresenter : MonoBehaviour
{
    private BattleModel _battleModel;
    private EnemyStateManager _enemyStateManager;
    private SpellSequenceModel _spellSequenceModel;

    private readonly CompositeDisposable _disposables = new();

    public void Initialize(
        BattleModel battleModel,
        EnemyStateManager enemyStateManager,
        SpellSequenceModel spellSequenceModel)
    {
        _battleModel = battleModel;
        _enemyStateManager = enemyStateManager;
        _spellSequenceModel = spellSequenceModel;

        // --- HP監視 ---
        _battleModel.PlayerHp
            .Subscribe(hp => Debug.Log($"[Battle] PlayerHP: {hp}/{_battleModel.PlayerMaxHp}"))
            .AddTo(_disposables);

        _battleModel.BossHp
            .Subscribe(hp => Debug.Log($"[Battle] BossHP: {hp}/{_battleModel.BossMaxHp}"))
            .AddTo(_disposables);

        _battleModel.ComboCount
            .Subscribe(count => Debug.Log($"[Battle] Combo: {count}"))
            .AddTo(_disposables);

        // --- 勝敗 ---
        _battleModel.OnBattleEnd
            .Subscribe(isWin => Debug.Log($"[Battle] バトル終了: {(isWin ? "勝利" : "敗北")}"))
            .AddTo(_disposables);

        // --- ボス攻撃 → ダメージ適用 ---
        _enemyStateManager.OnAttackExecuted
            .Subscribe(HandleEnemyAttack)
            .AddTo(_disposables);

        _enemyStateManager.CurrentPhase
            .Subscribe(phase => Debug.Log($"[Enemy] Phase: {phase}"))
            .AddTo(_disposables);

        // --- 術発動 → ダメージ適用 ---
        _spellSequenceModel.OnSpellCast
            .Subscribe(spell => _battleModel.ApplySpellDamage(spell))
            .AddTo(_disposables);

        _spellSequenceModel.OnSequenceReset
            .Subscribe(reason =>
            {
                if (reason == SequenceResetReason.Misfire)
                    _battleModel.ApplyMisfireDamage();
            })
            .AddTo(_disposables);
    }

    private void HandleEnemyAttack(EnemyAction action)
    {
        // A2ではGuarding未実装のため常にfalse
        bool isGuarding = false;
        _battleModel.ApplyEnemyDamage(action, isGuarding);

        string attackType = action.isHeavy ? "大技" : "通常";
        Debug.Log($"[Enemy] 攻撃: {action.attackData.attackName}({attackType}) DMG:{action.attackData.damage}");
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}