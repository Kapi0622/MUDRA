using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MUDRA.Data;
using UnityEditor;
using UnityEngine;

/// <summary>
/// B3の敵・攻撃・ステージのScriptableObjectを一括生成するEditor拡張。
/// 依頼書: docs/B3_enemy_stage_content.md
///
/// 定義値（クラス前半の表）と処理部分（後半）を分離している。
/// B8でバランス調整用スプレッドシートからのCSV取り込みに切り替える際は、
/// 表の部分をCSV読み込みに差し替えれば処理部分はそのまま使える。
/// 行動パターンを依頼書の表記に近い1行の文字列で持たせているのもそのため（CSVの1セルに収まる）。
///
/// 再実行しても重複しない。既存アセットはファイル名で判定し、値だけを上書きする。
/// スプライト・エフェクト・SE・BGM・背景のフィールドには一切書き込まない
/// （既に設定済みの絵や音を消さないため）。
/// コンテンツ作成用の一時ツールであり、ランタイムコードからは参照しない。
/// </summary>
public static class B3ContentGenerator
{
    private const string MenuPath = "MUDRA/Generate B3 Content";
    private const string LogTag = "[B3ContentGenerator]";

    private const string AttackDir = "Assets/_MUDRA/ScriptableObject/Enemy/EnemyAttackData";
    private const string EnemyDir = "Assets/_MUDRA/ScriptableObject/Enemy/EnemyData";
    private const string StageDir = "Assets/_MUDRA/ScriptableObject/Stage";

    private const string AttackPrefix = "EA_";
    private const string EnemyPrefix = "ED_";
    private const string StagePrefix = "SD_";

    // 表を読みやすくするための別名
    private const bool Heavy = true;
    private const bool Normal = false;
    private const float DefaultWeak = 1.5f;

    // ============================================================
    // 定義表
    // ============================================================

    /// <summary>
    /// 既存アセットのリネーム対応表（旧ファイル名 → 新ファイル名）。
    /// AssetDatabase.RenameAssetで行うため.metaのGUIDは維持され、既存の参照は切れない。
    /// </summary>
    private static readonly (string Dir, string OldName, string NewName)[] LegacyRenames =
    {
        (AttackDir, "MudballToss",    "EA_MudThrow"),
        (AttackDir, "Earthrumblings", "EA_Tremor"),
        (EnemyDir,  "ClayDoll",       "ED_MudGolem"),
        (StageDir,  "Stage1",         "SD_Stage01_SealedVillage"),
    };

    /// <summary>
    /// 攻撃データ。Keyはファイル名からプレフィックスを除いたもの。
    /// 最後の列（大技か否か）はEnemyAttackDataのフィールドではなく、
    /// この攻撃を使う全ての行動のisHeavyに自動で反映される。
    /// 行動ごとに書かせないことで「同じ攻撃が行動によって大技になったりならなかったりする」事故を構造的に防ぐ。
    /// </summary>
    private static readonly AttackDef[] Attacks =
    {
        //   Key               attackName   dmg  charge  大技
        new("MudSplash",      "泥はね",      4,  1.5f,  Normal),
        new("MudThrow",       "泥塊投げ",    4,  1.5f,  Normal),
        new("Tremor",         "地鳴り",     10,  2.0f,  Heavy),
        new("BranchWhip",     "枝打ち",      4,  1.5f,  Normal),
        new("RootBind",       "根縛り",     10,  2.5f,  Heavy),
        new("WaterGun",       "水鉄砲",      3,  1.0f,  Normal),
        new("ClawPinch",      "鋏挟み",      6,  2.0f,  Normal),
        new("ShellCrush",     "甲羅潰し",   14,  2.5f,  Heavy),
        new("WaterBullet",    "水弾",        3,  1.2f,  Normal),
        new("Whirlpool",      "渦潮",       10,  2.0f,  Heavy),
        new("Tsunami",        "大津波",     16,  2.5f,  Heavy),
        new("Ember",          "火の粉",      3,  0.8f,  Normal),
        new("RunOver",        "轢き",        6,  1.0f,  Normal),
        new("BlazeWheelRush", "炎輪突進",   14,  1.2f,  Heavy),
        new("InfernoFist",    "業火拳",      5,  1.0f,  Normal),
        new("ScorchWave",     "焦熱波",     14,  1.2f,  Heavy),
        new("FlameBurst",     "爆炎",       22,  1.8f,  Heavy),
        new("ShadowStab",     "影刺し",      5,  1.5f,  Normal),
        new("ShadowStitch",   "影縫い",     12,  0.8f,  Heavy),
        new("ThunderRoar",    "雷鳴",       14,  1.5f,  Heavy),
        new("ShadowClaw",     "影爪",        6,  1.5f,  Normal),
        new("BlackLightning", "黒雷",       14,  1.0f,  Heavy),
        new("NetherPrison",   "冥獄",       28,  3.0f,  Heavy),
    };

