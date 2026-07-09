using UnityEngine;
using MUDRA.Data;

/// <summary>
/// ボス1体分の定義データ。
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "MUDRA/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("基本情報")]
    public string enemyName;
    public Sprite sprite;

    [Header("戦闘パラメータ")]
    public int maxHp;
    public ElementType weakElement;
    [Tooltip("弱点属性ヒット時の倍率")]
    public float weakMultiplier = 1.5f;

    [Header("行動パターン")]
    [Tooltip("順番に実行し、末尾に達したら先頭に戻る")]
    public EnemyAction[] actionPattern;
}