using UnityEngine;
using R3;

/// <summary>
/// BattleModel・EnemyStateManagerの通知を購読し、
/// バトル状態の変化をViewに反映する配線役。
/// </summary>
public class BattlePresenter : MonoBehaviour
{
    private BattleModel _battleModel;
    private EnemyStateManager _enemyStateManager;
    private SpellSequenceModel _spellSequenceModel;
    
    private HpBarView _playerHpBarView;
    private HpBarView _bossHpBarView;

    private readonly CompositeDisposable _disposables = new();

    public void Initialize(
        BattleModel battleModel,
        EnemyStateManager enemyStateManager,
        SpellSequenceModel spellSequenceModel,
        HpBarView _playerHpBarView,
        HpBarView _bossHpBarView)
    {
        _battleModel = battleModel;
        _enemyStateManager = enemyStateManager;
        _spellSequenceModel = spellSequenceModel;

        // --- HP初期表示(初期化アニメーションは今のところなし) ---
        _playerHpBarView.InitializeHp(_battleModel.PlayerHp.CurrentValue, _battleModel.PlayerMaxHp);
        _bossHpBarView.InitializeHp(_battleModel.BossHp.CurrentValue, _battleModel.BossMaxHp);
        
        // --- HP監視 ---
        _battleModel.PlayerHp
            .Skip(1)
            .Subscribe(hp => _playerHpBarView.SetHp(hp, _battleModel.PlayerMaxHp))
            .AddTo(_disposables);

        _battleModel.BossHp
            .Skip(1)
            .Subscribe(hp => _bossHpBarView.SetHp(hp, _battleModel.BossMaxHp))
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

        // --- 術発動結果 → ダメージ適用(成功・暴発の両方をBattleModelに委ねる) ---
        _spellSequenceModel.OnSpellCast
            .Subscribe(result => _battleModel.ApplySpellDamage(result))
            .AddTo(_disposables);

        // --- Cancel時のUI演出フック(現時点ではログのみ) ---
        _spellSequenceModel.OnSequenceReset
            .Subscribe(reason => Debug.Log($"[Spell] シーケンスリセット: {reason}"))
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