    /// <summary>
    /// 敵データ。行動パターンの書式は「攻撃Key(intervalAfter秒)」を " > " で繋いだもの。
    /// 末尾の "x3" は同じ要素を3回並べることを表す（依頼書の「×3」）。
    /// </summary>
    private static readonly EnemyDef[] Enemies =
    {
        // --- ステージ1「封土の里」 ---
        new("MudBlob",         "泥ころ",   200, ElementType.Wind,    DefaultWeak,
            "MudSplash(3.0)"),
        new("WitheredKodama",  "枯れ木霊", 250, ElementType.Fire,    DefaultWeak,
            "BranchWhip(2.5) > BranchWhip(2.5) > RootBind(4.0)"),
        new("MudGolem",        "泥人形",   800, ElementType.Wind,    DefaultWeak,
            "MudThrow(3.0)x3 > Tremor(5.0)"),

        // --- ステージ2「水底の社」 ---
        new("Kappa",           "河童",     300, ElementType.Thunder, DefaultWeak,
            "WaterGun(0.8) > WaterGun(0.8) > WaterGun(4.0)"),
        new("StoneMudBlob",    "岩泥ころ", 300, ElementType.Wind,    DefaultWeak,
            "MudSplash(2.0)"),
        new("CrabMonk",        "蟹坊主",   380, ElementType.Earth,   DefaultWeak,
            "ClawPinch(2.0) > ShellCrush(5.0)"),
        new("WaterSerpent",    "水蛇",    1000, ElementType.Thunder, DefaultWeak,
            "WaterBullet(0.8)x3 > Whirlpool(3.0) > WaterBullet(0.8)x2 > Tsunami(5.0)"),

        // --- ステージ3「焔の廃寺」 ---
        new("WillOWisp",       "鬼火",     250, ElementType.Water,   DefaultWeak,
            "Ember(1.5)"),
        new("BurningKodama",   "燃え木霊", 350, ElementType.Water,   DefaultWeak,
            "BranchWhip(1.5) > BranchWhip(1.5) > RootBind(2.5)"),
        new("Kasha",           "火車",     450, ElementType.Earth,   DefaultWeak,
            "RunOver(2.0) > BlazeWheelRush(4.0)"),
        new("FlameDemon",      "炎魔",    1200, ElementType.Water,   DefaultWeak,
            "InfernoFist(1.5)x2 > ScorchWave(2.0) > InfernoFist(1.5) > FlameBurst(7.0)"),

        // --- ステージ4「影の深淵」 ---
        new("Shade",           "影法師",   400, ElementType.Light,   DefaultWeak,
            "ShadowStab(0.8) > ShadowStab(4.0) > ShadowStitch(3.0)"),
        new("ShadowWillOWisp", "影鬼火",   350, ElementType.Light,   DefaultWeak,
            "Ember(1.0)"),
        new("Nue",             "鵺",       550, ElementType.Fire,    DefaultWeak,
            "WaterBullet(1.5) > BranchWhip(1.5) > Ember(1.5) > ThunderRoar(4.0)"),
        new("ShadowDragon",    "影龍",    1600, ElementType.Light,   2.0f,
            "ShadowClaw(1.5)x2 > BlackLightning(2.5) > ShadowClaw(1.0) > ShadowStitch(2.0) > NetherPrison(6.0)"),
    };

