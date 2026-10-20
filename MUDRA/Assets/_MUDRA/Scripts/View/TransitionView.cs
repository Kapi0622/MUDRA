using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;

/// <summary>
/// 画面遷移の覆い（B5）。App側の最前面に置き、シーンの読み込み中も画面を覆い続ける。
///
/// 墨を左から右へ払うようなワイプ。黒い面（Fill）の端に、擦れた縁のスプライト（Edge）を付けて動かす。
/// 覆う時は左から右へ黒が伸び、晴れる時は同じ向きのまま黒が右へ抜けていく（払いの向きを揃える）。
/// 縁の素材は仮で、InkEdgeのスプライトを差し替えれば見た目だけ変えられる。
///
/// ポーズ中（timeScale = 0）にリタイアした時も動くよう、モーションは実時間で進める。
/// 覆っている間はクリックを遮る。
/// </summary>
public class TransitionView : MonoBehaviour
{
    /// <summary>
    /// 縁が画面外から入り、画面外へ抜けきるための行き過ぎ（画面幅に対する割合）。
    /// 縁の幅より大きくしておかないと、晴れきった後に縁だけが画面の端に残る
    /// </summary>
    private const float EdgeOvershoot = 0.2f;

    [SerializeField] private CanvasGroup _canvasGroup;
    [Tooltip("黒い面。アンカーの横幅を動かして伸び縮みさせる")]
    [SerializeField] private RectTransform _fill;
    [Tooltip("墨の擦れた縁。Fillの端に付いて動く。左端が不透明な素材を想定")]
    [SerializeField] private RectTransform _edge;

    private MotionHandle _handle;

    /// <summary>画面を覆う（左から黒が伸びる）</summary>
    public UniTask CoverAsync(float duration, CancellationToken token)
    {
        _canvasGroup.blocksRaycasts = true;
        return Animate(-EdgeOvershoot, 1f, duration, SetCoverProgress, token);
    }

    /// <summary>覆いを晴らす（黒が右へ抜けていく）</summary>
    public async UniTask RevealAsync(float duration, CancellationToken token)
    {
        await Animate(0f, 1f + EdgeOvershoot, duration, SetRevealProgress, token);
        _canvasGroup.blocksRaycasts = false;
    }

    /// <summary>演出なしで即座に覆う・晴らす。起動直後の初期状態に使う</summary>
    public void SetCovered(bool isCovered)
    {
        CancelMotion();
        if (isCovered) SetCoverProgress(1f);
        else SetRevealProgress(1f + EdgeOvershoot);
        _canvasGroup.blocksRaycasts = isCovered;
    }

    /// <summary>覆う途中。黒は左端からpまで。縁は黒の右端に付いて右へ伸びる</summary>
    private void SetCoverProgress(float p)
    {
        _fill.anchorMin = new Vector2(0f, 0f);
        _fill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
        PlaceEdge(p, 1f);
    }

    /// <summary>晴れる途中。黒はpから右端まで。縁は黒の左端に付いて左へ伸びる（左右反転）</summary>
    private void SetRevealProgress(float p)
    {
        _fill.anchorMin = new Vector2(Mathf.Clamp01(p), 0f);
        _fill.anchorMax = new Vector2(1f, 1f);
        PlaceEdge(p, -1f);
    }

    private void PlaceEdge(float x, float direction)
    {
        _edge.anchorMin = new Vector2(x, 0f);
        _edge.anchorMax = new Vector2(x, 1f);
        _edge.localScale = new Vector3(direction, 1f, 1f);
    }

    private UniTask Animate(float from, float to, float duration, System.Action<float> apply, CancellationToken token)
    {
        CancelMotion();
        _handle = LMotion.Create(from, to, duration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.InOutCubic)
            .Bind(apply);
        return _handle.ToUniTask(token);
    }

    private void CancelMotion()
    {
        if (_handle.IsActive()) _handle.Cancel();
    }

    private void OnDestroy()
    {
        CancelMotion();
    }
}
