using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LitMotion;

/// <summary>
/// HPバーの表示を管理するView。
/// プレイヤー・ボス共通で使用し、Presenterから SetHp() を呼ぶだけで
/// 2層バー(即時反映 + 遅延追従)のアニメーションが実行される。
/// 将来的にボス専用の表示(名前/弱点アイコン等)が必要になった場合は
/// このクラスを継承するか、別Viewに分離する想定。
///
/// UI改修 ステップ5: HPが減った時にバー全体を短く揺らす（回復では揺らさない）。
/// 名前の欄（任意）を設定したバーは SetName で敵の名前を出す（ボス側のバーの上中央）。
/// </summary>
public class HpBarView : MonoBehaviour
{
    // --- アニメーション定数 ---
    // CurrentBarが目標値に到達するまでの時間
    private const float CurrentBarDuration = 0.2f;
    // CurrentBar完了後、DelayedBarが追従を開始するまでの待機時間
    private const float DelayedBarWait = 0.5f;
    // DelayedBarが目標値に到達するまでの時間
    private const float DelayedBarDuration = 0.6f;

    // --- 被弾の揺れ ---
    private const float DamageShakeDuration = 0.2f;
    private const float DamageShakeStrength = 8f;
    private const int DamageShakeFrequency = 18;

    [Header("バー本体")]
    [SerializeField] private Image _currentBar;
    [SerializeField] private Image _delayedBar;

    [Header("数値表示(任意)")]
    [SerializeField] private TextMeshProUGUI _hpText;

    [Header("名前(任意。UI改修 ステップ5)")]
    [SerializeField] private TextMeshProUGUI _nameText;

    // 実行中のモーションを保持し、連続ダメージ時に前のモーションをキャンセルする
    private MotionHandle _currentBarHandle;
    private MotionHandle _delayedBarHandle;
    private MotionHandle _shakeHandle;

    // 揺れの基準位置。揺れの最中に次の被弾が来ても、ずれた位置を基準にしないよう揺れ始めに1度だけ取る
    private Vector2 _shakeBasePosition;
    private int _lastHp = -1;

    /// <summary>
    /// HPバーの表示を更新する。
    /// currentHpとmaxHpから割合を算出し、2層バーのアニメーションを実行する。
    /// </summary>
    public void SetHp(int currentHp, int maxHp)
    {
        if (_lastHp >= 0 && currentHp < _lastHp) PlayDamageShake();
        _lastHp = currentHp;

        float targetFill = maxHp > 0 ? (float)currentHp / maxHp : 0f;

        // テキスト表示(設定されている場合のみ)
        if (_hpText != null)
        {
            _hpText.text = $"{currentHp}/{maxHp}";
        }

        AnimateCurrentBar(targetFill);
        AnimateDelayedBar(targetFill);
    }

    /// <summary>
    /// 初期化時にバーを満タンにする。アニメーションなしで即時反映。
    /// </summary>
    public void InitializeHp(int currentHp, int maxHp)
    {
        _lastHp = currentHp;
        float fill = maxHp > 0 ? (float)currentHp / maxHp : 1f;
        _currentBar.fillAmount = fill;
        _delayedBar.fillAmount = fill;

        if (_hpText != null)
        {
            _hpText.text = $"{currentHp}/{maxHp}";
        }
    }

    /// <summary>名前を出す。名前の欄を設定していないバーでは何もしない</summary>
    public void SetName(string displayName)
    {
        if (_nameText != null) _nameText.text = displayName;
    }

    private void PlayDamageShake()
    {
        var rect = (RectTransform)transform;
        if (_shakeHandle.IsActive()) _shakeHandle.Cancel();
        else _shakeBasePosition = rect.anchoredPosition;

        var basePosition = _shakeBasePosition;
        _shakeHandle = LMotion.Shake.Create(Vector2.zero, new Vector2(DamageShakeStrength, DamageShakeStrength), DamageShakeDuration)
            .WithFrequency(DamageShakeFrequency)
            .WithOnComplete(() => rect.anchoredPosition = basePosition)
            .Bind(offset => rect.anchoredPosition = basePosition + offset);
    }

    /// <summary>
    /// CurrentBar: 現在のfillAmountから目標値まで短時間で補間する。
    /// 連続ヒット時は前のモーションをキャンセルして最新の目標値に向かう。
    /// </summary>
    private void AnimateCurrentBar(float targetFill)
    {
        TryCancelMotion(ref _currentBarHandle);

        float from = _currentBar.fillAmount;

        // 変化がなければスキップ(同値への再通知を無視)
        if (Mathf.Approximately(from, targetFill)) return;

        _currentBarHandle = LMotion.Create(from, targetFill, CurrentBarDuration)
            .WithEase(Ease.OutQuad)
            .Bind(x => _currentBar.fillAmount = x);
    }

    /// <summary>
    /// DelayedBar: CurrentBarの変化完了を待ってから、ゆっくり追従する。
    /// 待機中に新しいダメージが来た場合は前のモーションをキャンセルし、
    /// 現在位置から最新の目標値に向かい直す。
    /// </summary>
    private void AnimateDelayedBar(float targetFill)
    {
        TryCancelMotion(ref _delayedBarHandle);

        float from = _delayedBar.fillAmount;

        if (Mathf.Approximately(from, targetFill)) return;

        _delayedBarHandle = LMotion.Create(from, targetFill, DelayedBarDuration)
            .WithEase(Ease.InOutSine)
            .WithDelay(DelayedBarWait)
            .Bind(x => _delayedBar.fillAmount = x);
    }

    /// <summary>
    /// 実行中のモーションがあれば安全にキャンセルする。
    /// MotionHandleは構造体のため、IsActive()で有効性を確認してからComplete/Cancelする。
    /// </summary>
    private static void TryCancelMotion(ref MotionHandle handle)
    {
        if (handle.IsActive())
        {
            handle.Cancel();
        }
    }

    /// <summary>
    /// シーンのアンロード（チュートリアル完了・リトライ等）で、アニメーション途中のまま破棄された時に止める。
    /// 止めないとLitMotionが破棄済みのImageに書き込み続け、MissingReferenceExceptionになる。
    /// 遅延追従のバーは待機を含めて1秒以上動くので、決着の直後に遷移すると特に起きやすい
    /// </summary>
    private void OnDestroy()
    {
        TryCancelMotion(ref _currentBarHandle);
        TryCancelMotion(ref _delayedBarHandle);
        TryCancelMotion(ref _shakeHandle);
    }
}