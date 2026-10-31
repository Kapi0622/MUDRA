using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 図鑑の1項目に出す内容。術と妖で同じ形にして、Viewは中身の意味を知らずに並べるだけにする
/// （SpellListRowと同じく、Modelの型を持ち込まず表示用の値だけを持つ）。
/// </summary>
public readonly struct CodexEntry
{
    /// <summary>名前。未解放なら「？？？」</summary>
    public readonly string Name;

    /// <summary>名前の横に小さく添える札（例: 「ボス」）。無ければ空</summary>
    public readonly string Badge;

    /// <summary>術のアイコン・妖の絵。無ければnull</summary>
    public readonly Sprite Image;

    /// <summary>絵の色。未解放の妖は黒で影にする</summary>
    public readonly Color ImageColor;

    /// <summary>
    /// 絵を円い台紙に載せるか。術アイコンは白地に黒い墨の絵なので、暗い画面でも見えるよう台紙に載せる。
    /// 台紙の絵と、絵に掛ける色はシーン側（CodexImagePlate）で決める
    /// </summary>
    public readonly bool HasPlate;

    /// <summary>属性・印・★などの短い行（リッチテキスト可）</summary>
    public readonly string[] Facts;

    public readonly string Description;

    /// <summary>詳細の末尾に出す補足（術は共通の注意書き、妖は出現ステージと記録）</summary>
    public readonly string Footer;

    public CodexEntry(string name, string badge, Sprite image, Color imageColor, bool hasPlate,
        string[] facts, string description, string footer)
    {
        Name = name;
        Badge = badge;
        Image = image;
        ImageColor = imageColor;
        HasPlate = hasPlate;
        Facts = facts;
        Description = description;
        Footer = footer;
    }
}

/// <summary>図鑑のタブ</summary>
public enum CodexTab
{
    Spell,
    Enemy,
}

/// <summary>
/// 図鑑の画面。上にタブ（術／妖）、左に一覧、右に選んだ項目の詳細を出す。
/// 解放の判定や★の段階はPresenterが決めて文字にしてから渡すので、ここは並べて選ぶだけ。
///
/// 操作: Q / E または ← / → でタブ切替、↑ / ↓ で一覧の選択、Backspace / Esc で閉じる。項目・タブはクリックでも選べる。
/// ステージ選択の上に重ねて開く。閉じた時に下の画面が同じキーを拾わないよう、
/// 開いたフレームはキーを読まない（下の画面側も戻ったフレームは読まない）。
/// </summary>
public class CodexView : ScreenView
{
    private static readonly Color SelectedTabColor = Color.white;
    private static readonly Color UnselectedTabColor = new(1f, 1f, 1f, 0.4f);

    [Header("タブ")]
    [SerializeField] private Button _spellTabButton;
    [SerializeField] private TextMeshProUGUI _spellTabLabel;
    [SerializeField] private Button _enemyTabButton;
    [SerializeField] private TextMeshProUGUI _enemyTabLabel;

    [Header("一覧")]
    [SerializeField] private RectTransform _listContainer;
    [Tooltip("非アクティブのテンプレート。項目の数だけ複製する")]
    [SerializeField] private CodexListItemView _itemTemplate;
    [Tooltip("項目が画面に収まらない時のスクロール。無くてもよい")]
    [SerializeField] private ScrollRect _listScroll;

    [Header("詳細")]
    [SerializeField] private Image _detailImage;
    [Tooltip("絵の台紙（円・Mask付き）。_detailImageはこの子に置く")]
    [SerializeField] private CodexImagePlate _detailPlate;
    [SerializeField] private TextMeshProUGUI _detailNameText;
    [SerializeField] private TextMeshProUGUI _detailFactsText;
    [SerializeField] private TextMeshProUGUI _detailDescriptionText;
    [SerializeField] private TextMeshProUGUI _detailFooterText;

    [SerializeField] private Button _closeButton;

    private readonly List<CodexListItemView> _items = new();
    private IReadOnlyList<CodexEntry> _spellEntries = new CodexEntry[0];
    private IReadOnlyList<CodexEntry> _enemyEntries = new CodexEntry[0];
    private CodexTab _tab = CodexTab.Spell;
    private int _selectedIndex;
    private int _shownFrame = -1;

    private readonly Subject<Unit> _onCloseRequested = new();
    public Observable<Unit> OnCloseRequested => _onCloseRequested;

    private readonly Subject<Unit> _onCursorMoved = new();
    /// <summary>キー・クリックでタブか選択中の項目が変わった（効果音用）</summary>
    public Observable<Unit> OnCursorMoved => _onCursorMoved;

