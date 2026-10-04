using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using TMPro;
using LitMotion;

/// <summary>
/// 浮き上がって消えるダメージ数字・回復量のView（B4）。
/// 敵側の数字は敵のワールド座標を画面座標に直してHUD上に出す。
/// プレイヤー側の数字（被弾・回復）はHUD上の固定位置（プレイヤーHPバーの近く）に出す。
///
/// 数字は頻繁に出入りするので、シーン上のテンプレートを複製してObjectPoolで使い回す。
/// Modelの型（DamageResult等）は知らず、Presenterが分解した値だけを受け取る。
/// </summary>
public class DamageNumberView : MonoBehaviour
{
    // --- 動き ---
    private const float RiseDistance = 90f;
    private const float RiseDuration = 0.8f;
    private const float PopDuration = 0.15f;
    private const float PopScaleFrom = 1.6f;
    private const float FadeOutDuration = 0.3f;
    private const float MultiHitInterval = 0.1f;    // MultiHitの1ヒットごとのずらし
    private const float RandomOffsetX = 70f;        // 連続で出た数字が重ならないように横へ散らす
    private const float EnemyOffsetY = 120f;        // 敵の中心より少し上に出す

    // --- 大きさ ---
    private const float NormalSize = 64f;
    private const float WeakSize = 88f;
    private const float SmallSize = 44f;
    private const float LabelSize = 40f;
    private const float LabelOffsetY = 70f;

    // --- 色定義 ---
    private static readonly Color HitColor = Color.white;
    private static readonly Color WeakColor = new Color(1f, 0.85f, 0.2f);
    private static readonly Color DotColor = new Color(1f, 0.55f, 0.15f);
    private static readonly Color HealColor = new Color(0.4f, 1f, 0.5f);
    private static readonly Color PlayerDamageColor = new Color(1f, 0.3f, 0.3f);
    private static readonly Color GuardedDamageColor = new Color(0.6f, 0.85f, 1f);
    private static readonly Color LabelColor = new Color(1f, 0.95f, 0.7f);

    private const string WeakLabel = "弱点！";
    private const string SpeedLabel = "迅速！";

    [Tooltip("複製元のテキスト。非アクティブにしておく")]
    [SerializeField] private TextMeshProUGUI _template;
    [Tooltip("数字を並べる親。画面全体に広げたRectTransform")]
    [SerializeField] private RectTransform _container;
    [SerializeField] private Camera _camera;
    [Tooltip("敵側の数字の基準位置（敵のルート）")]
    [SerializeField] private Transform _enemyAnchor;
    [Tooltip("プレイヤー側の数字の基準位置（HUD上）")]
    [SerializeField] private RectTransform _playerAnchor;

    private ObjectPool<TextMeshProUGUI> _pool;

    private void Awake()
    {
        _template.gameObject.SetActive(false);
        _pool = new ObjectPool<TextMeshProUGUI>(
            createFunc: () => Instantiate(_template, _container),
            actionOnGet: t => t.gameObject.SetActive(true),
            actionOnRelease: t => t.gameObject.SetActive(false),
            actionOnDestroy: t => Destroy(t.gameObject));
    }

    /// <summary>
    /// 術のヒット。hitCountが2以上なら1ヒットずつ時間差で出す（MultiHitの連撃に見せる）。
    /// 弱点・速度ボーナスは最初の数字の上にラベルを添える。
    /// </summary>
    public void ShowSpellHit(int perHitDamage, int hitCount, bool isWeakness, bool hasSpeedBonus)
    {
        var origin = EnemyPosition();
        float size = isWeakness ? WeakSize : NormalSize;
        var color = isWeakness ? WeakColor : HitColor;

        int count = Mathf.Max(1, hitCount);
        for (int i = 0; i < count; i++)
        {
            Spawn(perHitDamage.ToString(), origin + RandomOffset(), size, color, i * MultiHitInterval);
        }

        var labelPosition = origin + new Vector2(0f, LabelOffsetY);
        if (isWeakness && hasSpeedBonus)
            Spawn(WeakLabel + " " + SpeedLabel, labelPosition, LabelSize, LabelColor, 0f);
        else if (isWeakness)
            Spawn(WeakLabel, labelPosition, LabelSize, LabelColor, 0f);
        else if (hasSpeedBonus)
            Spawn(SpeedLabel, labelPosition, LabelSize, LabelColor, 0f);
    }

