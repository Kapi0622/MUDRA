using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;
using MUDRA.Data;

/// <summary>
/// 敵・背景・ステージ名・決着表示の演出を配線する（B4。仕様書 §8-3 Enemy MVP）。
/// 「ステージ進行に沿った表示」をまとめて受け持つ。
/// SectionProgressManager / EnemyStateManager / BattleModel を購読し、
/// EnemyView / StageEnvironmentView / StageTitleView の命令的メソッドを呼ぶだけで、判定は持たない。
///
/// ダメージ適用などModelを操作する配線はBattlePresenterに残し、こちらは表示専用にしている。
/// </summary>
public class EnemyPresenter : MonoBehaviour
{
    [SerializeField] private EnemyView _enemyView;
    [Tooltip("3D環境と、セクション間の前進演出（3D化ステップ2b。旧BackgroundView）")]
    [SerializeField] private StageEnvironmentView _environmentView;
    [SerializeField] private StageTitleView _stageTitleView;
    [SerializeField] private BattleResultView _battleResultView;
    [SerializeField] private StageIntroView _stageIntroView;
    [Tooltip("ステージ開始の演出で、弱点属性を属性色で出すために使う")]
    [SerializeField] private BattlePaletteData _palette;

    // ボス戦専用の演出（B4）。カメラとフラッシュはBattlePresenterと共有するViewだが、参照を持つだけで状態は持ち合わない
    [Header("ボス戦の演出")]
    [SerializeField] private BossEncounterView _bossEncounterView;
    [SerializeField] private CameraShakeView _cameraShakeView;
    [SerializeField] private ScreenFlashView _screenFlashView;
    [Tooltip("撃破時のヒットストップ。timeScaleはポーズと共有するため、TimeScaleViewにだけ書き換えさせる（B5）")]
    [SerializeField] private TimeScaleView _timeScaleView;

    [Header("サウンド（B7）")]
    [SerializeField] private SePlayer _se;
    [Tooltip("勝敗のジングル・効果音の取り出し元。ステージの曲は StageData に持つ")]
    [SerializeField] private SoundData _sound;

    private SectionProgressManager _sectionProgressManager;
    private PresentationTimingData _timing;
    // App側に常駐しているBGMの再生役。BattleContext経由で受け取る（シーンをまたぐのでInspectorで結べない）
    private BgmPlayer _bgm;

    private readonly CompositeDisposable _disposables = new();