    private IReadOnlyList<CodexEntry> CurrentEntries => _tab == CodexTab.Spell ? _spellEntries : _enemyEntries;

    private void Awake()
    {
        _spellTabButton.onClick.AddListener(() => ChangeTabByUser(CodexTab.Spell));
        _enemyTabButton.onClick.AddListener(() => ChangeTabByUser(CodexTab.Enemy));
        _closeButton.onClick.AddListener(() => _onCloseRequested.OnNext(Unit.Default));
    }

    /// <summary>
    /// 中身を入れて画面を出す。開くたびに呼ぶ（クリア記録で解放が変わるため）。
    /// タブと選択位置は前回開いた時のまま残す。
    /// </summary>
    public void Show(IReadOnlyList<CodexEntry> spells, IReadOnlyList<CodexEntry> enemies)
    {
        _spellEntries = spells;
        _enemyEntries = enemies;
        SetVisible(true);
        _shownFrame = Time.frameCount;
        ShowTab(_tab, _selectedIndex);
    }

    private void ChangeTabByUser(CodexTab tab)
    {
        if (!IsInteractable || tab == _tab) return;
        ShowTab(tab, 0);
        _onCursorMoved.OnNext(Unit.Default);
    }

    private void ShowTab(CodexTab tab, int selectedIndex)
    {
        _tab = tab;
        _spellTabLabel.color = tab == CodexTab.Spell ? SelectedTabColor : UnselectedTabColor;
        _enemyTabLabel.color = tab == CodexTab.Enemy ? SelectedTabColor : UnselectedTabColor;

        var entries = CurrentEntries;
        _itemTemplate.gameObject.SetActive(false);

        while (_items.Count < entries.Count)
        {
            int index = _items.Count;
            var item = Instantiate(_itemTemplate, _listContainer);
            item.gameObject.SetActive(true);
            item.SetClickHandler(() =>
            {
                if (!IsInteractable || index == _selectedIndex) return;
                Select(index);
                _onCursorMoved.OnNext(Unit.Default);
            });
            _items.Add(item);
        }

        for (int i = 0; i < _items.Count; i++)
        {
            bool hasData = i < entries.Count;
            _items[i].gameObject.SetActive(hasData);
            if (hasData) _items[i].SetData(entries[i]);
        }

        Select(Mathf.Clamp(selectedIndex, 0, Mathf.Max(entries.Count - 1, 0)));
    }

    private void Select(int index)
    {
        _selectedIndex = index;
        var entries = CurrentEntries;
        for (int i = 0; i < entries.Count && i < _items.Count; i++)
            _items[i].SetSelected(i == index);

        ScrollTo(index, entries.Count);

        if (entries.Count == 0) return;
        ShowDetail(entries[index]);
    }

    private void ShowDetail(in CodexEntry entry)
    {
        _detailNameText.text = string.IsNullOrEmpty(entry.Badge)
            ? entry.Name
            : $"{entry.Name}<size=60%>　{entry.Badge}</size>";

        _detailPlate.Show(_detailImage, entry);

        _detailFactsText.text = entry.Facts != null ? string.Join("\n", entry.Facts) : string.Empty;
        _detailDescriptionText.text = entry.Description ?? string.Empty;
        _detailFooterText.text = entry.Footer ?? string.Empty;
    }

    /// <summary>選んだ項目が見える位置まで一覧を送る（上端=1、下端=0）</summary>
    private void ScrollTo(int index, int count)
    {
        if (_listScroll == null || count <= 1) return;
        _listScroll.verticalNormalizedPosition = 1f - index / (float)(count - 1);
    }

    private void Update()
    {
        if (!IsInteractable || Time.frameCount == _shownFrame) return;

        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
        {
            _onCloseRequested.OnNext(Unit.Default);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow))
            ChangeTabByUser(CodexTab.Spell);
        else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow))
            ChangeTabByUser(CodexTab.Enemy);

        int count = CurrentEntries.Count;
        if (count == 0) return;

        int next = _selectedIndex;
        if (Input.GetKeyDown(KeyCode.UpArrow)) next = Mathf.Max(0, _selectedIndex - 1);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) next = Mathf.Min(count - 1, _selectedIndex + 1);

        if (next != _selectedIndex)
        {
            Select(next);
            _onCursorMoved.OnNext(Unit.Default);
        }
    }

    private void OnDestroy()
    {
        _onCloseRequested.Dispose();
        _onCursorMoved.Dispose();
    }
}
