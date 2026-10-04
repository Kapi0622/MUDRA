using UnityEngine;

/// <summary>
/// 術発動時・敵の攻撃着弾時のパーティクルエフェクトを再生するView
/// 「どのPrefabを出すか」「術者側か敵側か」の判定はこのクラスの責務外。呼び出し側から受け取ってInstantiateするだけ
/// 出現位置は敵側（術の着弾点）とプレイヤー側（回復術・敵の攻撃の着弾点）の2つを持つ（B4）
/// </summary>
public class SpellEffectView : MonoBehaviour
{
    [Tooltip("SpellData.effectPrefabが未設定の術に使う既定のエフェクト")]
    [SerializeField] private GameObject _effectPrefab;

    [Tooltip("敵側の出現位置（術の着弾点）")]
    [SerializeField] private Transform _spawnPoint;

    [Tooltip("プレイヤー側の出現位置（回復術・敵の攻撃の着弾点）")]
    [SerializeField] private Transform _playerSpawnPoint;

    // Prefab側のStop Action = Destroy設定に加えて、保険として一定時間後に強制破棄する
    // DoTの残り火など尾を引くエフェクトがあるため、最も長いエフェクトより長くしておくこと
    [SerializeField] private float _safetyDestroyDelay = 3f;

    /// <summary>
    /// 術のエフェクトを再生する。prefabがnullなら既定のエフェクトを使う。
    /// </summary>
    /// <param name="onCaster">trueなら術者（プレイヤー）側に出す。回復術など自分に掛ける術で使う</param>
    public void PlaySpellEffect(GameObject prefab, bool onCaster)
    {
        Spawn(prefab != null ? prefab : _effectPrefab, onCaster ? _playerSpawnPoint : _spawnPoint);
    }

    /// <summary>
    /// 敵の攻撃の着弾エフェクトをプレイヤー側に再生する。
    /// 術と違い既定のエフェクトは持たない（未設定の攻撃は画面フラッシュと揺れだけで表現する）。
    /// </summary>
    public void PlayEnemyAttackEffect(GameObject prefab)
    {
        if (prefab == null) return;
        Spawn(prefab, _playerSpawnPoint);
    }

    private void Spawn(GameObject prefab, Transform point)
    {
        if (prefab == null || point == null)
        {
            Debug.LogWarning("[SpellEffectView] effectPrefab または出現位置が未設定です");
            return;
        }

        var instance = Instantiate(prefab, point.position, point.rotation);
        Destroy(instance, _safetyDestroyDelay);
    }
}
