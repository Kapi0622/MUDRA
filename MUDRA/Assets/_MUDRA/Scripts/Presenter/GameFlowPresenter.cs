using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MUDRA.Data;
using MUDRA.HandTracking;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameStateManagerのフェーズ遷移を、実際の画面切替とシーンのロードにつなぐ（B5）。
///
/// 1回の遷移は「覆う → InGameのアンロード/ロード → 画面の切替 → 晴らす → バトル開始」の順に進む。
/// 遷移は1つずつ順番に処理する。演出の途中で次の遷移が要求されたら（決着の直後にRキーなど）、
/// 今の遷移を終えてから続けて処理する。途中で割り込むとシーンのロードとアンロードが交錯するため。
///
/// InGameは加算ロードし、読み込んだらアクティブシーンにする。
/// アクティブシーンにしないと、バトル中にInstantiateしたもの（ダメージ数字・パーティクル）がApp側に生成され、
/// アンロード後も残ってしまう。
/// </summary>
public class GameFlowPresenter : MonoBehaviour
{
    [SerializeField] private TransitionView _transitionView;
    [SerializeField] private ScreenPresenter _screenPresenter;
    [SerializeField] private SettingsPresenter _settingsPresenter;

    [Tooltip("BGMの再生役（B7）。バトル側へもBattleContextで渡す")]
    [SerializeField] private BgmPlayer _bgmPlayer;

    [Tooltip("App側のカメラ。InGameを読み込んでいる間はInGameのMain Cameraに任せて止める。\n" +
             "AudioListenerはカメラではなくAppの Audio に1つだけ置く（B7。カメラの切り替えで2つ重なる瞬間を作らない）")]
    [SerializeField] private GameObject _appCamera;

    private GameStateManager _gameStateManager;
    private IHandLandmarkProvider _provider;
    private HandPresenceModel _handPresence;
    private IReadOnlyList<SpellData> _allSpells;
    private ProgressModel _progressModel;
    private PresentationTimingData _timing;

    private Scene _battleScene;
    private BattleInitializer _battle;

    private readonly Queue<GameTransition> _pendingTransitions = new();
    private bool _isProcessing;

    private readonly CompositeDisposable _disposables = new();

    /// <summary>
    /// AppInitializerから呼ばれる。
    /// </summary>
    /// <param name="preloadedBattle">エディタでInGameだけを再生した時の、既に読み込まれているバトル。通常はnull</param>
    public void Initialize(
        GameStateManager gameStateManager,
        IHandLandmarkProvider provider,
        HandPresenceModel handPresence,
        IReadOnlyList<SpellData> allSpells,
        ProgressModel progressModel,
        PresentationTimingData timing,
        BattleInitializer preloadedBattle)
    {
        _progressModel = progressModel;
        _gameStateManager = gameStateManager;
        _provider = provider;
        _handPresence = handPresence;
        _allSpells = allSpells;
        _timing = timing;

        _gameStateManager.OnTransition
            .Subscribe(EnqueueTransition)
            .AddTo(_disposables);

        if (preloadedBattle != null)
        {
            StartPreloadedBattle(preloadedBattle);
            return;
        }

        // 通常の起動: 暗転した状態からタイトルを出す
        SetAppCameraActive(true);
        _transitionView.SetCovered(true);
        _screenPresenter.ShowScreen(_gameStateManager.CurrentPhase.CurrentValue);
        RevealAsync().Forget();
    }

