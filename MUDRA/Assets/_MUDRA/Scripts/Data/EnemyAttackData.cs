using UnityEngine;

/// <summary>
/// 敵の攻撃1種分のテンプレートデータ。
/// 複数のEnemyDataで使い回せる。
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyAttack", menuName = "MUDRA/Enemy Attack Data")]
public class EnemyAttackData : ScriptableObject
{
    [Header("基本情報")]
    public string attackName;

    [Tooltip("基礎ダメージ")]
    public int damage;

    [Tooltip("攻撃予告の演出時間（秒）")]
    public float chargeTime = 1.5f;

    [Header("演出")]
    public GameObject effectPrefab;
    public AudioClip attackSE;
}