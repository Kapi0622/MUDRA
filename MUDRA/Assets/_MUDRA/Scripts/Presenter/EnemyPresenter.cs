using UnityEngine;
using R3;
using MUDRA.Data;

/// <summary>
/// 敵・背景・ステージ名・決着表示の演出を配線する（B4。仕様書 §8-3 Enemy MVP）。
/// 「ステージ進行に沿った表示」をまとめて受け持つ。
/// SectionProgressManager / EnemyStateManager / BattleModel を購読し、
/// EnemyView / BackgroundView / StageTitleView の命令的メソッドを呼ぶだけで、判定は持たない。
///
/// ダメージ適用などModelを操作する配線はBattlePresenterに残し、こちらは表示専用にしている。
/// </summary>
public class EnemyPresenter : MonoBehaviour
{
    [SerializeField] private EnemyView _enemyView;
    [SerializeField] private BackgroundView _backgroundView;
    [SerializeField] private StageTitleView _stageTitleView;
    [SerializeField] private BattleResultView _battleResultView;

    // ボス戦専用の演出（B4）。カメラとフラッシュはBattlePresenterと共有するViewだが、参照を持つだけで状態は持ち合わない
    [Header("ボス戦の演出")]
    [SerializeField] private BossEncounterView _bossEncounterView;
    [SerializeField] private CameraShakeView _cameraShakeView;
    [SerializeField] private ScreenFlashView _screenFlashView;

    private SectionProgressManager _sectionProgressManager;
    private PresentationTimingData _timing;

    private readonly CompositeDisposable _disposables = new();

    /// <summary>
    /// BattleInitializerから呼ばれる。StartStageより前に呼ぶこと（最初のステージ名と敵の登場を取りこぼさないため）。
    /// </summary>
    public void Initialize(
        SectionProgressManager sectionProgressManager,
        EnemyStateManager enemyStateManager,
        BattleModel battleModel,
        PresentationTimingData timing)
    {
        _sectionProgressManager = sectionProgressManager;
        _timing = timing;

        // --- ステージ開始 → ステージ名 ---
        sectionProgressManager.OnStageStarted
            .Subscribe(stage => _stageTitleView.ShowStageTitle(stage.stageName))
            .AddTo(_disposables);

        // --- 決着 → 討伐/敗北の表示。戦闘が再開したら消す（デバッグのステージ/セクションジャンプ） ---
        // ボスを倒した時は撃破演出を見せ切ってから「討伐」を出す。
        // OnStageClearedの時点ではCurrentSectionはまだ撃破したセクションを指している
        sectionProgressManager.OnStageCleared
            .Subscribe(_ => _battleResultView.ShowClear(
                _sectionProgressManager.CurrentSection.isBoss ? _timing.BossDefeatDuration : 0f))
            .AddTo(_disposables);

        sectionProgressManager.OnGameOver
            .Subscribe(_ => _battleResultView.ShowGameOver())
            .AddTo(_disposables);

        sectionProgressManager.OnSectionStarted
            .Subscribe(_ => _battleResultView.Hide())
            .AddTo(_disposables);

        // --- セクション開始 → 背景切替 + 敵の登場（ボスなら名前も出す） ---
        sectionProgressManager.OnSectionStarted
            .Subscribe(HandleSectionStarted)
            .AddTo(_disposables);

        // --- セクション撃破 → 撃破演出 → 前進演出 ---
        sectionProgressManager.OnSectionCleared
            .Subscribe(HandleSectionCleared)
            .AddTo(_disposables);

        // --- 敵の行動フェーズ ---
        // Chargingの表示はCurrentActionの側で行う。
        // EnemyStateManagerはフェーズ→行動の順に値を書き換えるため、
        // フェーズの通知時点ではまだ新しい行動（攻撃名・chargeTime）が取れない
        enemyStateManager.CurrentPhase
            .Subscribe(HandleEnemyPhase)
            .AddTo(_disposables);

        enemyStateManager.CurrentAction
            .Where(action => action.HasValue && enemyStateManager.CurrentPhase.CurrentValue == EnemyPhase.Charging)
            .Subscribe(action =>
            {
                var attack = action.Value.attackData;
                _enemyView.PlayCharging(attack.attackName, attack.chargeTime, action.Value.isHeavy);

                // ボスの大技だけ、画面全体でも危険を伝える
                if (action.Value.isHeavy && _sectionProgressManager.CurrentSection.isBoss)
                    _bossEncounterView.ShowHeavyWarning(attack.attackName);
            })
            .AddTo(_disposables);

        // --- 被弾 ---
        // 威力0の術（回復術）も OnSpellHit を流すため、0ダメージでは被弾リアクションを出さない
        battleModel.OnSpellHit
            .Where(result => result.TotalDamage > 0)
            .Subscribe(result => _enemyView.PlayHit(result.IsWeakness))
            .AddTo(_disposables);

        battleModel.OnDotTick
            .Subscribe(_ => _enemyView.PlayDotTick())
            .AddTo(_disposables);
    }

    private void HandleSectionStarted(StageSection section)
    {
        var stage = _sectionProgressManager.CurrentStage;
        var background = section.isBoss ? stage.bossBackgroundSprite : stage.roadBackgroundSprite;
        _backgroundView.SetBackground(background, section.isBoss);

        var enemy = section.enemyData;
        _enemyView.Appear(enemy.sprite, enemy.enemyName, section.isBoss);

        // ボスは登場シーケンス。各Viewが PresentationTimingData の同じ時間表で動く
        if (section.isBoss)
        {
            _bossEncounterView.PlayEncounter();
            _cameraShakeView.ShakeBossRoar(_timing.roar);
            _stageTitleView.ShowBossTitle(enemy.enemyName, _timing.roar);
        }
    }

    private void HandleSectionCleared(int clearedIndex)
    {
        _bossEncounterView.HideHeavyWarning();

        // OnSectionClearedの時点ではCurrentSectionはまだ撃破したセクションを指している
        if (_sectionProgressManager.CurrentSection.isBoss)
        {
            _bossEncounterView.PlayHitStop();
            _screenFlashView.FlashBossDefeat();
            _cameraShakeView.ShakeBossDefeat();
            _enemyView.PlayBossDefeat();
        }
        else
        {
            _enemyView.PlayDefeat();
        }

        // 最終セクション（ボス）の撃破はステージクリアで、先へは進まない
        bool hasNextSection = clearedIndex < _sectionProgressManager.SectionCount - 1;
        if (hasNextSection)
            _backgroundView.PlayAdvance(_timing.NormalDefeatDuration);
    }

    private void HandleEnemyPhase(EnemyPhase phase)
    {
        _enemyView.SetStunned(phase == EnemyPhase.Stunned);

        // ボスの大技予告は予告中だけ。攻撃・Stun・ループ停止のどれで抜けても消す
        if (phase != EnemyPhase.Charging)
            _bossEncounterView.HideHeavyWarning();

        switch (phase)
        {
            case EnemyPhase.Attacking:
                _enemyView.PlayAttack();
                break;
            case EnemyPhase.Idle:
                // 攻撃後の待機、StopLoop（決着・敵の差し替え）のどちらでも予告は消しておく
                _enemyView.HideChargeGauge();
                break;
        }
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}