    /// <summary>
    /// ステージデータ。sectionsは先頭から順に進行し、最後の1つだけがボス（isBoss = true）になる。
    /// </summary>
    private static readonly StageDef[] Stages =
    {
        new("Stage01_SealedVillage",  1, "封土の里",
            "MudBlob", "WitheredKodama", "MudGolem"),
        new("Stage02_SunkenShrine",   2, "水底の社",
            "Kappa", "StoneMudBlob", "CrabMonk", "WaterSerpent"),
        new("Stage03_BurningTemple",  3, "焔の廃寺",
            "WillOWisp", "BurningKodama", "Kasha", "FlameDemon"),
        new("Stage04_ShadowAbyss",    4, "影の深淵",
            "Shade", "ShadowWillOWisp", "Nue", "ShadowDragon"),
    };

    // ============================================================
    // 処理
    // ============================================================

    [MenuItem(MenuPath)]
    public static void Generate()
    {
        // --- 書き込み前の検査 ---
        // 表の誤記（存在しない攻撃Keyなど）で半端な状態のアセットを残さないよう、
        // 1つでもエラーがあれば何も書き込まずに中断する
        var errors = new List<string>();
        CheckFolders(errors);
        var patterns = ParseAllPatterns(errors);
        CheckReferences(patterns, errors);

        if (errors.Count > 0)
        {
            foreach (var e in errors) Debug.LogError($"{LogTag} {e}");
            Debug.LogError($"{LogTag} 定義表にエラーがあるため中断しました。アセットは変更していません");
            return;
        }

        // --- 生成 ---
        var report = new Report();

        RenameLegacyAssets(report);
        var attacks = UpsertAttacks(report);
        var enemies = UpsertEnemies(attacks, patterns, report);
        UpsertStages(enemies, report);

        AssetDatabase.SaveAssets();
        report.Log();

        // --- 生成後の検証（依頼書の完了条件④⑤⑥） ---
        VerifyGeneratedAssets();
    }

    // ------------------------------------------------------------
    // 書き込み前の検査
    // ------------------------------------------------------------

    private static void CheckFolders(List<string> errors)
    {
        foreach (var dir in new[] { AttackDir, EnemyDir, StageDir })
        {
            if (!AssetDatabase.IsValidFolder(dir))
                errors.Add($"フォルダが存在しません: {dir}");
        }
    }

    // 「Key(秒)」または「Key(秒)xN」
    private static readonly Regex StepPattern =
        new(@"^(?<key>\w+)\((?<interval>\d+(?:\.\d+)?)\)(?:x(?<count>\d+))?$");

    /// <summary>
    /// 全敵の行動パターン文字列を展開する。「x3」はここで3要素に展開される。
    /// </summary>
    private static Dictionary<string, List<ActionStep>> ParseAllPatterns(List<string> errors)
    {
        var result = new Dictionary<string, List<ActionStep>>();

        foreach (var enemy in Enemies)
        {
            var steps = new List<ActionStep>();

            foreach (var token in enemy.Pattern.Split('>'))
            {
                var match = StepPattern.Match(token.Trim());
                if (!match.Success)
                {
                    errors.Add($"{enemy.Key} の行動パターンを解釈できません: \"{token.Trim()}\"");
                    continue;
                }

                string attackKey = match.Groups["key"].Value;
                float interval = float.Parse(match.Groups["interval"].Value, CultureInfo.InvariantCulture);
                int count = match.Groups["count"].Success ? int.Parse(match.Groups["count"].Value) : 1;

                for (int i = 0; i < count; i++)
                    steps.Add(new ActionStep(attackKey, interval));
            }

            if (steps.Count == 0)
                errors.Add($"{enemy.Key} の行動パターンが空です");

            result[enemy.Key] = steps;
        }

        return result;
    }

