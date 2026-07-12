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

    [Header("バー本体")]
    [SerializeField] private Image _currentBar;
    [SerializeField] private Image _delayedBar;

    [Header("数値表示(任意)")]
    [SerializeField] private TextMeshProUGUI _hpText;

    // 実行中のモーションを保持し、連続ダメージ時に前のモーションをキャンセルする
    private MotionHandle _currentBarHandle;
    private MotionHandle _delayedBarHandle;

    /// <summary>
    /// HPバーの表示を更新する。
    /// currentHpとmaxHpから割合を算出し、2層バーのアニメーションを実行する。
    /// </summary>
    public void SetHp(int currentHp, int maxHp)
    {
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
        float fill = maxHp > 0 ? (float)currentHp / maxHp : 1f;
        _currentBar.fillAmount = fill;
        _delayedBar.fillAmount = fill;

        if (_hpText != null)
        {
            _hpText.text = $"{currentHp}/{maxHp}";
        }
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
}