using UnityEngine;
using LitMotion;
using MUDRA.Data;

/// <summary>
/// 継続中のStatusEffectのアイコン表示（B4）。
/// 敵に付く効果（DoT・Stun）は敵HPバーの横、プレイヤーに付く効果（HoT）はプレイヤーHPバーの横に置く。
/// アイコンはシーン上に1種1つずつ用意してあり、付与・解除に合わせて出し入れするだけ。
/// StatusEffectManagerは同種の効果を重複させないので、1種につき1つで足りる。
/// 仮素材のうちは色付きの四角に1文字（蝕＝DoT・封＝Stun・癒＝HoT）を載せている。
/// 文字と色は「効果の種類」で決め、付けた術の属性には寄せない（火以外のDoT、雷以外のStunでも違和感が出ないように）。
/// </summary>
public class StatusIconView : MonoBehaviour
{
    // --- 演出定数 ---
    private const float PopScaleFrom = 1.6f;
    private const float PopDuration = 0.2f;

    [Header("敵側")]
    [SerializeField] private RectTransform _dotIcon;
    [SerializeField] private RectTransform _stunIcon;

    [Header("プレイヤー側")]
    [SerializeField] private RectTransform _hotIcon;

    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 初期化を1回だけ行う。後からAwakeが走って、表示中のアイコンを消さないようにする。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _dotIcon.gameObject.SetActive(false);
        _stunIcon.gameObject.SetActive(false);
        _hotIcon.gameObject.SetActive(false);
    }

    /// <summary>
    /// 効果のアイコンを出し入れする。アイコンを持たない種類（Slow等）は無視する。
    /// </summary>
    public void SetEffectActive(StatusEffectType type, bool isActive)
    {
        EnsureInitialized();

        var icon = IconFor(type);
        if (icon == null) return;
        if (icon.gameObject.activeSelf == isActive) return;

        icon.gameObject.SetActive(isActive);
        if (!isActive) return;

        LMotion.Create(PopScaleFrom, 1f, PopDuration)
            .WithEase(Ease.OutBack)
            .Bind(s => icon.localScale = new Vector3(s, s, 1f))
            .AddTo(icon.gameObject);
    }

    private RectTransform IconFor(StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.DamageOverTime => _dotIcon,
            StatusEffectType.Stun => _stunIcon,
            StatusEffectType.HealOverTime => _hotIcon,
            _ => null,
        };
    }
}
