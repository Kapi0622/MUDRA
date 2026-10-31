using System.Collections.Generic;
using System.Linq;
using MUDRA.Data;
using R3;
using UnityEngine;

/// <summary>
/// 図鑑（術・妖）を配線する。App側に置き、ステージ選択から開く。
///
/// 開くたびに SpellData / EnemyData とクリア記録から表示内容を組み立て、CodexViewに渡す。
/// 数値はそのまま見せず★にぼかす（攻略本ではなく読み物として眺めてもらうため）。
/// 術は最初から全部載せる。妖は、出てくるステージのどれかをクリアしていれば解放する
/// （クリア記録をそのまま使うので、セーブに図鑑用の項目を足さずに済む）。
/// チュートリアルの木人は_allStagesに入っていないので、自然に載らない。
///
/// SettingsPresenterと同じく開閉の状態をIsOpenで公開し、下の画面の操作止めは開いた側（ScreenPresenter）が行う。
/// </summary>
public class CodexPresenter : MonoBehaviour
{
    [SerializeField] private CodexView _view;

    [Tooltip("属性の文字色に使う")]
    [SerializeField] private BattlePaletteData _palette;

    [Header("サウンド。開く時の決定音は開いた側（ステージ選択）が鳴らす")]
    [SerializeField] private SePlayer _se;
    [SerializeField] private SoundData _sound;

    // --- ★の段階（以下なら その段階）。docs/balance_sheet.md の値の散らばりから決めた ---
    // 術の威力（basePower）: 双刃・風刃60 / 雷連撃85 / 光輪・水爪100・火炎弾110 / 3印の術140 / 業火175
    private static readonly float[] PowerStarThresholds = { 60f, 85f, 110f, 140f };
    // 妖の体力（maxHp）: 雑魚250〜650 / ボス1400〜2250。雑魚は★1〜3、ボスは★4〜5に収まる
    private static readonly int[] HpStarThresholds = { 300, 500, 700, 1600 };
    private const int MaxStars = 5;
    private const char FilledStar = '★';
    private const char EmptyStar = '☆';

    private const string LockedName = "？？？";
    private const string BossBadge = "ボス";
    private const string ReleaseSignName = "発";
    private const string SignSeparator = "→";
    private const string AttackSeparator = "／";
    private const string HeavyAttackSuffix = "<size=80%>（大技）</size>";
    private const string LockedDescription = "まだ相まみえていない。この妖が出るステージを討伐すると記される。";

    // 連続減衰は術ごとの性質ではなく全術共通の決まりなので、各術の末尾に同じ注記を添える（ポーズの術一覧と同じ扱い）
    private const string SpellFooter = "<size=85%>※ 同じ属性の術を続けて撃つと威力が下がる</size>";

    private static readonly Color LockedImageColor = Color.black;

    private readonly ReactiveProperty<bool> _isOpen = new(false);
    /// <summary>図鑑を開いているか</summary>
    public ReadOnlyReactiveProperty<bool> IsOpen => _isOpen;

    private IReadOnlyList<StageData> _stages;
    private IReadOnlyList<SpellData> _spells;
    private ProgressModel _progressModel;
    private readonly CompositeDisposable _disposables = new();

    /// <summary>AppInitializerから呼ばれる</summary>
    public void Initialize(IReadOnlyList<StageData> stages, IReadOnlyList<SpellData> spells, ProgressModel progressModel)
    {
        _stages = stages;
        _spells = spells;
        _progressModel = progressModel;
        _view.Hide();

        _view.OnCursorMoved
            .Subscribe(_ => _se.Play(_sound.uiCursor))
            .AddTo(_disposables);

        _view.OnCloseRequested
            .Subscribe(_ =>
            {
                _se.Play(_sound.uiBack);
                Close();
            })
            .AddTo(_disposables);
    }

    public void Open()
    {
        if (_isOpen.Value) return;
        _view.Show(BuildSpellEntries(), BuildEnemyEntries());
        _isOpen.Value = true;
    }

    public void Close()
    {
        if (!_isOpen.Value) return;
        _view.Hide();
        _isOpen.Value = false;
    }

    // ---------------- 術 ----------------

    /// <summary>印の少ない順（覚えやすい順）、同じ印数の中は属性の並び順</summary>
    private List<CodexEntry> BuildSpellEntries()
    {
        return _spells
            .Where(spell => spell != null)
            .OrderBy(spell => spell.sequence?.Length ?? 0)
            .ThenBy(spell => spell.element)
            .Select(BuildSpellEntry)
            .ToList();
    }

    private CodexEntry BuildSpellEntry(SpellData spell)
    {
        var signs = spell.sequence ?? new HandSign[0];
        string sequence = string.Join(SignSeparator,
            signs.Select(SequenceGuideView.GetSignDisplayName).Append(ReleaseSignName));

        var facts = new List<string>
        {
            $"属性　{ColoredElement(spell.element)}",
            $"印　　{sequence}",
        };

        // 回復だけの術は威力の★を出さない（★0個が並ぶと「弱い術」に見えるため）
        if (spell.basePower > 0f)
            facts.Add($"威力　{Stars(spell.basePower, PowerStarThresholds)}");

        string effect = EffectText(spell);
        if (!string.IsNullOrEmpty(effect))
            facts.Add($"効果　{effect}");

        return new CodexEntry(
            spell.spellName,
            string.Empty,
            spell.icon,
            Color.white,
            true,
            facts.ToArray(),
            spell.description,
            SpellFooter);
    }

