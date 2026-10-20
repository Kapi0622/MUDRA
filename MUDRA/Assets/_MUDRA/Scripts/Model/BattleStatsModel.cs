using System;
using R3;

/// <summary>
/// 1回のバトルの戦績を集計する（B5）。既存のModelの通知を購読して数えるだけで、バトルの進行には関わらない。
///
/// 時間はステージ開始から決着までをTickで積算する。BattlePresenterがゲーム内時間のdeltaTimeで呼ぶので、
/// ポーズ中・ヒットストップ中は進まない。
/// </summary>
public class BattleStatsModel : IDisposable
{
    private float _elapsedSeconds;
    private int _maxCombo;
    private int _weaknessHits;
    private int _guardSuccesses;
    private int _spellsCast;
    private int _misfires;
    private int _damageTaken;

    private bool _isRunning;
    private readonly CompositeDisposable _disposables = new();

    public BattleStatsModel(
        BattleModel battleModel,
        SpellSequenceModel spellSequenceModel,
        SectionProgressManager sectionProgressManager)
    {
        // デバッグのステージジャンプで別のステージを始め直した時は数え直す
        sectionProgressManager.OnStageStarted
            .Subscribe(_ => Reset())
            .AddTo(_disposables);

        // デバッグのセクションジャンプで決着後に再開した時も時計を動かす
        sectionProgressManager.OnSectionStarted
            .Subscribe(_ => _isRunning = true)
            .AddTo(_disposables);

        Observable.Merge(sectionProgressManager.OnStageCleared, sectionProgressManager.OnGameOver)
            .Subscribe(_ => _isRunning = false)
            .AddTo(_disposables);

        battleModel.ComboCount
            .Subscribe(count => _maxCombo = Math.Max(_maxCombo, count))
            .AddTo(_disposables);

        battleModel.OnSpellHit
            .Where(result => result.IsWeakness && result.TotalDamage > 0)
            .Subscribe(_ => _weaknessHits++)
            .AddTo(_disposables);

        battleModel.OnPlayerDamaged
            .Subscribe(info =>
            {
                _damageTaken += info.Damage;
                if (info.WasGuarded) _guardSuccesses++;
            })
            .AddTo(_disposables);

        // 決着後の入力はHandSignPresenterで止めているが、数えるのは進行中だけにしておく
        spellSequenceModel.OnSpellCast
            .Where(_ => _isRunning)
            .Subscribe(result =>
            {
                if (result.IsSuccess) _spellsCast++;
                else _misfires++;
            })
            .AddTo(_disposables);
    }

    /// <summary>ゲーム内時間を進める。毎フレーム呼ばれる</summary>
    public void Tick(float deltaTime)
    {
        if (_isRunning) _elapsedSeconds += deltaTime;
    }

    /// <summary>現時点の戦績</summary>
    public BattleStats CreateSnapshot()
    {
        return new BattleStats(
            _elapsedSeconds, _maxCombo, _weaknessHits, _guardSuccesses, _spellsCast, _misfires, _damageTaken);
    }

    private void Reset()
    {
        _elapsedSeconds = 0f;
        _maxCombo = 0;
        _weaknessHits = 0;
        _guardSuccesses = 0;
        _spellsCast = 0;
        _misfires = 0;
        _damageTaken = 0;
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
