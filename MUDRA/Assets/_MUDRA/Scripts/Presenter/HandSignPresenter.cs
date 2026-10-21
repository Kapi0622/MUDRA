using JetBrains.Annotations;
using MUDRA.Data;
using MUDRA.HandTracking;
using UnityEngine;
using TMPro;
using R3;

/// <summary>
/// HandTrackingServiceを駆動し、確定通知をSpellSequenceModelへ振り分ける配線役。
/// 将来的に仕様書のHandSignPresenterへ発展させる想定。
/// </summary>
public class HandSignPresenter : MonoBehaviour
{
    [SerializeField] private SpellEffectView _spellEffectView;
    [SerializeField] [CanBeNull] private TextMeshProUGUI _debugSignText;
    [SerializeField] private SequenceGuideView _sequenceGuideView;
    [SerializeField] private SpellTelopView _spellTelopView;
    [SerializeField] private HandSignStatusView _handSignStatusView;

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField, Tooltip("アタッチするとカメラをバイパスしてキーボード入力でテスト可能")]
        private DebugKeyboardInput _debugKeyboardInput;

        [SerializeField, Tooltip("オンにすると手印判定の経過（候補切替・確定・合の保持）をログに出す。閾値の調整用")]
        private bool _logHandSignJudgement;
    #endif


    private HandTrackingService _handTrackingService;
    private SpellSequenceModel _model;
    private PlayerStateManager _playerStateManager;
    private GuardWindowManager _guardWindowManager;
    private PauseModel _pauseModel;

    private readonly CompositeDisposable _disposables = new();
    private bool _isInitialized;

    /// <summary>
    /// キーボードで印を入力するデバッグモードか。カメラを使わないので、手が映らない時の自動ポーズを行わない（B5）
    /// </summary>
    public bool IsKeyboardMode
    {
        get
        {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
            return _debugKeyboardInput != null;
        #else
            return false;
        #endif
        }
    }

    /// <summary>
    /// BattleInitializerから呼ばれる。Model群を外部から受け取る。
    /// </summary>
    public void Initialize(
        HandTrackingService handTrackingService,
        SpellSequenceModel model,
        PlayerStateManager playerStateManager,
        GuardWindowManager guardWindowManager,
        PauseModel pauseModel)
    {
        _handTrackingService = handTrackingService;
        _model = model;
        _playerStateManager = playerStateManager;
        _guardWindowManager = guardWindowManager;
        _pauseModel = pauseModel;

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_debugKeyboardInput != null)
        {
            // デバッグモード: キーボード入力 → HandleSignConfirmed に直結
            // HandTrackingService.Tick() は駆動しない（Update側で分岐）
            _debugKeyboardInput.OnHandSignInput
                .Subscribe(HandleSignConfirmed)
                .AddTo(_disposables);

            if (_debugSignText != null)
            {
                _debugKeyboardInput.OnHandSignInput
                    .Subscribe(sign => _debugSignText.text = sign.ToString())
                    .AddTo(_disposables);
            }

            // キーボードには確定前の段階がないので、確定演出だけ出す（カメラなしで見た目を確認するため）
            _debugKeyboardInput.OnHandSignInput
                .Subscribe(ShowSignConfirmed)
                .AddTo(_disposables);

            Debug.Log("[DebugMode] キーボード手印シミュレーション有効");
        }
        else
    #endif
        {
            // 通常モード: HandTrackingService 経由
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
            _handTrackingService.IsDebugLogEnabled = _logHandSignJudgement;
    #endif
            _handTrackingService.OnHandSignRecognized
                .Subscribe(HandleSignConfirmed)
                .AddTo(_disposables);

            if (_debugSignText != null)
            {
                _handTrackingService.OnHandSignRecognized
                    .Subscribe(sign => _debugSignText.text = sign.ToString())
                    .AddTo(_disposables);
            }

            // --- 判定中の印 → 印名とゲージ ---
            _handTrackingService.Candidate
                .Subscribe(ShowSignCandidate)
                .AddTo(_disposables);

            _handTrackingService.OnHandSignRecognized
                .Subscribe(ShowSignConfirmed)
                .AddTo(_disposables);
        }

        // --- 印確定 → ガイド更新 + 確定エフェクト ---
        _model.OnSignAdded
            .Subscribe(_ =>
            {
                _sequenceGuideView.UpdateGuide(_model.MatchCandidates, _model.InputCount);
                _sequenceGuideView.PlayConfirmEffect();
            })
            .AddTo(_disposables);

        // --- 術発動 → エフェクト再生 + ガイドクリア ---
        _model.OnSpellCast
            .Subscribe(HandleSpellCast)
            .AddTo(_disposables);

        _model.OnSpellCast
            .Where(result => result.IsSuccess)
            .Subscribe(_ => _playerStateManager.HandleSpellCast())
            .AddTo(_disposables);
        
        _model.OnSpellCast
            .Where(result => !result.IsSuccess)
            .Subscribe(_ => _playerStateManager.HandleSequenceReset())
            .AddTo(_disposables);

        // --- シーケンスリセット → ガイドクリア ---
        _model.OnSequenceReset
            .Subscribe(HandleSequenceReset)
            .AddTo(_disposables);

        _model.OnSequenceReset
            .Subscribe(_ => _playerStateManager.HandleSequenceReset())
            .AddTo(_disposables);

        _model.OnChantStarted
            .Subscribe(_ => _playerStateManager.HandleChantStarted())
            .AddTo(_disposables);

        _playerStateManager.CurrentPhase
            .Subscribe(phase => Debug.Log($"[PlayerState] {phase}"))
            .AddTo(_disposables);

        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized) return;

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_debugKeyboardInput != null) return; // キーボードモードではTick不要
    #endif

        _handTrackingService.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }

    private void HandleSignConfirmed(HandSign sign)
    {
        // 開始前・ポーズ中・決着後の印は捨てる（B5）。
        // キーボード入力はtimeScale = 0でも届くため、ここで止めないとポーズ中に術が出てしまう
        if (!_pauseModel.AcceptsBattleInput) return;

        switch (sign)
        {
            case HandSign.Release:
                _model.Release();
                break;
            case HandSign.Cancel:
                _model.Cancel();
                break;
            case HandSign.Guard:
                _guardWindowManager.Activate();
                Debug.Log("[Guard] ガード");
                break;
            default:
                _model.AddSign(sign);
                break;
        }
    }

    private void ShowSignCandidate(HandSignCandidate candidate)
    {
        // 進み具合1は確定済みの印を保持している状態。確定演出は ShowSignConfirmed が出すので、ここでは隠す
        if (candidate.Sign is not { } sign || candidate.Progress >= 1f || !_pauseModel.AcceptsBattleInput)
        {
            _handSignStatusView.Hide();
            return;
        }

        _handSignStatusView.ShowCandidate(SequenceGuideView.GetSignDisplayName(sign), candidate.Progress);
    }

    private void ShowSignConfirmed(HandSign sign)
    {
        // 受け付けない間の印は HandleSignConfirmed で捨てるので、確定したようにも見せない
        if (!_pauseModel.AcceptsBattleInput)
        {
            _handSignStatusView.Hide();
            return;
        }

        _handSignStatusView.PlayConfirm(SequenceGuideView.GetSignDisplayName(sign));
    }

    private void HandleSpellCast(SpellCastResult result)
    {
        _sequenceGuideView.Clear();

        if (result.IsSuccess)
        {
            Debug.Log($"[SpellSequence] 発動成功: {result.Spell.spellName} (SpeedBonus: {result.SpeedBonus})");
            var spell = result.Spell;
            _spellEffectView.PlaySpellEffect(spell.effectPrefab, spell.effectOnCaster);
            _spellTelopView.ShowCutIn(spell.spellName, spell.element, spell.cutInSprite);
        }
        else
        {
            Debug.Log("[SpellSequence] 暴発");
            _spellTelopView.ShowMisfire();
        }
    }

    private void HandleSequenceReset(SequenceResetReason reason)
    {
        Debug.Log($"[SpellSequence] リセット: {reason}");
    }
}