using System.Collections.Generic;
using System.Linq;
using MUDRA.Data;
using R3;
using UnityEngine;

/// <summary>
/// App側の画面（Title / StageSelect / Result）を配線する（B5）。Calibration（B6）の中身はCalibrationPresenterが配線し、ここは出し入れだけ行う。
/// 画面からの要求をGameStateManagerのメソッドに渡し、画面に出す内容をModelとDataから組み立てて渡す。
/// どの画面をいつ出すか（遷移演出の途中で切り替える）はGameFlowPresenterが決め、ShowScreenを呼ぶ。
/// </summary>
public class ScreenPresenter : MonoBehaviour
{
    [SerializeField] private TitleView _titleView;
    [SerializeField] private StageSelectView _stageSelectView;
    [SerializeField] private ResultView _resultView;
    [SerializeField] private CalibrationPresenter _calibrationPresenter;

    [Tooltip("App側の画面の背景（AppCameraの子）。画面ごとに絵と色味を切り替える")]
    [SerializeField] private AppBackdropView _backdropView;

    [Tooltip("弱点属性の文字色に使う")]
    [SerializeField] private BattlePaletteData _palette;

    [Header("サウンド（B7）")]
    [SerializeField] private SePlayer _se;
    [SerializeField] private SoundData _sound;

    // --- Result画面の戦績の項目名 ---
    private const string TimeLabel = "討伐時間";
    private const string TimeLabelOnLose = "戦った時間";
    private const string MaxComboLabel = "最大コンボ";
    private const string WeaknessLabel = "弱点を突いた回数";
    private const string GuardLabel = "ガード成功";
    private const string SpellsLabel = "術の発動";
    private const string MisfireLabel = "暴発";
    private const string DamageLabel = "受けたダメージ";

    private GameStateManager _gameStateManager;
    private IReadOnlyList<SpellData> _allSpells;
    private ProgressModel _progressModel;
    private SettingsPresenter _settingsPresenter;
    private readonly CompositeDisposable _disposables = new();

    /// <summary>AppInitializerから呼ばれる</summary>
    public void Initialize(
        GameStateManager gameStateManager,
        ProgressModel progressModel,
        SettingsPresenter settingsPresenter,
        IReadOnlyList<SpellData> allSpells)
    {
        _gameStateManager = gameStateManager;
        _progressModel = progressModel;
        _settingsPresenter = settingsPresenter;
        _allSpells = allSpells;

        HideAll();
        _stageSelectView.SetStages(BuildStageCards());

        _titleView.OnStartRequested
            .Subscribe(_ =>
            {
                _se.Play(_sound.uiDecide);
                _gameStateManager.StartGame();
            })
            .AddTo(_disposables);

        _titleView.OnSettingsRequested
            .Subscribe(_ =>
            {
                _se.Play(_sound.uiDecide);
                _settingsPresenter.Open();
            })
            .AddTo(_disposables);

        // 設定画面を開いている間は、下のタイトル画面のキー操作（Spaceでスタート）を止める
        _settingsPresenter.IsOpen
            .Skip(1)
            .Where(_ => _gameStateManager.CurrentPhase.CurrentValue == GamePhase.Title)
            .Subscribe(isOpen =>
            {
                if (isOpen) _titleView.Lock();
                else _titleView.Show();
            })
            .AddTo(_disposables);

        _stageSelectView.OnStageDecided
            .Subscribe(index =>
            {
                _se.Play(_sound.uiDecide);
                _gameStateManager.SelectStage(_gameStateManager.AllStages[index]);
            })
            .AddTo(_disposables);

        _stageSelectView.OnCursorMoved
            .Subscribe(_ => _se.Play(_sound.uiCursor))
            .AddTo(_disposables);

        // 選択中のステージの絵を背景に出す（画面を開いた時の初期位置でも流れる）
        _stageSelectView.OnSelectionChanged
            .Subscribe(index => _backdropView.ShowStage(_gameStateManager.AllStages[index].selectThumbnail))
            .AddTo(_disposables);

        _stageSelectView.OnBackRequested
            .Subscribe(_ =>
            {
                _se.Play(_sound.uiBack);
                _gameStateManager.ToTitle();
            })
            .AddTo(_disposables);

        _resultView.OnRetryRequested
            .Subscribe(_ =>
            {
                _se.Play(_sound.uiDecide);
                _gameStateManager.Retry();
            })
            .AddTo(_disposables);

        _resultView.OnStageSelectRequested
            .Subscribe(_ =>
            {
                _se.Play(_sound.uiDecide);
                _gameStateManager.ToStageSelect();
            })
            .AddTo(_disposables);

        _resultView.OnTitleRequested
            .Subscribe(_ =>
            {
                _se.Play(_sound.uiDecide);
                _gameStateManager.ToTitle();
            })
            .AddTo(_disposables);

        // ランクのハンコが押された瞬間。自己ベストなら祝いの音を重ねる
        _resultView.OnRankStamped
            .Subscribe(_ =>
            {
                _se.Play(_sound.rankStamp);
                if (_progressModel.LastResultWasNewBest) _se.Play(_sound.newRecord);
            })
            .AddTo(_disposables);
    }

