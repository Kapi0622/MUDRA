using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LitMotion;

/// <summary>
/// 敵1体の見た目と演出を担うView（B4）。
/// 登場・攻撃予告・攻撃・被弾・DoT被弾・Stun・撃破を命令的メソッドとして公開する。
///
/// 演出は同時に重なる（予告中に被弾する、Stun中にDoTが入る等）。
/// 各演出がSpriteRendererやTransformを直接書き換えると互いを上書きしてしまうため、
/// 演出ごとに「自分の成分」だけを持たせ、ApplyVisual() で1か所に合成して反映する。
///   色   = 基本色(Stun/仮素材の色) → 予告の明滅 → 被弾フラッシュ → 透明度
///   位置 = 登場スライド + 踏み込み + 揺れ
///   拡縮 = 基準 × ボス倍率 × 踏み込みの膨らみ × 撃破の縮小
///
/// 階層は「ルート(このコンポーネント) / Body(SpriteRenderer)」の2段。
/// 動かすのはBodyだけにし、ルートの位置は動かさない。
/// 術エフェクトの出現位置（EffectSpawnPoint）はルートの子に置くので、演出の揺れに引きずられない。
/// </summary>
public class EnemyView : MonoBehaviour
{
    // --- 登場 ---
    private const float AppearDuration = 0.6f;
    private const float AppearSlideDistance = 10f;      // 画面右外から滑り込む距離（ワールド単位）
    private const float BossScaleMultiplier = 1.6f;

    // --- 攻撃予告 ---
    // 明滅の周波数を予告の始め→終わりで上げていき、「もうすぐ来る」を伝える
    private const float ChargePulseStartHz = 1.5f;
    private const float ChargePulseEndHz = 8f;
    private const float ChargeTintMax = 0.75f;
    private const string HeavyAttackPrefix = "大技 ";
    private const float MinChargeDuration = 0.01f;      // chargeTime=0のデータでも0除算にしない

    // --- 攻撃 ---
    private const float AttackPunchScale = 1.25f;
    private const float AttackLungeDistance = 0.5f;     // 画面手前（下方向）への踏み込み
    private const float AttackDuration = 0.25f;

    // --- 被弾 ---
    private const float HitFlashDuration = 0.25f;
    private const float HitShakeDuration = 0.3f;
    private const float HitShakeAmplitude = 0.15f;
    private const float WeakHitShakeAmplitude = 0.3f;
    private const float HitShakeFrequency = 30f;
    private const float DotFlashDuration = 0.2f;
    private const float DotFlashStrength = 0.6f;

    // --- 仮素材の色 ---
    private const float PlaceholderSaturation = 0.45f;

    // --- Stun ---
    private const float StunWobbleAngle = 8f;
    private const float StunWobbleHalfPeriod = 0.35f;

    // --- 撃破 ---
    private const float DefeatBlinkDuration = 0.4f;
    private const int DefeatBlinkCount = 4;
    private const float DefeatVanishDuration = 0.4f;
    private const float DefeatEndScale = 0.5f;
    private const float DefeatBlinkAlpha = 0.2f;

    /// <summary>撃破演出の全体の長さ。Presenterが前進演出の開始を遅らせるのに使う</summary>
    public const float DefeatDuration = DefeatBlinkDuration + DefeatVanishDuration;

    // --- 色定義 ---
    private static readonly Color NormalChargeColor = new Color(1f, 0.75f, 0.2f);   // 通常攻撃の予告: 橙
    private static readonly Color HeavyChargeColor = new Color(1f, 0.15f, 0.15f);   // 大技の予告: 赤
    private static readonly Color HitFlashColor = new Color(1f, 0.25f, 0.25f);
    private static readonly Color WeakHitFlashColor = new Color(1f, 0.9f, 0.3f);
    private static readonly Color DotFlashColor = new Color(1f, 0.5f, 0.1f);
    private static readonly Color StunColor = new Color(0.6f, 0.6f, 0.8f);