    /// <summary>
    /// 表の間の参照（敵→攻撃、ステージ→敵）がすべて解決できるかを確認する。
    /// </summary>
    private static void CheckReferences(Dictionary<string, List<ActionStep>> patterns, List<string> errors)
    {
        var attackKeys = new HashSet<string>(Attacks.Select(a => a.Key));
        var enemyKeys = new HashSet<string>(Enemies.Select(e => e.Key));

        foreach (var pair in patterns)
        {
            foreach (var step in pair.Value)
            {
                if (!attackKeys.Contains(step.AttackKey))
                    errors.Add($"{pair.Key} の行動パターンに未定義の攻撃があります: {step.AttackKey}");
            }
        }

        foreach (var stage in Stages)
        {
            foreach (var enemyKey in stage.EnemyKeys)
            {
                if (!enemyKeys.Contains(enemyKey))
                    errors.Add($"{stage.Key} のセクションに未定義の敵があります: {enemyKey}");
            }
        }
    }

    // ------------------------------------------------------------
    // 生成
    // ------------------------------------------------------------

    /// <summary>
    /// 既存アセットを新しい命名規則へリネームする。
    /// 新旧両方が存在する場合は、どちらが正なのか判断できないのでスキップして警告を出す。
    /// </summary>
    private static void RenameLegacyAssets(Report report)
    {
        foreach (var (dir, oldName, newName) in LegacyRenames)
        {
            string oldPath = $"{dir}/{oldName}.asset";
            string newPath = $"{dir}/{newName}.asset";

            if (AssetDatabase.LoadMainAssetAtPath(oldPath) == null) continue;

            if (AssetDatabase.LoadMainAssetAtPath(newPath) != null)
            {
                Debug.LogWarning(
                    $"{LogTag} {oldPath} と {newPath} が両方存在するためリネームをスキップしました。" +
                    "旧アセットを確認して手動で整理してください");
                continue;
            }

            string error = AssetDatabase.RenameAsset(oldPath, newName);
            if (string.IsNullOrEmpty(error))
                report.Renamed.Add($"{oldName} → {newName}");
            else
                Debug.LogError($"{LogTag} リネームに失敗しました: {oldPath} → {newName} ({error})");
        }
    }

    private static Dictionary<string, EnemyAttackData> UpsertAttacks(Report report)
    {
        var result = new Dictionary<string, EnemyAttackData>();

        foreach (var def in Attacks)
        {
            var asset = LoadOrCreate<EnemyAttackData>($"{AttackDir}/{AttackPrefix}{def.Key}.asset", report);
            if (asset == null) continue;

            asset.attackName = def.AttackName;
            asset.damage = def.Damage;
            asset.chargeTime = def.ChargeTime;
            // effectPrefab / attackSE には触らない

            EditorUtility.SetDirty(asset);
            result[def.Key] = asset;
        }

        return result;
    }

    private static Dictionary<string, EnemyData> UpsertEnemies(
        Dictionary<string, EnemyAttackData> attacks,
        Dictionary<string, List<ActionStep>> patterns,
        Report report)
    {
        var isHeavyByKey = Attacks.ToDictionary(a => a.Key, a => a.IsHeavy);
        var result = new Dictionary<string, EnemyData>();

        foreach (var def in Enemies)
        {
            var asset = LoadOrCreate<EnemyData>($"{EnemyDir}/{EnemyPrefix}{def.Key}.asset", report);
            if (asset == null) continue;

            asset.enemyName = def.EnemyName;
            asset.maxHp = def.MaxHp;
            asset.weakElement = def.WeakElement;
            asset.weakMultiplier = def.WeakMultiplier;
            // sprite には触らない（泥人形には既にスプライトが設定されている）

            asset.actionPattern = patterns[def.Key]
                .Select(step => new EnemyAction
                {
                    attackData = attacks.TryGetValue(step.AttackKey, out var attack) ? attack : null,
                    isHeavy = isHeavyByKey[step.AttackKey],
                    intervalAfter = step.Interval,
                })
                .ToArray();

            EditorUtility.SetDirty(asset);
            result[def.Key] = asset;
        }

        return result;
    }