    /// <summary>フェーズに対応する画面だけを出す。InGame・Tutorialではどの画面も出さない</summary>
    public void ShowScreen(GamePhase phase)
    {
        HideAll();

        switch (phase)
        {
            case GamePhase.Title:
                _backdropView.ShowTitle();
                _titleView.Show();
                _titleView.PlayIntro();
                break;
            case GamePhase.Calibration:
                _backdropView.ShowMenu();
                _calibrationPresenter.Show();
                break;
            case GamePhase.StageSelect:
                _backdropView.ShowMenu();
                // クリア記録が変わっているかもしれないので、出すたびに作り直す
                _stageSelectView.SetStages(BuildStageCards());
                _stageSelectView.Show(IndexOfSelectedStage());
                break;
            case GamePhase.Result:
                var resultData = BuildResultData();
                _backdropView.ShowResult(resultData.IsWin);
                _resultView.Show(resultData);
                break;
        }
    }

    /// <summary>遷移が始まったら、覆われるまでの間の操作を止める（二重の遷移要求を防ぐ）</summary>
    public void LockAll()
    {
        _titleView.Lock();
        _stageSelectView.Lock();
        _resultView.Lock();
        _calibrationPresenter.Lock();
    }

    public void HideAll()
    {
        _titleView.Hide();
        _stageSelectView.Hide();
        _resultView.Hide();
        _calibrationPresenter.Hide();
    }

    private int IndexOfSelectedStage()
    {
        var stages = _gameStateManager.AllStages;
        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i] == _gameStateManager.SelectedStage) return i;
        }
        return 0;
    }

    private List<StageCardData> BuildStageCards()
    {
        var cards = new List<StageCardData>();
        foreach (var stage in _gameStateManager.AllStages)
        {
            var boss = stage.FindBoss();
            var weakElement = boss != null ? boss.weakElement : default;
            cards.Add(new StageCardData(
                stage.stageNumber,
                stage.stageName,
                boss != null ? boss.enemyName : "？",
                boss != null ? ElementNames.Get(weakElement) : "？",
                _palette != null ? _palette.ElementColor(weakElement) : Color.white,
                BuildRecordText(stage),
                stage.selectThumbnail));
        }
        return cards;
    }

    private ResultScreenData BuildResultData()
    {
        var record = _gameStateManager.LastRecord;
        if (!record.HasValue)
            return new ResultScreenData(false, string.Empty, string.Empty,
                new string[0], new string[0], string.Empty, false);

        var r = record.Value;
        var stats = r.Stats;

        var labels = new[]
        {
            r.IsWin ? TimeLabel : TimeLabelOnLose, MaxComboLabel, WeaknessLabel,
            GuardLabel, SpellsLabel, MisfireLabel, DamageLabel,
        };
        var values = new[]
        {
            FormatTime(stats.ElapsedSeconds),
            stats.MaxCombo.ToString(),
            stats.WeaknessHits.ToString(),
            stats.GuardSuccesses.ToString(),
            stats.SpellsCast.ToString(),
            stats.Misfires.ToString(),
            stats.DamageTaken.ToString(),
        };

        return new ResultScreenData(
            r.IsWin,
            r.Stage != null ? r.Stage.stageName : string.Empty,
            r.Rank.HasValue ? r.Rank.Value.ToString() : string.Empty,
            labels,
            values,
            r.IsWin ? string.Empty : BuildDefeatHint(r.LastEnemy),
            _progressModel.LastResultWasNewBest);
    }

    /// <summary>カードに出すクリア記録。未クリアなら空</summary>
    private string BuildRecordText(StageData stage)
    {
        var record = _progressModel.FindRecord(stage);
        if (record == null) return string.Empty;

        return $"討伐済　{(BattleRank)record.bestRank}　{FormatTime(record.bestTimeSeconds)}";
    }

    /// <summary>「分:秒」。1時間を超えることは想定しない</summary>
    private static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60}:{total % 60:00}";
    }

    /// <summary>
    /// 敗北時のヒント。負けた相手の弱点と、その属性で攻撃できる術（印の並び付き）を出す。
    /// StageSelectの弱点表示と同じEnemyData.weakElementを使う。
    /// </summary>
    private string BuildDefeatHint(EnemyData enemy)
    {
        if (enemy == null) return string.Empty;

        string element = ElementNames.Get(enemy.weakElement);
        var spells = _allSpells
            .Where(spell => spell != null && spell.element == enemy.weakElement && spell.basePower > 0f)
            .Select(spell => $"{spell.spellName}（{string.Join(" → ", spell.sequence.Select(SequenceGuideView.GetSignDisplayName))} → 発）")
            .ToArray();

        string hint = $"{enemy.enemyName}は{element}に弱い";
        return spells.Length > 0 ? $"{hint}。　{string.Join("　", spells)}" : hint;
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}
