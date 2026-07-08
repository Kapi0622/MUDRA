using MUDRA.Data;
using MUDRA.HandTracking;
using UnityEngine;
using TMPro;
using R3;

/// <summary>
/// HandTrackingServiceを駆動し、確定通知をSpellSequenceModelへ振り分ける配線役
/// 将来的に仕様書のHandSignPresenterへ発展させる想定
/// </summary>
public class SpellSequenceRunner : MonoBehaviour
{
    [SerializeField] private MediaPipeHandLandmarkProvider _provider;
    [SerializeField] private SpellEffectView _spellEffectView;
    [SerializeField] private SpellData[]  _allSpells;
    [SerializeField] private TextMeshProUGUI _debugSignText;

    private HandTrackingService _handTrackingService;
    private SpellSequenceModel _model;
    private PlayerStateManager _playerStateManager;
    
    // 購読解除をまとめて管理する入れ物
    private readonly CompositeDisposable _disposables = new();

    private void Awake()
    {
        _handTrackingService = new HandTrackingService(_provider);
        _model = new SpellSequenceModel(_allSpells);
        _playerStateManager = new PlayerStateManager();

        _handTrackingService.OnHandSignRecognized
            .Subscribe(HandleSignConfirmed)
            .AddTo(_disposables);
        
        _handTrackingService.OnHandSignRecognized
            .Subscribe(sign => _debugSignText.text = sign.ToString())
            .AddTo(_disposables);

        _model.OnSpellCast
            .Subscribe(HandleSpellCast)
            .AddTo(_disposables);

        // 「エフェクト再生」と「状態遷移」は別の関心事なので、あえて分けて購読している
        _model.OnSpellCast
            .Subscribe(_ => _playerStateManager.HandleSpellCast())
            .AddTo(_disposables);

        _model.OnSequenceReset
            .Subscribe(HandleSequenceReset)
            .AddTo(_disposables);

        // 「エフェクト再生」と「状態遷移」は別の関心事なので、あえて分けて購読している
        _model.OnSequenceReset
            .Subscribe(_ => _playerStateManager.HandleSequenceReset())
            .AddTo(_disposables);

        _model.OnChantStarted
            .Subscribe(_ => _playerStateManager.HandleChantStarted())
            .AddTo(_disposables);

        _playerStateManager.CurrentPhase
            .Subscribe(phase => Debug.Log($"[PlayerState] {phase}"))
            .AddTo(_disposables);
    }

    private void Update()
    {
        // 駆動役として毎フレームTickを呼ぶ
        _handTrackingService.Tick();
    }
    
    private void OnDestroy()
    {
        _disposables.Dispose();     // 全Subscribeをまとめて解除
        _handTrackingService.Dispose();
        _model.Dispose();
        _playerStateManager.Dispose();
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
        // TODO: 必要ならリセット時の演出をここに繋ぐ
    }
}