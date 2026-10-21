using MUDRA.Data;
using MUDRA.HandTracking;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 常駐シーン（App）のエントリーポイント（B5）。App側のModelの生成と各Presenterへの注入を集約する。
/// バトル側のBattleInitializerと同じ「Composition Root」の役で、App側にはシーンに1つだけ置く。
///
/// Appに常駐させるもの: GameStateManager、カメラ（MediaPipe）とProvider、Title / StageSelect / Result の画面、遷移演出。
/// カメラを常駐させることで、バトルやリトライのたびにWebカメラと推論を再起動しなくて済む。
/// </summary>
public class AppInitializer : MonoBehaviour
{
    [Header("Presenter参照")]
    [SerializeField] private GameFlowPresenter _gameFlowPresenter;
    [SerializeField] private ScreenPresenter _screenPresenter;
    [SerializeField] private SettingsPresenter _settingsPresenter;

    [Header("Input")]
    [SerializeField] private MediaPipeHandLandmarkProvider _provider;

    [Header("Data")]
    [Tooltip("全ステージ。StageSelectにこの順で並ぶ")]
    [SerializeField] private StageData[] _allStages;
    [Tooltip("全ての術。バトルの詠唱と、Result画面の敗北時ヒントで使う")]
    [SerializeField] private SpellData[] _allSpells;
    [Tooltip("画面遷移の尺を含む演出の時間表")]
    [SerializeField] private PresentationTimingData _presentationTiming;

    private GameStateManager _gameStateManager;
    private ProgressModel _progressModel;

    private readonly CompositeDisposable _disposables = new();

    private void Awake()
    {
        if (_allStages == null || _allStages.Length == 0)
        {
            Debug.LogError("[AppInitializer] StageDataが1つも設定されていません");
            return;
        }

        // エディタでInGameだけを再生した場合は、InGameが先に読み込まれていて、そこからAppが加算ロードされてくる。
        // その時はTitleを飛ばして、既にあるInGameでそのままバトルを始める
        var preloadedBattle = FindPreloadedBattle();

        _gameStateManager = preloadedBattle != null
            ? new GameStateManager(_allStages, GamePhase.InGame, ResolveDebugStartStage(preloadedBattle))
            : new GameStateManager(_allStages);

        _progressModel = new ProgressModel(new JsonFileSaveStorage());

        // バトルの結果をクリア記録へ。Result画面は遷移演出で覆った後に出るので、それまでに新記録かどうかが決まっている
        _gameStateManager.OnTransition
            .Where(transition => transition.To == GamePhase.Result && _gameStateManager.LastRecord.HasValue)
            .Subscribe(_ => _progressModel.RecordBattle(_gameStateManager.LastRecord.Value))
            .AddTo(_disposables);

        _settingsPresenter.Initialize(_progressModel);
        _screenPresenter.Initialize(_gameStateManager, _progressModel, _settingsPresenter, _allSpells);
        _gameFlowPresenter.Initialize(
            _gameStateManager, _provider, _allSpells, _progressModel, _presentationTiming, preloadedBattle);
    }

    /// <summary>既に読み込まれているInGameのBattleInitializer。通常の起動（Appから始まる）ではnull</summary>
    private static BattleInitializer FindPreloadedBattle()
    {
        var scene = SceneManager.GetSceneByName(SceneNames.InGame);
        if (!scene.isLoaded) return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var battle = root.GetComponentInChildren<BattleInitializer>();
            if (battle != null) return battle;
        }
        return null;
    }

    private StageData ResolveDebugStartStage(BattleInitializer battle)
    {
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (battle.DebugStartStage != null) return battle.DebugStartStage;
    #endif
        return _allStages[0];
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
        _gameStateManager?.Dispose();
        _progressModel?.Dispose();
    }
}