    /// <summary>DoTのtickダメージ。毎秒出るので小さく控えめにする。</summary>
    public void ShowDotTick(int damage)
    {
        Spawn(damage.ToString(), EnemyPosition() + RandomOffset(), SmallSize, DotColor, 0f);
    }

    /// <summary>回復量。プレイヤー側に緑で出す。</summary>
    public void ShowHeal(int amount)
    {
        Spawn("+" + amount, PlayerPosition() + RandomOffset(), SmallSize, HealColor, 0f);
    }

    /// <summary>プレイヤーの被弾。ガードで軽減した時は青く小さく出す。</summary>
    public void ShowPlayerDamage(int damage, bool wasGuarded)
    {
        Spawn(damage.ToString(), PlayerPosition() + RandomOffset(),
            wasGuarded ? SmallSize : NormalSize,
            wasGuarded ? GuardedDamageColor : PlayerDamageColor, 0f);
    }

    /// <summary>
    /// 数字を1つ出す。delayの間は透明で待ち、ポップ → 上昇 → フェードアウト → プールに戻す。
    /// </summary>
    private void Spawn(string text, Vector2 position, float fontSize, Color color, float delay)
    {
        var label = _pool.Get();
        label.text = text;
        label.fontSize = fontSize;
        label.color = new Color(color.r, color.g, color.b, 0f);

        var rect = label.rectTransform;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();

        // シーン破棄で数字が途中で消えた時にモーションを止めるため、このViewの破棄トークンに紐づける。
        // 数字は使い回すので、完了したら登録を解除する（解除しないと登録が溜まり続ける）
        var registration = default(CancellationTokenRegistration);
        var handle = LMotion.Create(0f, 1f, RiseDuration)
            .WithDelay(delay)
            .WithOnComplete(() =>
            {
                registration.Dispose();
                _pool.Release(label);
            })
            .Bind(t =>
            {
                float elapsed = t * RiseDuration;
                float pop = Mathf.Clamp01(elapsed / PopDuration);
                float scale = Mathf.Lerp(PopScaleFrom, 1f, 1f - (1f - pop) * (1f - pop));
                rect.localScale = new Vector3(scale, scale, 1f);

                float rise = 1f - (1f - t) * (1f - t) * (1f - t);   // OutCubic
                rect.anchoredPosition = position + new Vector2(0f, RiseDistance * rise);

                float fadeStart = RiseDuration - FadeOutDuration;
                float alpha = elapsed < fadeStart ? 1f : 1f - (elapsed - fadeStart) / FadeOutDuration;
                label.color = new Color(color.r, color.g, color.b, alpha);
            });
        registration = destroyCancellationToken.Register(() =>
        {
            if (handle.IsActive()) handle.Cancel();
        });
    }

    /// <summary>敵のワールド座標を、数字の親（Overlay Canvas上）のローカル座標に直す。</summary>
    private Vector2 EnemyPosition()
    {
        var screen = _camera.WorldToScreenPoint(_enemyAnchor.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screen, null, out var local);
        return local + new Vector2(0f, EnemyOffsetY);
    }

    /// <summary>プレイヤー側の基準位置を、数字の親のローカル座標に直す。</summary>
    private Vector2 PlayerPosition()
    {
        var screen = RectTransformUtility.WorldToScreenPoint(null, _playerAnchor.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screen, null, out var local);
        return local;
    }

    private static Vector2 RandomOffset()
    {
        return new Vector2(Random.Range(-RandomOffsetX, RandomOffsetX), 0f);
    }
}
