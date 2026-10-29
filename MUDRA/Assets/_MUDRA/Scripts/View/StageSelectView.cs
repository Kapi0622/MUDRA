using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ステージ選択画面（B5）。カードを並べ、選択と決定・戻るの要求を流す。
///
/// 操作: 数字キー1〜4でカーソル移動 → Space / Enter で決定。カードのクリックは即決定。
/// Backspace / Esc、または戻るボタンでタイトルへ。
/// </summary>
public class StageSelectView : ScreenView
{
    // 数字キーでカーソルを動かせるのは先頭から9枚まで
    private static readonly KeyCode[] NumberKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
    };

    [SerializeField] private RectTransform _cardContainer;
    [Tooltip("非アクティブのテンプレート。ステージの数だけ複製する")]
    [SerializeField] private StageCardView _cardTemplate;
    [SerializeField] private Button _backButton;

    private readonly List<StageCardView> _cards = new();
    private int _selectedIndex;

    private readonly Subject<int> _onStageDecided = new();
    /// <summary>ステージが決定された。引数はカードの並び順（=全ステージの並び順）</summary>
    public Observable<int> OnStageDecided => _onStageDecided;

    private readonly Subject<Unit> _onBackRequested = new();
    public Observable<Unit> OnBackRequested => _onBackRequested;

    private readonly Subject<Unit> _onCursorMoved = new();
    /// <summary>キー操作でカーソルが別のカードへ動いた（B7。画面を開いた時の初期位置では流さない）</summary>
    public Observable<Unit> OnCursorMoved => _onCursorMoved;

    private void Awake()
    {
        _backButton.onClick.AddListener(() => _onBackRequested.OnNext(Unit.Default));
    }

    /// <summary>
    /// カードの内容を設定する。初回はテンプレートから枚数分を作り、以降は中身だけ書き換える
    /// （クリア記録が変わった時に呼び直せるように）。
    /// </summary>
    public void SetStages(IReadOnlyList<StageCardData> stages)
    {
        _cardTemplate.gameObject.SetActive(false);

        while (_cards.Count < stages.Count)
        {
            int index = _cards.Count;
            var card = Instantiate(_cardTemplate, _cardContainer);
            card.gameObject.SetActive(true);
            card.SetClickHandler(() =>
            {
                if (!IsInteractable) return;
                Select(index);
                _onStageDecided.OnNext(index);
            });
            _cards.Add(card);
        }

        for (int i = 0; i < _cards.Count; i++)
        {
            bool hasData = i < stages.Count;
            _cards[i].gameObject.SetActive(hasData);
            if (hasData) _cards[i].SetData(stages[i]);
        }
    }

    /// <summary>画面を出す。カーソルはselectedIndexのカードに置く（直前に遊んだステージ）</summary>
    public void Show(int selectedIndex)
    {
        SetVisible(true);
        Select(Mathf.Clamp(selectedIndex, 0, Mathf.Max(_cards.Count - 1, 0)));
    }

    private void Select(int index)
    {
        _selectedIndex = index;
        for (int i = 0; i < _cards.Count; i++)
            _cards[i].SetSelected(i == index);
    }

    private void Update()
    {
        if (!IsInteractable || _cards.Count == 0) return;

        for (int i = 0; i < NumberKeys.Length && i < _cards.Count; i++)
        {
            if (Input.GetKeyDown(NumberKeys[i]) && i != _selectedIndex)
            {
                Select(i);
                _onCursorMoved.OnNext(Unit.Default);
            }
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            _onStageDecided.OnNext(_selectedIndex);
        else if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
            _onBackRequested.OnNext(Unit.Default);
    }

    private void OnDestroy()
    {
        _onStageDecided.Dispose();
        _onBackRequested.Dispose();
        _onCursorMoved.Dispose();
    }
}