    private static void UpsertStages(Dictionary<string, EnemyData> enemies, Report report)
    {
        foreach (var def in Stages)
        {
            var asset = LoadOrCreate<StageData>($"{StageDir}/{StagePrefix}{def.Key}.asset", report);
            if (asset == null) continue;

            asset.stageName = def.StageName;
            asset.stageNumber = def.StageNumber;
            // bgm / roadBackgroundSprite / bossBackgroundSprite には触らない

            int lastIndex = def.EnemyKeys.Length - 1;
            asset.sections = def.EnemyKeys
                .Select((key, i) => new StageSection
                {
                    enemyData = enemies.TryGetValue(key, out var enemy) ? enemy : null,
                    isBoss = i == lastIndex,
                })
                .ToArray();

            EditorUtility.SetDirty(asset);
        }
    }

    /// <summary>
    /// 指定パスのアセットを読み込む。無ければ新規作成する。
    /// 同じパスに別の型のアセットがある場合は上書きせずnullを返す
    /// （CreateAssetは既存ファイルを黙って置き換えてしまうため）。
    /// </summary>
    private static T LoadOrCreate<T>(string path, Report report) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            report.Updated.Add(path);
            return asset;
        }

        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            Debug.LogError($"{LogTag} {path} に {typeof(T).Name} 以外のアセットがあるためスキップしました");
            return null;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        report.Created.Add(path);
        return asset;
    }

    // ------------------------------------------------------------
    // 生成後の検証
    // ------------------------------------------------------------

    /// <summary>
    /// 保存済みのアセットをディスクから読み直し、依頼書の完了条件④⑤⑥を検査する。
    /// ④ 全EnemyDataのactionPatternにnull参照がない
    /// ⑤ 大技指定の攻撃を使う行動がすべてisHeavy = true（それ以外はfalse）
    /// ⑥ StageDataのsectionsが表の順序どおりで、最後のセクションのみisBoss = true
    /// </summary>
    private static void VerifyGeneratedAssets()
    {
        var errors = new List<string>();
        var isHeavyByName = Attacks.ToDictionary(a => AttackPrefix + a.Key, a => a.IsHeavy);

        // ④⑤
        foreach (var def in Enemies)
        {
            string path = $"{EnemyDir}/{EnemyPrefix}{def.Key}.asset";
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (enemy == null)
            {
                errors.Add($"④ {path} が存在しません");
                continue;
            }

            if (enemy.actionPattern == null || enemy.actionPattern.Length == 0)
            {
                errors.Add($"④ {enemy.name} の actionPattern が空です");
                continue;
            }

            for (int i = 0; i < enemy.actionPattern.Length; i++)
            {
                var action = enemy.actionPattern[i];
                if (action.attackData == null)
                {
                    errors.Add($"④ {enemy.name} の actionPattern[{i}] の attackData が null です");
                    continue;
                }

                if (!isHeavyByName.TryGetValue(action.attackData.name, out bool expectedHeavy))
                {
                    errors.Add($"⑤ {enemy.name} の actionPattern[{i}] が表に無い攻撃 {action.attackData.name} を参照しています");
                    continue;
                }

                if (action.isHeavy != expectedHeavy)
                    errors.Add($"⑤ {enemy.name} の actionPattern[{i}] ({action.attackData.name}) の isHeavy が {action.isHeavy} です（期待値 {expectedHeavy}）");
            }
        }

        // ⑥
        foreach (var def in Stages)
        {
            string path = $"{StageDir}/{StagePrefix}{def.Key}.asset";
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (stage == null)
            {
                errors.Add($"⑥ {path} が存在しません");
                continue;
            }

            if (stage.sections == null || stage.sections.Length != def.EnemyKeys.Length)
            {
                errors.Add($"⑥ {stage.name} のセクション数が {stage.sections?.Length ?? 0} です（期待値 {def.EnemyKeys.Length}）");
                continue;
            }

            int lastIndex = stage.sections.Length - 1;
            for (int i = 0; i < stage.sections.Length; i++)
            {
                var section = stage.sections[i];
                string expectedName = EnemyPrefix + def.EnemyKeys[i];

                if (section.enemyData == null)
                    errors.Add($"⑥ {stage.name} のセクション{i} の enemyData が null です");
                else if (section.enemyData.name != expectedName)
                    errors.Add($"⑥ {stage.name} のセクション{i} が {section.enemyData.name} です（期待値 {expectedName}）");

                if (section.isBoss != (i == lastIndex))
                    errors.Add($"⑥ {stage.name} のセクション{i} の isBoss が {section.isBoss} です");
            }
        }

        if (errors.Count == 0)
        {
            Debug.Log($"{LogTag} 検証OK: 完了条件④⑤⑥をすべて満たしています");
            return;
        }

        foreach (var e in errors) Debug.LogError($"{LogTag} 検証NG: {e}");
    }

    // ============================================================
    // 型
    // ============================================================

    private readonly struct AttackDef
    {
        public readonly string Key;
        public readonly string AttackName;
        public readonly int Damage;
        public readonly float ChargeTime;
        public readonly bool IsHeavy;

        public AttackDef(string key, string attackName, int damage, float chargeTime, bool isHeavy)
        {
            Key = key;
            AttackName = attackName;
            Damage = damage;
            ChargeTime = chargeTime;
            IsHeavy = isHeavy;
        }
    }

    private readonly struct EnemyDef
    {
        public readonly string Key;
        public readonly string EnemyName;
        public readonly int MaxHp;
        public readonly ElementType WeakElement;
        public readonly float WeakMultiplier;
        public readonly string Pattern;

        public EnemyDef(string key, string enemyName, int maxHp, ElementType weakElement, float weakMultiplier, string pattern)
        {
            Key = key;
            EnemyName = enemyName;
            MaxHp = maxHp;
            WeakElement = weakElement;
            WeakMultiplier = weakMultiplier;
            Pattern = pattern;
        }
    }

    private readonly struct StageDef
    {
        public readonly string Key;
        public readonly int StageNumber;
        public readonly string StageName;
        public readonly string[] EnemyKeys;

        public StageDef(string key, int stageNumber, string stageName, params string[] enemyKeys)
        {
            Key = key;
            StageNumber = stageNumber;
            StageName = stageName;
            EnemyKeys = enemyKeys;
        }
    }

    /// <summary>行動パターン1要素分。パターン文字列を展開した結果</summary>
    private readonly struct ActionStep
    {
        public readonly string AttackKey;
        public readonly float Interval;

        public ActionStep(string attackKey, float interval)
        {
            AttackKey = attackKey;
            Interval = interval;
        }
    }

    /// <summary>実行結果のログ用</summary>
    private class Report
    {
        public readonly List<string> Renamed = new();
        public readonly List<string> Created = new();
        public readonly List<string> Updated = new();

        public void Log()
        {
            Debug.Log(
                $"{LogTag} 完了 — リネーム {Renamed.Count} / 新規 {Created.Count} / 更新 {Updated.Count}\n" +
                Section("リネーム", Renamed) +
                Section("新規作成", Created) +
                Section("更新", Updated));
        }

        private static string Section(string title, List<string> items)
        {
            return items.Count == 0 ? "" : $"--- {title} ---\n{string.Join("\n", items)}\n";
        }
    }
}
