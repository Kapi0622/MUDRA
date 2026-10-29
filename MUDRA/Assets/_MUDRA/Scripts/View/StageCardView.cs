using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StageSelectに並ぶステージのカード1枚に出す内容（B5）。Modelの型を持ち込まないよう表示用の値だけにする。
/// </summary>
public readonly struct StageCardData
{
    public readonly int StageNumber;
    public readonly string StageName;
    public readonly string BossName;
    public readonly string WeakElementName;
    public readonly Color WeakElementColor;

    /// <summary>クリア済みの記録。未クリアなら空文字</summary>
    public readonly string RecordText;

    /// <summary>カードに敷く絵（UI改修 ステップ3）。nullなら無地。ステージ開始の演出（StageIntroView）では使わないので省略できる</summary>
    public readonly Sprite Thumbnail;

    public StageCardData(int stageNumber, string stageName, string bossName,
        string weakElementName, Color weakElementColor, string recordText, Sprite thumbnail = null)
    {
        StageNumber = stageNumber;
        StageName = stageName;
        BossName = bossName;
        WeakElementName = weakElementName;
        WeakElementColor = weakElementColor;
        RecordText = recordText;
        Thumbnail = thumbnail;
    }
}

/// <summary>
/// StageSelectのカード1枚（B5）。StageSelectViewがテンプレートから複製して並べる。
///
/// カードの内側にはステージの絵を敷き、上に黒の半透明を重ねて文字を読めるようにする（UI改修 ステップ3）。
/// 絵は縦長のカードに合わせて中央で切り抜く（AspectRatioFitterのEnvelopeParentで覆い、親のRectMask2Dで切る）。
/// 選択中のカードは黒を薄くして絵を明るく見せ、拡大と枠の色をまとめて短く補間する。
/// </summary>
public class StageCardView : MonoBehaviour
{
    // --- 見た目 ---
    private static readonly Color NormalFrameColor = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color SelectedFrameColor = new Color(1f, 0.85f, 0.3f, 1f);
    private const float NormalScale = 1f;
    private const float SelectedScale = 1.06f;
    private const float NormalShadeAlpha = 0.75f;
    private const float SelectedShadeAlpha = 0.4f;
    private const float SelectDuration = 0.15f;

    // 「其の一」のように幕の番号を漢数字で出す
    private static readonly string[] KanjiNumbers = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

    [SerializeField] private Button _button;
    [SerializeField] private Image _frame;
    [SerializeField] private TextMeshProUGUI _numberText;
    [SerializeField] private TextMeshProUGUI _stageNameText;
    [SerializeField] private TextMeshProUGUI _bossText;
    [SerializeField] private TextMeshProUGUI _weaknessText;
    [SerializeField] private TextMeshProUGUI _recordText;

    [Header("絵（UI改修 ステップ3）")]
    [Tooltip("ステージの絵。親にRectMask2D、自身にAspectRatioFitter（EnvelopeParent）を付けておく")]
    [SerializeField] private Image _thumbnail;
    [SerializeField] private AspectRatioFitter _thumbnailFitter;
    [Tooltip("絵の上に重ねる黒の半透明")]
    [SerializeField] private Image _shade;

    private MotionHandle _selectHandle;
    /// <summary>選択の度合い（0=通常、1=選択中）。補間の途中から逆向きに戻せるよう今の値を持つ</summary>
    private float _selection;

    /// <summary>クリックされた時の通知先。StageSelectViewが設定する</summary>
    public void SetClickHandler(Action onClick)
    {
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick());
    }

    public void SetData(StageCardData data)
    {
        _numberText.text = $"其の{ToKanji(data.StageNumber)}";
        _stageNameText.text = data.StageName;
        _bossText.text = $"主　{data.BossName}";
        _weaknessText.text = $"弱点　{data.WeakElementName}";
        _weaknessText.color = data.WeakElementColor;
        _recordText.text = data.RecordText;

        bool hasThumbnail = data.Thumbnail != null;
        _thumbnail.gameObject.SetActive(hasThumbnail);
        if (hasThumbnail)
        {
            _thumbnail.sprite = data.Thumbnail;
            var size = data.Thumbnail.rect.size;
            _thumbnailFitter.aspectRatio = size.x / size.y;
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (_selectHandle.IsActive()) _selectHandle.Cancel();
        _selectHandle = LMotion.Create(_selection, isSelected ? 1f : 0f, SelectDuration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutCubic)
            .Bind(ApplySelection);
    }

    private void ApplySelection(float t)
    {
        _selection = t;
        _frame.color = Color.Lerp(NormalFrameColor, SelectedFrameColor, t);
        float scale = Mathf.Lerp(NormalScale, SelectedScale, t);
        transform.localScale = new Vector3(scale, scale, 1f);
        var shade = _shade.color;
        _shade.color = new Color(shade.r, shade.g, shade.b, Mathf.Lerp(NormalShadeAlpha, SelectedShadeAlpha, t));
    }

    private void OnDestroy()
    {
        if (_selectHandle.IsActive()) _selectHandle.Cancel();
    }

    private static string ToKanji(int number)
    {
        return number >= 0 && number < KanjiNumbers.Length ? KanjiNumbers[number] : number.ToString();
    }
}
