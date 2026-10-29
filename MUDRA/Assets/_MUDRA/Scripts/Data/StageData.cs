using System;
using UnityEngine;

/// <summary>
/// ステージ内の1セクション分のデータ。
/// 道中セクション（雑魚）とボスセクションを同一構造で表現する。
/// 雑魚とボスの強さの差はEnemyData側のパラメータで付けるため、
/// このstructは「どの敵か」と「ボスか否か」しか持たない。
/// </summary>
[Serializable]
public struct StageSection
{
    [Tooltip("このセクションに登場する敵")]
    public EnemyData enemyData;

    [Tooltip("ボスセクションかどうか（演出・背景切替の判定に使用。B4で使う）")]
    public bool isBoss;
}

/// <summary>
/// ステージの定義データ。
/// 1ステージは複数のセクション（道中雑魚×N + ボス×1）で構成される。
/// セクションを先頭から順に進行し、全セクション完了でステージクリアとなる。
/// プレイヤーHPはセクション間で引き継ぐ（リソース管理要素）。
/// 進行の制御はSectionProgressManagerが行い、このクラスは定義を持つだけ。
/// </summary>
[CreateAssetMenu(fileName = "NewStage", menuName = "MUDRA/Stage Data")]
public class StageData : ScriptableObject
{
    [Header("基本情報")]
    public string stageName;
    public int stageNumber;

    [Header("セクション構成")]
    [Tooltip("先頭から順に進行する。最後のセクションがボス戦となる想定")]
    public StageSection[] sections;

    [Header("演出")]
    [Tooltip("道中のBGM（BGM_Stage0N_*）。ループで流す（B7）")]
    public AudioClip bgm;

    [Tooltip("ボス戦のBGM。ステージごとに別の曲を設定する（B7）。\n" +
             "未設定ならボス戦でも道中の bgm を流し続ける（チュートリアルの木人戦など）")]
    public AudioClip bossBgm;

    [Tooltip("道中セクションの背景。B4で使用")]
    public Sprite roadBackgroundSprite;

    [Tooltip("ボスセクションの背景。B4で使用")]
    public Sprite bossBackgroundSprite;

    [Tooltip("3D環境のPrefab（ルートにStageEnvironmentを付け、セクション数ぶんのマーカーを置く）。\n" +
             "未設定なら道中/ボスの背景画像から書き割りの廊下を自動で組む（3D化ステップ2b）")]
    public GameObject environmentPrefab;

    [Tooltip("ステージ選択のカードと背景に出す絵（UI改修 ステップ3）。環境の遠景（T_Env_S0N_Backdrop）を想定。\n" +
             "未設定ならカードは無地、背景は共通の絵のまま")]
    public Sprite selectThumbnail;

    /// <summary>
    /// このステージのボス。isBossのセクションを末尾から探し、無ければ末尾の敵で代用する。
    /// StageSelectの弱点表示とステージ開始の演出で使う（B5）
    /// </summary>
    public EnemyData FindBoss()
    {
        if (sections == null || sections.Length == 0) return null;

        for (int i = sections.Length - 1; i >= 0; i--)
        {
            if (sections[i].isBoss) return sections[i].enemyData;
        }
        return sections[^1].enemyData;
    }
}
