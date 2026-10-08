using UnityEngine;
using UnityEngine.UI;
using LitMotion;
using MUDRA.Data;

/// <summary>
/// 画面全体の色フラッシュ（B4）。プレイヤー側の被弾・ガード成功・暴発を色で区別して伝える。
/// 全画面のImageのalphaを一瞬上げて戻すだけ。HUDより奥（BattleCanvasの先頭）に置き、HPバーは隠さない。
/// 色の意味づけ（赤=被弾、青白=ガード、紫=暴発）はこのViewが持ち、Presenterは出来事の種類だけを伝える。
/// </summary>
public class ScreenFlashView : MonoBehaviour
{
    // --- 演出定数 ---
    private const float DamageAlpha = 0.35f;
    private const float HeavyDamageAlpha = 0.55f;
    private const float GuardAlpha = 0.35f;
    private const float MisfireAlpha = 0.4f;
    private const float FlashDuration = 0.35f;
    private const float HeavyFlashDuration = 0.5f;
    private const float BossDefeatAlpha = 0.85f;
    private const float BossDefeatFlashDuration = 0.8f;

    // --- 色定義（ガードの色はBattlePaletteData） ---
    private static readonly Color DamageColor = new Color(1f, 0.1f, 0.1f);
    private static readonly Color MisfireColor = new Color(0.6f, 0.2f, 0.9f);
    private static readonly Color BossDefeatColor = Color.white;

    [Tooltip("ガード成功の色。ガード時の被ダメージ数字と同じ色を使う")]
    [SerializeField] private BattlePaletteData _palette;
    [SerializeField] private Image _flashImage;

    private MotionHandle _flashHandle;

    private void Awake()
    {
        SetAlpha(0f);
    }

    /// <summary>敵の攻撃を受けた。大技は濃く長く光らせる。</summary>
    public void FlashDamage(bool isHeavy)
    {
        Flash(DamageColor,
            isHeavy ? HeavyDamageAlpha : DamageAlpha,
            isHeavy ? HeavyFlashDuration : FlashDuration);
    }

    /// <summary>ガード受付中に攻撃を受けた（ガード成功）。</summary>
    public void FlashGuard()
    {
        Flash(_palette.guard, GuardAlpha, FlashDuration);
    }

    /// <summary>暴発のセルフダメージを受けた。</summary>
    public void FlashMisfire()
    {
        Flash(MisfireColor, MisfireAlpha, FlashDuration);
    }

    /// <summary>
    /// ボスを倒した。ヒットストップ中は白いまま止まり、時間が戻ってからゆっくり引く。
    /// </summary>
    public void FlashBossDefeat()
    {
        Flash(BossDefeatColor, BossDefeatAlpha, BossDefeatFlashDuration);
    }

    /// <summary>
    /// 指定色で光らせ、alphaを0まで戻す。連続で呼ばれたら前の演出を止めて上書きする。
    /// </summary>
    private void Flash(Color color, float peakAlpha, float duration)
    {
        if (_flashHandle.IsActive()) _flashHandle.Cancel();

        _flashImage.color = new Color(color.r, color.g, color.b, peakAlpha);
        _flashHandle = LMotion.Create(peakAlpha, 0f, duration)
            .WithEase(Ease.OutQuad)
            .Bind(SetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        var c = _flashImage.color;
        c.a = alpha;
        _flashImage.color = c;
    }

    private void OnDestroy()
    {
        if (_flashHandle.IsActive()) _flashHandle.Cancel();
    }
}
