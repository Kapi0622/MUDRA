using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LitMotion;
using MUDRA.Data;

/// <summary>
/// ボス戦専用の、画面全体にかかる演出（B4）。
///   登場: 暗転 ＋「強敵出現」の帯（敵本体の出現はEnemyViewが担う）
///   大技: 画面の縁を赤黒く沈めるビネット ＋ 中央に大きく「大技：○○」
///   撃破: ヒットストップ（一瞬時間を止める）
/// 秒数はPresentationTimingDataの時間表に揃えている。
/// </summary>
public class BossEncounterView : MonoBehaviour
{
    // --- 登場 ---
    private const float DimAlpha = 0.6f;
    private const float BandSlideDistance = BattleUiConstants.OffscreenSlideDistance;

    // --- 大技予告 ---
    private const string HeavyPrefix = BattleUiConstants.HeavyAttackLabel + "：";
    private const float VignetteMinAlpha = 0.45f;
    private const float VignetteMaxAlpha = 0.9f;
    private const float VignettePulseHalfPeriod = 0.25f;
    private const float WarningFadeDuration = 0.15f;

    [Tooltip("登場・撃破の時間表。敵本体・カメラ・ボス名のViewと同じものを使う")]
    [SerializeField] private PresentationTimingData _timing;

    [Header("登場")]
    [Tooltip("暗転。ワールド側（背景の手前・敵の奥）に置く。HUDのCanvasに置くとボス本体まで暗くなり、影からの登場が見えにくいため")]
    [SerializeField] private SpriteRenderer _dimOverlay;
    [SerializeField] private RectTransform _warningBand;

    [Header("大技予告")]
    [SerializeField] private Image _vignette;
    [SerializeField] private CanvasGroup _heavyTextGroup;
    [SerializeField] private TextMeshProUGUI _heavyText;

    private MotionHandle _dimHandle;
    private MotionHandle _bandHandle;
    private MotionHandle _vignetteHandle;
    private MotionHandle _heavyTextHandle;
    private bool _isHitStopping;
    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 初期化を1回だけ行う。Awakeを待たずに公開メソッドからも呼ぶ。
    /// 最初のセクションがボスだと、PlayEncounterはBattleInitializer.Awake内のStartStageから流れてくるため、
    /// オブジェクト間のAwake順によってはこのViewのAwakeより先に呼ばれる。
    /// その後にAwakeで初期化すると、表示し始めた「強敵出現」の帯を消してしまう。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        SetDimAlpha(0f);
        SetImageAlpha(_vignette, 0f);
        _heavyTextGroup.alpha = 0f;
        _warningBand.gameObject.SetActive(false);
    }

    /// <summary>
    /// 登場シーケンスの画面側。暗転しながら「強敵出現」の帯を通し、咆哮の後に明ける。
    /// </summary>
    public void PlayEncounter()
    {
        EnsureInitialized();
        CancelEncounterMotions();

        // 暗転: フェードイン → 咆哮まで維持 → フェードアウト
        _dimHandle = LMotion.Create(0f, DimAlpha, _timing.dimFadeInDuration)
            .WithOnComplete(() =>
            {
                _dimHandle = LMotion.Create(DimAlpha, 0f, _timing.dimFadeOutDuration)
                    .WithDelay(_timing.roar - _timing.dimFadeInDuration)
                    .Bind(SetDimAlpha);
            })
            .Bind(SetDimAlpha);

        // 帯: 左から入って中央で止まり、右へ抜ける
        _warningBand.gameObject.SetActive(true);
        _bandHandle = LMotion.Create(-BandSlideDistance, 0f, _timing.bandInDuration)
            .WithEase(Ease.OutCubic)
            .WithOnComplete(() =>
            {
                _bandHandle = LMotion.Create(0f, BandSlideDistance, _timing.bandOutDuration)
                    .WithEase(Ease.InCubic)
                    .WithDelay(_timing.bandOutStart - _timing.bandInDuration)
                    .WithOnComplete(() => _warningBand.gameObject.SetActive(false))
                    .Bind(SetBandX);
            })
            .Bind(SetBandX);
    }

    /// <summary>
    /// ボスの大技の予告。通常の予告（敵の頭上のゲージ）に重ねて、画面全体で危険を伝える。
    /// 予告が終わる（攻撃・Stun・撃破）まで出し続けるので、必ずHideHeavyWarningと対で呼ぶ。
    /// </summary>
    public void ShowHeavyWarning(string attackName)
    {
        EnsureInitialized();
        CancelWarningMotions();

        _heavyText.text = HeavyPrefix + attackName;
        _heavyTextHandle = LMotion.Create(_heavyTextGroup.alpha, 1f, WarningFadeDuration)
            .Bind(a => _heavyTextGroup.alpha = a);

        _vignetteHandle = LMotion.Create(VignetteMinAlpha, VignetteMaxAlpha, VignettePulseHalfPeriod)
            .WithEase(Ease.InOutSine)
            .WithLoops(-1, LoopType.Yoyo)
            .Bind(a => SetImageAlpha(_vignette, a));
    }

    /// <summary>大技の予告を消す。表示していなければ何もしない。</summary>
    public void HideHeavyWarning()
    {
        if (_heavyTextGroup.alpha <= 0f && _vignette.color.a <= 0f) return;

        CancelWarningMotions();
        _heavyTextHandle = LMotion.Create(_heavyTextGroup.alpha, 0f, WarningFadeDuration)
            .Bind(a => _heavyTextGroup.alpha = a);
        _vignetteHandle = LMotion.Create(_vignette.color.a, 0f, WarningFadeDuration)
            .Bind(a => SetImageAlpha(_vignette, a));
    }

    /// <summary>
    /// ヒットストップ。timeScaleを0にして、実時間で一定時間後に1へ戻す。
    /// 止めている間もこの待機は進むよう、timeScaleを無視して待つ。
    /// timeScaleはゲーム全体に効くため、B5でポーズをtimeScaleで作る場合はここと整理が要る。
    /// </summary>
    public void PlayHitStop()
    {
        if (_isHitStopping) return;
        HitStopAsync().Forget();
    }

    private async UniTaskVoid HitStopAsync()
    {
        _isHitStopping = true;
        Time.timeScale = 0f;
        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(_timing.hitStopRealtime),
                ignoreTimeScale: true,
                cancellationToken: destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 破棄された場合もfinallyで必ず元に戻す
        }
        finally
        {
            Time.timeScale = 1f;
            _isHitStopping = false;
        }
    }

    private void SetBandX(float x)
    {
        _warningBand.anchoredPosition = new Vector2(x, _warningBand.anchoredPosition.y);
    }

    private void SetDimAlpha(float alpha)
    {
        var c = _dimOverlay.color;
        c.a = alpha;
        _dimOverlay.color = c;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        var c = image.color;
        c.a = alpha;
        image.color = c;
    }

    private void CancelEncounterMotions()
    {
        if (_dimHandle.IsActive()) _dimHandle.Cancel();
        if (_bandHandle.IsActive()) _bandHandle.Cancel();
    }

    private void CancelWarningMotions()
    {
        if (_vignetteHandle.IsActive()) _vignetteHandle.Cancel();
        if (_heavyTextHandle.IsActive()) _heavyTextHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelEncounterMotions();
        CancelWarningMotions();
        // ヒットストップ中に破棄されてもtimeScaleを止めたままにしない
        if (_isHitStopping) Time.timeScale = 1f;
    }
}
