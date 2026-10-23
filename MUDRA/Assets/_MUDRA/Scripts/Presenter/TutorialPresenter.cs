using System.Collections.Generic;
using System.Linq;
using MUDRA.Data;
using R3;
using UnityEngine;

/// <summary>
/// チュートリアルの指示パネルを配線する（B6）。InGameに置き、チュートリアルの時だけBattleInitializerから注入を受ける。
/// 注入されなければ何もしない（指示パネルは消えたまま）。
///
/// 指示文の術名と印の並びはSpellDataから組み立てる（ScreenPresenter.BuildDefeatHintと同じやり方）。
/// 術や印を変えても、指示文を書き直さずに済むようにするため。
/// </summary>
public class TutorialPresenter : MonoBehaviour
{
    private const string ClearedText = "よし！";
    private const string CompletedTitle = "修練完了！";
    private const string CompletedDetail = "これで準備は整った。ステージ選択へ進む";
    private const string MisfireNote = "並びが違うと暴発して、自分が傷を負う。術の並びはポーズ（Esc）の術一覧でも見られる";

    [SerializeField] private TutorialGuideView _view;

    private readonly CompositeDisposable _disposables = new();

    /// <summary>BattleInitializerから呼ばれる（チュートリアルの時だけ）</summary>
    /// <param name="enemy">木人。弱点属性の段階で、弱点と術を示すのに使う</param>
    public void Initialize(
        TutorialModel tutorialModel,
        GameStateManager gameStateManager,
        PauseModel pauseModel,
        IReadOnlyList<SpellData> spells,
        EnemyData enemy)
    {
        var attackSpells = spells.Where(spell => spell != null && spell.basePower > 0f).ToArray();

        tutorialModel.CurrentStep
            .Subscribe(step =>
            {
                _view.SetStep(ProgressText(step), TitleText(step, enemy), DetailText(step, attackSpells, enemy));
                if (step == TutorialStep.Completed) _view.PlayCleared(CompletedTitle);
            })
            .AddTo(_disposables);

        tutorialModel.IsStepCleared
            .Where(isCleared => isCleared)
            .Subscribe(_ => _view.PlayCleared(ClearedText))
            .AddTo(_disposables);

        tutorialModel.OnMisfired
            .Subscribe(_ => _view.SetNote(MisfireNote))
            .AddTo(_disposables);

        pauseModel.IsPaused
            .Subscribe(isPaused => _view.SetInputEnabled(!isPaused))
            .AddTo(_disposables);

        _view.OnSkipRequested
            .Subscribe(_ =>
            {
                if (gameStateManager.SkipTutorial()) _view.Lock();
            })
            .AddTo(_disposables);

        _view.Show();
    }

    private static string ProgressText(TutorialStep step)
    {
        int number = Mathf.Min((int)step + 1, TutorialModel.StepCount);
        return $"修練　{number} / {TutorialModel.StepCount}";
    }

    private static string TitleText(TutorialStep step, EnemyData enemy)
    {
        string release = Sign(HandSign.Release);
        string cancel = Sign(HandSign.Cancel);
        string guard = Sign(HandSign.Guard);

        return step switch
        {
            TutorialStep.Chant => "詠唱：印を組もう",
            TutorialStep.Release => $"発動：印を並べて「{release}」で放とう",
            TutorialStep.Cancel => $"解除：「{cancel}」で並びを取り消そう",
            TutorialStep.Guard => $"防御：攻撃の予告が出たら「{guard}」で受けよう",
            TutorialStep.Element => $"属性：{enemy.enemyName}は{ElementNames.Get(enemy.weakElement)}に弱い",
            _ => CompletedTitle,
        };
    }

    private static string DetailText(TutorialStep step, IReadOnlyList<SpellData> attackSpells, EnemyData enemy)
    {
        string chantSigns = string.Join("・",
            new[] { HandSign.Open, HandSign.Fist, HandSign.Point, HandSign.Scissors, HandSign.Palm }.Select(Sign));

        switch (step)
        {
            case TutorialStep.Chant:
                return $"詠唱印（{chantSigns}）のどれかを組むと、画面に並ぶ";

            case TutorialStep.Release:
            {
                // 例には並びが一番短い術を出す（最初の1回を成功させやすいように）
                var example = attackSpells.OrderBy(spell => spell.sequence.Length).FirstOrDefault();
                return example != null
                    ? $"術の並びどおりに印を組んでから「{Sign(HandSign.Release)}」。例：{SpellWithSequence(example)}"
                    : $"術の並びどおりに印を組んでから「{Sign(HandSign.Release)}」";
            }

            case TutorialStep.Cancel:
                return $"印を1つ以上組んでから「{Sign(HandSign.Cancel)}」。間違えた時は、放たずに取り消せば暴発しない";

            case TutorialStep.Guard:
                return $"敵が力を溜めている間に「{Sign(HandSign.Guard)}」を組むと、受けるダメージが減る";

            case TutorialStep.Element:
            {
                var weakSpells = attackSpells.Where(spell => spell.element == enemy.weakElement).Select(SpellWithSequence).ToArray();
                string element = ElementNames.Get(enemy.weakElement);
                return weakSpells.Length > 0
                    ? $"弱点の属性で攻めると大きく効く。{element}の術：{string.Join("　", weakSpells)}"
                    : "弱点の属性で攻めると大きく効く";
            }

            default:
                return CompletedDetail;
        }
    }

    /// <summary>「火炎弾（掌 → 握 → 発）」</summary>
    private static string SpellWithSequence(SpellData spell)
    {
        var signs = spell.sequence.Select(Sign).Append(Sign(HandSign.Release));
        return $"{spell.spellName}（{string.Join(" → ", signs)}）";
    }

    private static string Sign(HandSign sign) => SequenceGuideView.GetSignDisplayName(sign);

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}
