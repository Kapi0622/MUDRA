using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using TMPro;
using UnityEngine;

/// <summary>
/// ステージ開始の演出（B5）。遷移の覆いが晴れた後、敵が出てくる前に流す。
///   1. 帯と一緒に「其の一」「ステージ名」「主　ボス名　／　弱点　属性」を出す
///   2. 帯を消して、中央に「結印！」を叩きつける
/// 終わるまで待てるようUniTaskを返す。これが終わってからバトルを始める（敵の登場・印の受付）。
/// ステージ名の表示はB4までStageTitleViewが担っていたが、ここに統合した。
/// </summary>
public class StageIntroView : MonoBehaviour
{
    // --- 演出定数 ---
    private const float FadeDuration = 0.3f;
    private const float CallPunchScale = 1.8f;
    private const float CallInDuration = 0.25f;
    private const float CallOutDuration = 0.25f;

    private const string CallText = "結印！";

    // 「其の一」のように幕の番号を漢数字で出す（StageCardViewと同じ表記）
    private static readonly string[] KanjiNumbers = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

    [SerializeField] private CanvasGroup _titleGroup;
    [SerializeField] private TextMeshProUGUI _numberText;
    [SerializeField] private TextMeshProUGUI _stageNameText;
    [SerializeField] private TextMeshProUGUI _bossText;

    [SerializeField] private CanvasGroup _callGroup;
    [SerializeField] private TextMeshProUGUI _callText;

    private MotionHandle _handle;
    private MotionHandle _scaleHandle;

    private void Awake()
    {
        _titleGroup.alpha = 0f;
        _callGroup.alpha = 0f;
    }

    /// <param name="titleDuration">ステージ名を出しておく長さ（フェードを含む）</param>
    /// <param name="callDuration">「結印！」を出しておく長さ（出入りを含む）</param>
    public async UniTask PlayAsync(StageCardData stage, float titleDuration, float callDuration, CancellationToken token)
    {
        int number = stage.StageNumber;
        _numberText.text = $"其の{(number >= 0 && number < KanjiNumbers.Length ? KanjiNumbers[number] : number.ToString())}";
        _stageNameText.text = stage.StageName;
        string colorHex = ColorUtility.ToHtmlStringRGB(stage.WeakElementColor);
        _bossText.text = $"主　{stage.BossName}　／　弱点　<color=#{colorHex}>{stage.WeakElementName}</color>";

        // 1. ステージ名
        await Fade(_titleGroup, 0f, 1f, FadeDuration, token);
        await UniTask.Delay(System.TimeSpan.FromSeconds(Mathf.Max(0f, titleDuration - FadeDuration * 2f)), cancellationToken: token);
        await Fade(_titleGroup, 1f, 0f, FadeDuration, token);

        // 2. 「結印！」
        _callText.text = CallText;
        var rect = _callText.rectTransform;
        _scaleHandle = LMotion.Create(CallPunchScale, 1f, CallInDuration)
            .WithEase(Ease.OutBack)
            .Bind(s => rect.localScale = new Vector3(s, s, 1f));
        await Fade(_callGroup, 0f, 1f, CallInDuration, token);
        await UniTask.Delay(System.TimeSpan.FromSeconds(Mathf.Max(0f, callDuration - CallInDuration - CallOutDuration)), cancellationToken: token);
        await Fade(_callGroup, 1f, 0f, CallOutDuration, token);
    }

    private UniTask Fade(CanvasGroup group, float from, float to, float duration, CancellationToken token)
    {
        if (_handle.IsActive()) _handle.Cancel();
        _handle = LMotion.Create(from, to, duration)
            .WithEase(Ease.OutQuad)
            .Bind(a => group.alpha = a);
        return _handle.ToUniTask(token);
    }

    private void OnDestroy()
    {
        if (_handle.IsActive()) _handle.Cancel();
        if (_scaleHandle.IsActive()) _scaleHandle.Cancel();
    }
}
