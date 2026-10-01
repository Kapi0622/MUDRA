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
    [Tooltip("B7で使用")]
    public AudioClip bgm;

    [Tooltip("道中セクションの背景。B4で使用")]
    public Sprite roadBackgroundSprite;

    [Tooltip("ボスセクションの背景。B4で使用")]
    public Sprite bossBackgroundSprite;
}