    [Header("本体")]
    [SerializeField] private Transform _body;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("EnemyData.spriteが未設定の敵に使う仮スプライト")]
    [SerializeField] private Sprite _fallbackSprite;

    [Header("攻撃予告UI（BattleCanvas上）")]
    [SerializeField] private CanvasGroup _chargeGroup;
    [SerializeField] private TextMeshProUGUI _attackNameText;
    [SerializeField] private Image _chargeFill;

    [Header("Stun表示（BattleCanvas上）")]
    [SerializeField] private GameObject _stunLabel;

    // --- 合成前の各成分 ---
    private Vector3 _baseScale;
    private float _bossScale = 1f;
    private Color _baseColor = Color.white;     // 仮素材の色
    private bool _isStunned;
    private Color _chargeColor;
    private float _chargeTint;                  // 0〜1
    private Color _flashColor;
    private float _flash;                       // 0〜1
    private float _alpha = 1f;
    private float _appearOffsetX;
    private float _lungeOffsetY;
    private float _shakeOffsetX;
    private float _punchScale = 1f;
    private float _vanishScale = 1f;

    // --- 実行中のモーション ---
    private MotionHandle _appearHandle;
    private MotionHandle _chargeHandle;
    private MotionHandle _attackHandle;
    private MotionHandle _flashHandle;
    private MotionHandle _shakeHandle;
    private MotionHandle _stunHandle;
    private MotionHandle _defeatHandle;

    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 初期化を1回だけ行う。Awakeを待たずに公開メソッドからも呼ぶ。
    /// 最初の敵の登場はBattleInitializer.Awake内のStartStageから流れてくるため、
    /// オブジェクト間のAwake順によってはこのViewのAwakeより先にAppearが呼ばれる。
    /// その場合でもAppearが書き換える前の拡縮を基準として記録できるようにする。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _baseScale = _body.localScale;
        _chargeGroup.alpha = 0f;
        _stunLabel.SetActive(false);
    }

    /// <summary>
    /// 敵を登場させる。画面右外から滑り込みながらフェードインする。
    /// 前の敵の演出状態はここで全てリセットする。
    /// spriteがnullなら仮スプライトを使い、名前から決めた色で塗り分けて別の敵だと分かるようにする。
    /// 登場の長さはEnemyStateManager.FirstActionDelay（1手目までの待機）に収めること。
    /// </summary>
    public void Appear(Sprite sprite, string enemyName, bool isBoss)
    {
        EnsureInitialized();
        CancelAllMotions();
        ResetVisualState();

        _spriteRenderer.sprite = sprite != null ? sprite : _fallbackSprite;
        _baseColor = sprite != null ? Color.white : PlaceholderColor(enemyName);
        _bossScale = isBoss ? BossScaleMultiplier : 1f;

        _appearHandle = LMotion.Create(1f, 0f, AppearDuration)
            .WithEase(Ease.OutCubic)
            .Bind(t =>
            {
                _appearOffsetX = t * AppearSlideDistance;
                _alpha = 1f - t;
                ApplyVisual();
            });
    }

    /// <summary>
    /// 攻撃予告を開始する。Guardのタイミングを読ませるための演出で、予告の終わり＝ガードの合図になる。
    /// 攻撃名とchargeTimeで満ちるゲージを表示し、敵本体を明滅させる（終わりに近いほど速く）。
    /// 大技は赤で表示し、攻撃名に「大技」を付ける。
    /// </summary>
    public void PlayCharging(string attackName, float chargeTime, bool isHeavy)
    {
        EnsureInitialized();
        TryCancel(ref _chargeHandle);

        _chargeColor = isHeavy ? HeavyChargeColor : NormalChargeColor;
        _attackNameText.text = isHeavy ? HeavyAttackPrefix + attackName : attackName;
        _attackNameText.color = _chargeColor;
        _chargeFill.color = _chargeColor;
        _chargeFill.fillAmount = 0f;
        _chargeGroup.alpha = 1f;

        float duration = Mathf.Max(chargeTime, MinChargeDuration);
        _chargeHandle = LMotion.Create(0f, 1f, duration)
            .Bind(t =>
            {
                _chargeFill.fillAmount = t;

                // 周波数を線形に上げる。位相は周波数の積分（∫f dt）で求めると明滅が途切れない
                float elapsed = t * duration;
                float phase = 2f * Mathf.PI * elapsed *
                              (ChargePulseStartHz + (ChargePulseEndHz - ChargePulseStartHz) * t * 0.5f);
                _chargeTint = (Mathf.Sin(phase) * 0.5f + 0.5f) * ChargeTintMax;
                ApplyVisual();
            });
    }

    /// <summary>
    /// 攻撃予告の表示を消す。攻撃に移った時・Stunで中断された時・行動ループが止まった時に呼ぶ。
    /// </summary>
    public void HideChargeGauge()
    {
        TryCancel(ref _chargeHandle);
        _chargeGroup.alpha = 0f;
        _chargeTint = 0f;
        ApplyVisual();
    }

    /// <summary>
    /// 攻撃の瞬間の演出。画面手前に踏み込みつつ膨らみ、元に戻る。
    /// </summary>
    public void PlayAttack()
    {
        HideChargeGauge();
        TryCancel(ref _attackHandle);

        _attackHandle = LMotion.Create(0f, 1f, AttackDuration)
            .Bind(t =>
            {
                // 0→1→0 の山形。前半で踏み込み、後半で戻る
                float k = Mathf.Sin(t * Mathf.PI);
                _lungeOffsetY = -AttackLungeDistance * k;
                _punchScale = Mathf.Lerp(1f, AttackPunchScale, k);
                ApplyVisual();
            });
    }

    /// <summary>
    /// 術の被弾リアクション。色フラッシュ＋横揺れ。弱点ヒットは色を変え、揺れを大きくする。
    /// </summary>
    public void PlayHit(bool isWeakness)
    {
        Flash(isWeakness ? WeakHitFlashColor : HitFlashColor, 1f, HitFlashDuration);
        Shake(isWeakness ? WeakHitShakeAmplitude : HitShakeAmplitude);
    }

    /// <summary>
    /// DoTのtick被弾。術の被弾より控えめな橙のフラッシュのみ（毎秒来るので揺らさない）。
    /// </summary>
    public void PlayDotTick()
    {
        Flash(DotFlashColor, DotFlashStrength, DotFlashDuration);
    }

    /// <summary>
    /// Stun状態の表示を切り替える。青灰色に沈み、左右にゆっくり傾き続け、頭上に「気絶」を出す。
    /// 同じ状態で再度呼ばれても何もしない（Presenterはフェーズが変わるたびに呼ぶため）。
    /// </summary>
    public void SetStunned(bool isStunned)
    {
        EnsureInitialized();
        if (_isStunned == isStunned) return;
        _isStunned = isStunned;

        _stunLabel.SetActive(isStunned);
        TryCancel(ref _stunHandle);

        if (isStunned)
        {
            HideChargeGauge();
            _stunHandle = LMotion.Create(-StunWobbleAngle, StunWobbleAngle, StunWobbleHalfPeriod)
                .WithEase(Ease.InOutSine)
                .WithLoops(-1, LoopType.Yoyo)
                .Bind(angle => _body.localRotation = Quaternion.Euler(0f, 0f, angle));
        }
        else
        {
            _body.localRotation = Quaternion.identity;
        }

        ApplyVisual();
    }

    /// <summary>
    /// 撃破演出。点滅したあと、縮みながら消える。長さはDefeatDuration。
    /// </summary>
    public void PlayDefeat()
    {
        // 予告・Stun・被弾フラッシュの途中で倒されても、素の状態から撃破演出を始める
        CancelAllMotions();
        ResetVisualState();

        _defeatHandle = LMotion.Create(0f, 1f, DefeatDuration)
            .Bind(t =>
            {
                float elapsed = t * DefeatDuration;
                if (elapsed < DefeatBlinkDuration)
                {
                    // 前半: 一定間隔で表示/非表示を切り替える
                    int step = (int)(elapsed / DefeatBlinkDuration * DefeatBlinkCount * 2f);
                    _alpha = step % 2 == 0 ? 1f : DefeatBlinkAlpha;
                }
                else
                {
                    // 後半: 縮小しながらフェードアウト
                    float v = (elapsed - DefeatBlinkDuration) / DefeatVanishDuration;
                    _alpha = 1f - v;
                    _vanishScale = Mathf.Lerp(1f, DefeatEndScale, v);
                }
                ApplyVisual();
            });
    }

    private void Flash(Color color, float strength, float duration)
    {
        TryCancel(ref _flashHandle);
        _flashColor = color;
        _flashHandle = LMotion.Create(strength, 0f, duration)
            .WithEase(Ease.OutQuad)
            .Bind(f =>
            {
                _flash = f;
                ApplyVisual();
            });
    }

    private void Shake(float amplitude)
    {
        TryCancel(ref _shakeHandle);
        _shakeHandle = LMotion.Create(1f, 0f, HitShakeDuration)
            .Bind(decay =>
            {
                // 減衰する正弦波。経過時間は (1 - decay) × 長さ
                float elapsed = (1f - decay) * HitShakeDuration;
                _shakeOffsetX = Mathf.Sin(elapsed * HitShakeFrequency) * amplitude * decay;
                ApplyVisual();
            });
    }

    /// <summary>
    /// 各演出の成分を合成してSpriteRendererとBodyに反映する。
    /// </summary>
    private void ApplyVisual()
    {
        EnsureInitialized();
        var color = _isStunned ? _baseColor * StunColor : _baseColor;
        color = Color.Lerp(color, _chargeColor, _chargeTint);
        color = Color.Lerp(color, _flashColor, _flash);
        color.a = _alpha;
        _spriteRenderer.color = color;

        _body.localPosition = new Vector3(_appearOffsetX + _shakeOffsetX, _lungeOffsetY, 0f);
        _body.localScale = _baseScale * (_bossScale * _punchScale * _vanishScale);
    }

    private void ResetVisualState()
    {
        _isStunned = false;
        _stunLabel.SetActive(false);
        _body.localRotation = Quaternion.identity;
        _chargeGroup.alpha = 0f;
        _chargeTint = 0f;
        _flash = 0f;
        _alpha = 1f;
        _appearOffsetX = 0f;
        _lungeOffsetY = 0f;
        _shakeOffsetX = 0f;
        _punchScale = 1f;
        _vanishScale = 1f;
    }

    /// <summary>
    /// 仮素材用の色。敵名から色相を決め、同じ敵は常に同じ色になるようにする。
    /// string.GetHashCodeは実行ごとに値が変わりうるため、自前で安定したハッシュを取る。
    /// </summary>
    private static Color PlaceholderColor(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return Color.white;

        uint hash = 2166136261;     // FNV-1a
        foreach (char c in enemyName)
        {
            hash ^= c;
            hash *= 16777619;
        }

        float hue = (hash % 360) / 360f;
        return Color.HSVToRGB(hue, PlaceholderSaturation, 1f);
    }

    private void CancelAllMotions()
    {
        TryCancel(ref _appearHandle);
        TryCancel(ref _chargeHandle);
        TryCancel(ref _attackHandle);
        TryCancel(ref _flashHandle);
        TryCancel(ref _shakeHandle);
        TryCancel(ref _stunHandle);
        TryCancel(ref _defeatHandle);
    }

    /// <summary>
    /// MotionHandleは構造体のため、IsActive()で有効性を確認してからCancelする。
    /// </summary>
    private static void TryCancel(ref MotionHandle handle)
    {
        if (handle.IsActive()) handle.Cancel();
    }

    private void OnDestroy()
    {
        CancelAllMotions();
    }
}
