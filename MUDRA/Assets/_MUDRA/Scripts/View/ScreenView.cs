using UnityEngine;

/// <summary>
/// App側の画面（Title / StageSelect / Result など）の共通部分（B5）。
/// CanvasGroupで表示・操作の可否を切り替えるだけで、どの画面を出すかは知らない（ScreenPresenterが決める）。
///
/// Awakeでは何もしない。最初の表示切替はAppInitializer.Awake内から流れてくるため、
/// 各画面のAwakeより先に呼ばれることがある（StageTitleViewのEnsureInitializedと同じ事情）。
/// 初期状態はScreenPresenter.InitializeでHideAllして揃える。
/// </summary>
public abstract class ScreenView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;

    /// <summary>操作を受け付ける状態か。キー入力の判定はこれがtrueの時だけ行う</summary>
    protected bool IsInteractable => _canvasGroup.interactable;

    /// <summary>画面を出して操作を受け付ける</summary>
    protected void SetVisible(bool isVisible)
    {
        _canvasGroup.alpha = isVisible ? 1f : 0f;
        _canvasGroup.interactable = isVisible;
        _canvasGroup.blocksRaycasts = isVisible;
    }

    /// <summary>画面を消す</summary>
    public void Hide()
    {
        SetVisible(false);
    }

    /// <summary>
    /// 表示したまま操作だけ止める。遷移演出で画面が覆われるまでの間に、
    /// ボタンやキーがもう一度押されて同じ遷移が二重に要求されるのを防ぐ。
    /// </summary>
    public void Lock()
    {
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }
}
