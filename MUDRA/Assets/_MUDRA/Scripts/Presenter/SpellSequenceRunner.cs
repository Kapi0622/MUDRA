using MUDRA.Data;
using MUDRA.HandTracking;
using UnityEngine;
using TMPro;
using R3;

/// <summary>
/// HandTrackingServiceを駆動し、確定通知をSpellSequenceModelへ振り分ける配線役。
/// 将来的に仕様書のHandSignPresenterへ発展させる想定。
/// </summary>
public class SpellSequenceRunner : MonoBehaviour
{
    [SerializeField] private SpellEffectView _spellEffectView;
    [SerializeField] private TextMeshProUGUI _debugSignText;

    private HandTrackingService _handTrackingService;
    private SpellSequenceModel _model;
    private PlayerStateManager _playerStateManager;

    private readonly CompositeDisposable _disposables = new();
    private bool _isInitialized;

    /// <summary>
    /// BattleInitializerから呼ばれる。Model群を外部から受け取る。
    /// </summary>
    public void Initialize(
        HandTrackingService handTrackingService,
        SpellSequenceModel model,
        PlayerStateManager playerStateManager)
    {
        _handTrackingService = handTrackingService;
        _model = model;
        _playerStateManager = playerStateManager;

        _handTrackingService.OnHandSignRecognized
            .Subscribe(HandleSignConfirmed)
            .AddTo(_disposables);

        _handTrackingService.OnHandSignRecognized
            .Subscribe(sign => _debugSignText.text = sign.ToString())
            .AddTo(_disposables);

        _model.OnSpellCast
            .Subscribe(HandleSpellCast)
            .AddTo(_disposables);

        _model.OnSpellCast
            .Subscribe(_ => _playerStateManager.HandleSpellCast())
            .AddTo(_disposables);

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
        if (_isInitialized)
            _handTrackingService.Tick();
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }

    private void HandleSignConfirmed(HandSign sign)
    {
        switch (sign)
        {
            case HandSign.Release:
                _model.Release();
                break;
            case HandSign.Cancel:
                _model.Cancel();
                break;
            default:
                _model.AddSign(sign);
                break;
        }
    }

    private void HandleSpellCast(SpellData spell)
    {
        Debug.Log($"[SpellSequence] 発動成功: {spell.spellName}");
        _spellEffectView.PlayEffect();
    }

    private void HandleSequenceReset(SequenceResetReason reason)
    {
        Debug.Log($"[SpellSequence] リセット: {reason}");
    }
}