    private async UniTaskVoid RevealAsync()
    {
        try
        {
            await _transitionView.RevealAsync(_timing.screenRevealDuration, destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 破棄された場合は何もしない
        }
    }

    /// <summary>InGameだけを再生した時: 遷移演出なしで、既にあるシーンのバトルをそのまま始める</summary>
    private void StartPreloadedBattle(BattleInitializer battle)
    {
        _battle = battle;
        _battleScene = battle.gameObject.scene;

        SetAppCameraActive(false);
        _screenPresenter.HideAll();
        _transitionView.SetCovered(false);

        if (_battle.Initialize(CreateBattleContext()))
            _battle.BeginBattle();
    }

    private void EnqueueTransition(GameTransition transition)
    {
        // 覆われるまでの間に、もう一度押されて同じ遷移が二重に要求されないようにする
        _screenPresenter.LockAll();

        _pendingTransitions.Enqueue(transition);
        if (!_isProcessing) ProcessTransitionsAsync().Forget();
    }

    private async UniTaskVoid ProcessTransitionsAsync()
    {
        _isProcessing = true;
        var token = destroyCancellationToken;

        try
        {
            while (_pendingTransitions.Count > 0)
            {
                await RunTransitionAsync(_pendingTransitions.Dequeue(), token);
            }
        }
        catch (OperationCanceledException)
        {
            // Appの破棄（アプリ終了）による中断
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async UniTask RunTransitionAsync(GameTransition transition, CancellationToken token)
    {
        // ポーズメニューから設定を開いたまま抜けることは無いが、念のため閉じてから覆う
        _settingsPresenter.Close();
        await _transitionView.CoverAsync(_timing.screenCoverDuration, token);

        _screenPresenter.HideAll();

        // チュートリアルもInGameシーンを使う（B6）
        if (GameStateManager.UsesBattleScene(transition.From))
            await UnloadBattleAsync(token);

        bool canBegin = false;
        if (GameStateManager.UsesBattleScene(transition.To))
            canBegin = await LoadBattleAsync(token);
        else
            _screenPresenter.ShowScreen(transition.To);

        await _transitionView.RevealAsync(_timing.screenRevealDuration, token);

        // 敵の登場は覆いが晴れてから始める（暗転中に登場演出が終わってしまわないように）
        if (canBegin) _battle.BeginBattle();
    }

    /// <returns>バトルを組み立てられたらtrue</returns>
    private async UniTask<bool> LoadBattleAsync(CancellationToken token)
    {
        await SceneManager.LoadSceneAsync(SceneNames.InGame, LoadSceneMode.Additive).ToUniTask(cancellationToken: token);

        _battleScene = SceneManager.GetSceneByName(SceneNames.InGame);
        SceneManager.SetActiveScene(_battleScene);
        SetAppCameraActive(false);

        _battle = FindBattleInitializer(_battleScene);
        if (_battle == null)
        {
            Debug.LogError("[GameFlowPresenter] InGameにBattleInitializerが見つかりません");
            return false;
        }

        return _battle.Initialize(CreateBattleContext());
    }

    private async UniTask UnloadBattleAsync(CancellationToken token)
    {
        if (!_battleScene.IsValid() || !_battleScene.isLoaded) return;

        // InGameのカメラが消える前にApp側のカメラへ戻す（カメラが無いフレームを作らない）
        SetAppCameraActive(true);
        SceneManager.SetActiveScene(gameObject.scene);

        var scene = _battleScene;
        _battle = null;
        _battleScene = default;
        await SceneManager.UnloadSceneAsync(scene).ToUniTask(cancellationToken: token);
    }

    private BattleContext CreateBattleContext()
    {
        bool isTutorial = _gameStateManager.CurrentPhase.CurrentValue == GamePhase.Tutorial;
        return new BattleContext(
            isTutorial ? _gameStateManager.TutorialStage : _gameStateManager.SelectedStage,
            _gameStateManager.AllStages,
            _allSpells,
            _provider,
            _gameStateManager,
            _progressModel.IsAutoPauseEnabled,
            _settingsPresenter,
            isTutorial,
            _handPresence,
            _bgmPlayer);
    }

    private static BattleInitializer FindBattleInitializer(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var battle = root.GetComponentInChildren<BattleInitializer>();
            if (battle != null) return battle;
        }
        return null;
    }

    private void SetAppCameraActive(bool isActive)
    {
        if (_appCamera != null) _appCamera.SetActive(isActive);
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}
