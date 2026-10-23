using System.Collections.Generic;
using MUDRA.Data;
using MUDRA.HandTracking;
using R3;

/// <summary>
/// App側（常駐シーン）からInGameのBattleInitializerへ渡すもの一式（B5）。
///
/// シーンをまたぐ参照はInspectorで結べないため、AppのGameFlowPresenterがInGameを読み込んだ後に
/// これをBattleInitializer.Initializeへ渡す。static経由で受け渡さないための入れ物。
/// </summary>
public sealed class BattleContext
{
    /// <summary>挑戦するステージ</summary>
    public StageData Stage { get; }

    /// <summary>全ステージ。デバッグメニューのステージジャンプ候補に使う</summary>
    public IReadOnlyList<StageData> AllStages { get; }

    /// <summary>全ての術。Result画面の敗北時ヒントでも使うため、App側で1か所に持つ</summary>
    public IReadOnlyList<SpellData> AllSpells { get; }

    /// <summary>手のランドマークの提供元。カメラ（MediaPipe）はApp側に常駐している</summary>
    public IHandLandmarkProvider Provider { get; }

    /// <summary>決着・リトライ・リタイアの報告先</summary>
    public GameStateManager GameStateManager { get; }

    /// <summary>自動ポーズを行うか（設定画面の値）。バトル中に設定を変えても追従させるため購読できる形で渡す</summary>
    public ReadOnlyReactiveProperty<bool> IsAutoPauseEnabled { get; }

    /// <summary>ポーズメニューから設定画面を開くため。設定画面はApp側にある。無ければnull</summary>
    public SettingsPresenter Settings { get; }

    /// <summary>
    /// チュートリアル（練習バトル）として始めるか（B6）。trueならStageはチュートリアルのステージで、
    /// 決着してもResult画面へは行かない
    /// </summary>
    public bool IsTutorial { get; }

    /// <summary>手の映り具合（B6）。App側で常に判定していて、手を見失った時の自動ポーズに使う</summary>
    public HandPresenceModel HandPresence { get; }

    public BattleContext(
        StageData stage,
        IReadOnlyList<StageData> allStages,
        IReadOnlyList<SpellData> allSpells,
        IHandLandmarkProvider provider,
        GameStateManager gameStateManager,
        ReadOnlyReactiveProperty<bool> isAutoPauseEnabled,
        SettingsPresenter settings,
        bool isTutorial,
        HandPresenceModel handPresence)
    {
        Stage = stage;
        AllStages = allStages;
        AllSpells = allSpells;
        Provider = provider;
        GameStateManager = gameStateManager;
        IsAutoPauseEnabled = isAutoPauseEnabled;
        Settings = settings;
        IsTutorial = isTutorial;
        HandPresence = handPresence;
    }
}