    /// <summary>連撃・追加効果を短い言葉で。どちらも無ければ空</summary>
    private static string EffectText(SpellData spell)
    {
        var parts = new List<string>();
        if (spell.damageType == DamageType.MultiHit && spell.hitCount > 1)
            parts.Add("連撃");

        switch (spell.statusEffect)
        {
            case StatusEffectType.Stun:
                parts.Add("痺れ（敵の動きを止める）");
                break;
            case StatusEffectType.Slow:
                parts.Add("鈍足（敵の動きを遅くする）");
                break;
            case StatusEffectType.DamageOverTime:
                parts.Add("燃焼（しばらく削り続ける）");
                break;
            case StatusEffectType.HealOverTime when spell.healPower > 0f:
                parts.Add("回復（時間をかけて癒やす）");
                break;
        }
        return string.Join("・", parts);
    }

    // ---------------- 妖 ----------------

    /// <summary>ステージの順、ステージ内は出てくる順。同じ敵が複数のステージに出ても1体として扱う</summary>
    private List<CodexEntry> BuildEnemyEntries()
    {
        var order = new List<EnemyData>();
        var appearances = new Dictionary<EnemyData, List<StageData>>();
        var bosses = new HashSet<EnemyData>();

        foreach (var stage in _stages)
        {
            if (stage == null || stage.sections == null) continue;
            foreach (var section in stage.sections)
            {
                var enemy = section.enemyData;
                if (enemy == null) continue;

                if (!appearances.TryGetValue(enemy, out var stages))
                {
                    stages = new List<StageData>();
                    appearances.Add(enemy, stages);
                    order.Add(enemy);
                }
                if (!stages.Contains(stage)) stages.Add(stage);
                if (section.isBoss) bosses.Add(enemy);
            }
        }

        return order
            .Select(enemy => BuildEnemyEntry(enemy, appearances[enemy], bosses.Contains(enemy)))
            .ToList();
    }

    private CodexEntry BuildEnemyEntry(EnemyData enemy, List<StageData> stages, bool isBoss)
    {
        bool isUnlocked = stages.Any(stage => _progressModel.FindRecord(stage) != null);
        string footer = string.Join("\n", stages.Select(BuildAppearanceLine));

        if (!isUnlocked)
        {
            return new CodexEntry(
                LockedName,
                string.Empty,
                enemy.sprite,
                LockedImageColor,
                false,
                new string[0],
                LockedDescription,
                footer);
        }

        var facts = new[]
        {
            $"弱点　{ColoredElement(enemy.weakElement)}",
            $"体力　{Stars(enemy.maxHp, HpStarThresholds)}",
            $"技　　{AttackText(enemy)}",
        };

        return new CodexEntry(
            enemy.enemyName,
            isBoss ? BossBadge : string.Empty,
            enemy.sprite,
            Color.white,
            false,
            facts,
            enemy.description,
            footer);
    }

    /// <summary>「出現　封土の里　討伐済　S　3:12」。未クリアなら記録は付かない</summary>
    private string BuildAppearanceLine(StageData stage)
    {
        string record = StageRecordFormat.Format(_progressModel.FindRecord(stage));
        return string.IsNullOrEmpty(record)
            ? $"出現　{stage.stageName}"
            : $"出現　{stage.stageName}　{record}";
    }

    /// <summary>行動パターンの攻撃名を出てくる順に重複なく並べ、大技には印を付ける</summary>
    private static string AttackText(EnemyData enemy)
    {
        if (enemy.actionPattern == null || enemy.actionPattern.Length == 0) return "—";

        var names = new List<string>();
        var heavy = new HashSet<string>();
        foreach (var action in enemy.actionPattern)
        {
            if (action.attackData == null) continue;
            string name = action.attackData.attackName;
            if (!names.Contains(name)) names.Add(name);
            if (action.isHeavy) heavy.Add(name);
        }

        return string.Join(AttackSeparator,
            names.Select(name => heavy.Contains(name) ? name + HeavyAttackSuffix : name));
    }

    // ---------------- 共通 ----------------

    /// <summary>値がどの段階に入るかを★で。thresholdsは「この値以下ならその段階」を昇順に並べたもの</summary>
    private static string Stars(float value, IReadOnlyList<float> thresholds)
    {
        int stars = MaxStars;
        for (int i = 0; i < thresholds.Count; i++)
        {
            if (value <= thresholds[i])
            {
                stars = i + 1;
                break;
            }
        }
        return new string(FilledStar, stars) + new string(EmptyStar, MaxStars - stars);
    }

    private static string Stars(int value, IReadOnlyList<int> thresholds)
        => Stars(value, thresholds.Select(t => (float)t).ToArray());

    private string ColoredElement(ElementType element)
    {
        string hex = ColorUtility.ToHtmlStringRGB(ElementColor(element));
        return $"<color=#{hex}>{ElementNames.Get(element)}</color>";
    }

    private Color ElementColor(ElementType element)
        => _palette != null ? _palette.ElementColor(element) : Color.white;

    private void OnDestroy()
    {
        _disposables.Dispose();
        _isOpen.Dispose();
    }
}