    /// <summary>
    /// BattleInitializerから呼ばれる。StartStageより前に呼ぶこと（最初のステージ名と敵の登場を取りこぼさないため）。
    /// </summary>
    public void Initialize(
        SectionProgressManager sectionProgressManager,
        EnemyStateManager enemyStateManager,
        BattleModel battleModel,
        PresentationTimingData timing,
        BgmPlayer bgm)
    {
        _sectionProgressManager = sectionProgressManager;
        _timing = timing;
        _bgm = bgm;

        // --- ステージ開始 → ステージ名 ---
        // 最初のステージ名はバトル開始前の演出（PlayStageIntroAsync）で出しているので、
        // ここで出すのはデバッグのステージジャンプで2回目以降のステージを始めた時だけ
        sectionProgressManager.OnStageStarted
            .Skip(1)
            .Subscribe(stage => _stageTitleView.ShowStageTitle(stage.stageName))
            .AddTo(_disposables);

        // --- 決着 → 討伐/敗北の表示。戦闘が再開したら消す（デバッグのステージ/セクションジャンプ） ---
        // ボスを倒した時は撃破演出を見せ切ってから「討伐」を出す。
        // OnStageClearedの時点ではCurrentSectionはまだ撃破したセクションを指している。
        // 勝敗のジングルは表示と同じ遅れで鳴らす。ボス曲はジングルの前に BgmPlayer が止める
        sectionProgressManager.OnStageCleared
            .Subscribe(_ =>
            {
                float delay = _sectionProgressManager.CurrentSection.isBoss ? _timing.BossDefeatDuration : 0f;
                _battleResultView.ShowClear(delay);
                _bgm.PlayJingle(_sound.victoryJingle, delay);
            })
            .AddTo(_disposables);

        sectionProgressManager.OnGameOver
            .Subscribe(_ =>
            {
                _battleResultView.ShowGameOver();
                _bgm.PlayJingle(_sound.defeatJingle);
            })
            .AddTo(_disposables);

        sectionProgressManager.OnSectionStarted
            .Subscribe(_ => _battleResultView.Hide())
            .AddTo(_disposables);

        // --- セクション開始 → 環境の位置合わせ + 敵の登場（ボスなら名前も出す） ---
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
                // せり上がる音の頂点が攻撃の瞬間に来るよう、溜め時間に合わせて伸縮する。溜めを抜けたら HandleEnemyPhase で止める
                _se.PlayHeld(_sound.enemyCharge, attack.chargeTime);

                // ボスの大技だけ、画面全体でも危険を伝える
                if (action.Value.isHeavy && _sectionProgressManager.CurrentSection.isBoss)
                {
                    _bossEncounterView.ShowHeavyWarning(attack.attackName);
                    _se.Play(_sound.heavyWarning);
                }
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

    /// <summary>
    /// ステージの見た目を開始前の状態にする（B5）。環境を用意してカメラを先頭セクションに置き、敵は登場まで隠す。
    /// 遷移の覆いがかかっている間（BattleInitializer.Initialize）に呼ぶ。開始演出の裏に仮の背景・敵が映らないようにするため。
    /// 先頭セクションの開始時にもう一度Build/MoveToSectionが呼ばれるが、同じステージ・同じ位置なので何もしない。
    /// 前の画面の曲もここで止める。ステージの曲は開始の演出（「結印！」）の後、先頭セクションの開始で始める（B7）。
    /// </summary>
    public void PrepareStage(StageData stage)
    {
        _bgm.Stop();
        _environmentView.Build(stage);
        _environmentView.MoveToSection(0);
        _enemyView.Hide();
    }

    /// <summary>
    /// ステージ開始の演出を流し、終わるまで待つ（B5）。BattleInitializerがこの後にステージを開始する。
    /// </summary>
    public UniTask PlayStageIntroAsync(StageData stage, CancellationToken token)
    {
        var boss = stage.FindBoss();
        var weakElement = boss != null ? boss.weakElement : default;
        var data = new StageCardData(
            stage.stageNumber,
            stage.stageName,
            boss != null ? boss.enemyName : "？",
            boss != null ? ElementNames.Get(weakElement) : "？",
            _palette != null ? _palette.ElementColor(weakElement) : Color.white,
            string.Empty);

        // 帯と同時に拍子木、帯が消えて「結印！」が出る時（introTitleDuration 後）に太鼓と掛け声
        _se.Play(_sound.stageIntro);
        _se.PlayDelayed(_sound.ketsuin, _timing.introTitleDuration);
        _se.PlayDelayed(_sound.ketsuinVoice, _timing.introTitleDuration);

        return _stageIntroView.PlayAsync(data, _timing.introTitleDuration, _timing.introCallDuration, token);
    }

    private void HandleSectionStarted(StageSection section)
    {
        // 通常の進行では前進演出で既に着いている。デバッグのステージ/セクションジャンプの時だけ作り直し・置き直しになる
        _environmentView.Build(_sectionProgressManager.CurrentStage);
        _environmentView.MoveToSection(_sectionProgressManager.CurrentSectionIndex.CurrentValue);

        // 道中は同じ曲なので頭に戻らない。ボス曲が未設定のステージ（チュートリアル）は道中の曲のまま
        var stage = _sectionProgressManager.CurrentStage;
        _bgm.Play(section.isBoss && stage.bossBgm != null ? stage.bossBgm : stage.bgm);

        var enemy = section.enemyData;
        _enemyView.Appear(enemy.sprite, enemy.enemyName, section.isBoss);

        // ボスは登場シーケンス。各Viewが PresentationTimingData の同じ時間表で動く
        if (section.isBoss)
        {
            _bossEncounterView.PlayEncounter();
            _cameraShakeView.ShakeBossRoar(_timing.roar);
            _stageTitleView.ShowBossTitle(enemy.enemyName, _timing.roar);
            _se.Play(_sound.bossAppear);
            _se.PlayDelayed(_sound.bossRoar, _timing.roar);
        }
        else
        {
            _se.Play(_sound.enemyAppear);
        }
    }

    private void HandleSectionCleared(int clearedIndex)
    {
        _bossEncounterView.HideHeavyWarning();

        // OnSectionClearedの時点ではCurrentSectionはまだ撃破したセクションを指している
        if (_sectionProgressManager.CurrentSection.isBoss)
        {
            _timeScaleView.PlayHitStop(_timing.hitStopRealtime, _timing.bossDefeatSlowScale, _timing.bossDefeatSlowRealtime);
            _screenFlashView.FlashBossDefeat();
            _cameraShakeView.ShakeBossDefeat();
            _enemyView.PlayBossDefeat();
            _se.Play(_sound.bossDefeat);
        }
        else
        {
            _enemyView.PlayDefeat();
            _se.Play(_sound.enemyDefeat);
        }

        // 最終セクション（ボス）の撃破はステージクリアで、先へは進まない
        bool hasNextSection = clearedIndex < _sectionProgressManager.SectionCount - 1;
        if (hasNextSection)
        {
            _environmentView.PlayAdvance(clearedIndex + 1, _timing.NormalDefeatDuration);
            _se.PlayDelayed(_sound.advance, _timing.NormalDefeatDuration);
        }
    }

    private void HandleEnemyPhase(EnemyPhase phase)
    {
        _enemyView.SetStunned(phase == EnemyPhase.Stunned);

        // ボスの大技予告と溜めの音は予告中だけ。攻撃・Stun・ループ停止のどれで抜けても消す
        if (phase != EnemyPhase.Charging)
        {
            _bossEncounterView.HideHeavyWarning();
            _se.StopHeld();
        }

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
