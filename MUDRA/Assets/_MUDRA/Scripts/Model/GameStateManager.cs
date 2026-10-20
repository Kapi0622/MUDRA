using System;
using System.Collections.Generic;
using R3;

/// <summary>
/// ゲーム全体の画面フェーズを管理する（仕様書 §4-1）。
///
/// 持つのは「今どの画面か」「どのステージに挑んでいるか」「直前の結果」だけで、
/// シーンのロードや画面の切替は知らない。それはGameFlowPresenterが遷移イベントを受けて行う。
///
/// 許されていない遷移（Title から直接 Result へ等）は何もせず false を返す。
/// ボタンの連打で同じ遷移が二重に要求されても、2回目はフェーズが既に変わっているので弾かれる。
/// </summary>
public class GameStateManager : IDisposable
{
    private readonly ReactiveProperty<GamePhase> _currentPhase;
    /// <summary>現在のフェーズ</summary>
    public ReadOnlyReactiveProperty<GamePhase> CurrentPhase => _currentPhase;

    private readonly Subject<GameTransition> _onTransition = new();
    /// <summary>遷移のたびに発火する。リトライ（InGame→InGame）もここで流れる</summary>
    public Observable<GameTransition> OnTransition => _onTransition;

    /// <summary>選択できる全ステージ</summary>
    public IReadOnlyList<StageData> AllStages { get; }

    /// <summary>挑戦中（または直前に挑戦した）ステージ。ステージを選ぶまではnull</summary>
    public StageData SelectedStage { get; private set; }

    /// <summary>直前のバトルの結果。Result画面で使う。次のバトルを始めるとnullに戻る</summary>
    public BattleRecord? LastRecord { get; private set; }

    /// <param name="initialPhase">起動時のフェーズ。通常はTitle。エディタでInGameだけを再生した時はInGameで始める</param>
    /// <param name="initialStage">InGameで始める場合のステージ</param>
    public GameStateManager(StageData[] allStages, GamePhase initialPhase = GamePhase.Title, StageData initialStage = null)
    {
        AllStages = allStages ?? Array.Empty<StageData>();
        _currentPhase = new ReactiveProperty<GamePhase>(initialPhase);
        SelectedStage = initialStage;
    }

    /// <summary>Title → StageSelect</summary>
    public bool StartGame()
    {
        if (_currentPhase.Value != GamePhase.Title) return false;
        return Transition(GamePhase.StageSelect);
    }

    /// <summary>StageSelect → InGame。選んだステージでバトルを始める</summary>
    public bool SelectStage(StageData stage)
    {
        if (_currentPhase.Value != GamePhase.StageSelect || stage == null) return false;

        SelectedStage = stage;
        LastRecord = null;
        return Transition(GamePhase.InGame);
    }

    /// <summary>InGame → Result。バトルの決着を記録する</summary>
    public bool EndBattle(BattleRecord record)
    {
        if (_currentPhase.Value != GamePhase.InGame) return false;

        LastRecord = record;
        return Transition(GamePhase.Result);
    }

    /// <summary>InGame / Result → InGame。同じステージを最初からやり直す</summary>
    public bool Retry()
    {
        var phase = _currentPhase.Value;
        if (phase != GamePhase.InGame && phase != GamePhase.Result) return false;
        if (SelectedStage == null) return false;

        LastRecord = null;
        return Transition(GamePhase.InGame);
    }

    /// <summary>InGame / Result → StageSelect。バトル中ならリタイア扱い（結果は残さない）</summary>
    public bool ToStageSelect()
    {
        var phase = _currentPhase.Value;
        if (phase != GamePhase.InGame && phase != GamePhase.Result) return false;
        return Transition(GamePhase.StageSelect);
    }

    /// <summary>StageSelect / InGame / Result → Title</summary>
    public bool ToTitle()
    {
        if (_currentPhase.Value == GamePhase.Title) return false;
        return Transition(GamePhase.Title);
    }

    private bool Transition(GamePhase to)
    {
        var from = _currentPhase.Value;
        _currentPhase.Value = to;
        _onTransition.OnNext(new GameTransition(from, to));
        return true;
    }

    public void Dispose()
    {
        _currentPhase.Dispose();
        _onTransition.Dispose();
    }
}
