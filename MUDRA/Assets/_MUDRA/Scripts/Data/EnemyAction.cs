using System;
using UnityEngine;

/// <summary>
/// 行動パターン1要素分。
/// EnemyDataのactionPatternに並べて使う。
/// </summary>
[Serializable]
public struct EnemyAction
{
    [Tooltip("使用する攻撃データ")]
    public EnemyAttackData attackData;

    [Tooltip("大技かどうか（防御時の軽減率が異なる）")]
    public bool isHeavy;

    [Tooltip("この行動後の待機時間（秒）")]
    public float intervalAfter;
}