using System.Collections.Generic;
using System.Linq;
using MUDRA.Data;
using MUDRA.HandTracking;
using R3;
using UnityEngine;

/// <summary>
/// ポーズを配線する（B5）。InGameに置き、BattleInitializerから注入を受ける。
///
/// PauseModelの状態をTimeScaleView（時間を止める）とPauseMenuView（メニュー・カウントダウン）に渡し、
/// メニューからの要求をPauseModel・GameStateManagerのメソッドに渡す。
/// 自動ポーズの材料（フォーカスの喪失・手が映っているか）もここからModelへ渡す。
/// </summary>
public class PausePresenter : MonoBehaviour
{
    private const string HandLostReasonText = "手が映っていません";
    private const string FocusLostReasonText = "画面の外が操作されました";

    [SerializeField] private PauseMenuView _pauseMenuView;
    [SerializeField] private SpellListView _spellListView;
    [SerializeField] private TimeScaleView _timeScaleView;

    [Tooltip("術一覧の属性名の色に使う")]
    [SerializeField] private BattlePaletteData _palette;

    private PauseModel _pauseModel;
    private IHandLandmarkProvider _provider;
    private bool _watchesHands;

    private readonly CompositeDisposable _disposables = new();

    /// <summary>BattleInitializerから呼ばれる</summary>
    /// <param name="watchesHands">手が映らない時に自動でポーズするか。キーボードモードではfalse</param>
    public void Initialize(
        PauseModel pauseModel,
        GameStateManager gameStateManager,
        IHandLandmarkProvider provider,
        bool watchesHands,
        IReadOnlyList<SpellData> spells,
        SettingsPresenter settings)
    {
        _pauseModel = pauseModel;
        _provider = provider;
        _watchesHands = watchesHands;

        _spellListView.SetSpells(BuildSpellRows(spells));
        _pauseMenuView.SetSettingsAvailable(settings != null);

        if (settings != null)
        {
            _pauseMenuView.OnSettingsRequested
                .Subscribe(_ => settings.Open())
                .AddTo(_disposables);

            // 設定画面を開いている間は、ポーズメニューのキー操作（Esc・Space）を止める
            settings.IsOpen
                .Subscribe(isOpen => _pauseMenuView.SetInputEnabled(!isOpen))
                .AddTo(_disposables);
        }

        // --- ポーズの開始・解除 → 時間を止める / 動かす ---
        _pauseModel.IsPaused
            .Subscribe(isPaused =>
            {
                _timeScaleView.SetPaused(isPaused);
                if (isPaused)
                    _pauseMenuView.ShowMenu(ReasonText(_pauseModel.Reason));
                else
                    _pauseMenuView.HideMenu();
            })
            .AddTo(_disposables);

        // --- 再開のカウントダウン。取り消された（ポーズのまま）ならメニューに戻す ---
        _pauseModel.IsResuming
            .Skip(1)
            .Subscribe(isResuming =>
            {
                if (isResuming)
                    _pauseMenuView.PlayCountdown(_pauseModel.ResumeCountdownSeconds);
                else if (_pauseModel.IsPaused.CurrentValue)
                    _pauseMenuView.ShowMenu(ReasonText(_pauseModel.Reason));
            })
            .AddTo(_disposables);

        // --- メニューからの要求 ---
        _pauseMenuView.OnPauseToggleRequested
            .Subscribe(_ => _pauseModel.TogglePause())
            .AddTo(_disposables);

        _pauseMenuView.OnResumeRequested
            .Subscribe(_ => _pauseModel.Resume())
            .AddTo(_disposables);

        _pauseMenuView.OnRetryRequested
            .Subscribe(_ =>
            {
                if (gameStateManager.Retry()) _pauseMenuView.Lock();
            })
            .AddTo(_disposables);

        _pauseMenuView.OnStageSelectRequested
            .Subscribe(_ =>
            {
                if (gameStateManager.ToStageSelect()) _pauseMenuView.Lock();
            })
            .AddTo(_disposables);
    }

    private void Update()
    {
        if (_pauseModel == null || !_watchesHands) return;

        // ポーズ中は時間が止まるため、実時間で数える
        _pauseModel.TickHandPresence(Time.unscaledDeltaTime, _provider.DetectedHandCount > 0);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) _pauseModel?.RequestPause(PauseReason.FocusLost);
    }

    private static string ReasonText(PauseReason reason)
    {
        return reason switch
        {
            PauseReason.HandLost => HandLostReasonText,
            PauseReason.FocusLost => FocusLostReasonText,
            _ => string.Empty
        };
    }

    private List<SpellListRow> BuildSpellRows(IReadOnlyList<SpellData> spells)
    {
        var rows = new List<SpellListRow>();
        foreach (var spell in spells)
        {
            if (spell == null) continue;

            string sequence = string.Join(" → ", spell.sequence.Select(SequenceGuideView.GetSignDisplayName));
            rows.Add(new SpellListRow(
                spell.spellName,
                ElementNames.Get(spell.element),
                _palette != null ? _palette.ElementColor(spell.element) : Color.white,
                sequence));
        }
        return rows;
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}
