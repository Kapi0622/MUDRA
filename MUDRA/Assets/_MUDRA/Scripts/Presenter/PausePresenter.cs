using System.Collections.Generic;
using System.Linq;
using MUDRA.Data;
using R3;
using UnityEngine;

/// <summary>
/// ポーズを配線する（B5）。InGameに置き、BattleInitializerから注入を受ける。
///
/// PauseModelの状態をTimeScaleView（時間を止める）とPauseMenuView（メニュー・カウントダウン）に渡し、
/// メニューからの要求をPauseModel・GameStateManagerのメソッドに渡す。
/// 自動ポーズの材料（フォーカスの喪失・手を見失ったか）もここからModelへ渡す。
/// 手を見失ったかの判定はApp側のHandPresenceModelが持ち（B6）、ここは立ち上がりでポーズを要求するだけ。
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

    private readonly CompositeDisposable _disposables = new();

    /// <summary>BattleInitializerから呼ばれる</summary>
    /// <param name="handPresence">手の映り具合（App側で共有）</param>
    /// <param name="watchesHands">手が映らない時に自動でポーズするか。キーボードモードではfalse</param>
    public void Initialize(
        PauseModel pauseModel,
        GameStateManager gameStateManager,
        HandPresenceModel handPresence,
        bool watchesHands,
        IReadOnlyList<SpellData> spells,
        SettingsPresenter settings)
    {
        _pauseModel = pauseModel;

        if (watchesHands && handPresence != null)
        {
            // --- 手を見失った → 自動ポーズ。バトルの開始・再開のたびに数え直させる ---
            handPresence.IsHandLost
                .Where(isLost => isLost)
                .Subscribe(_ => _pauseModel.RequestPause(PauseReason.HandLost))
                .AddTo(_disposables);

            _pauseModel.OnHandWatchRestarted
                .Subscribe(_ => handPresence.RestartLostTimer())
                .AddTo(_disposables);
        }

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
                    ShowMenu();
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
                    ShowMenu();
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

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) _pauseModel?.RequestPause(PauseReason.FocusLost);
    }

    private void ShowMenu()
    {
        var reason = _pauseModel.Reason;
        _pauseMenuView.ShowMenu(ReasonText(reason), reason == PauseReason.HandLost);
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
