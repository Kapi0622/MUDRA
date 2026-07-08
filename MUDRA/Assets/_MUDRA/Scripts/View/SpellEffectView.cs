using UnityEngine;

/// <summary>
/// 術発動時のパーティクルエフェクトを再生するView
/// 「どこに出すか」の判定はこのクラスの責務外。呼び出し側が用意したTransformに対してInstantiateするだけ
/// </summary>
public class SpellEffectView : MonoBehaviour
{
    [SerializeField] private GameObject _effectPrefab;
    [SerializeField] private Transform _spawnPoint;

    // Prefab側のStop Action = Destroy設定に加えて、保険として一定時間後に強制破棄する
    [SerializeField] private float _safetyDestroyDelay = 5f;

    /// <summary>
    /// エフェクトを再生する
    /// </summary>
    public void PlayEffect()
    {
        if (_effectPrefab == null || _spawnPoint == null)
        {
            Debug.LogWarning("[SpellEffectView] effectPrefab または spawnPoint が未設定です");
            return;
        }

        var instance = Instantiate(_effectPrefab, _spawnPoint.position, _spawnPoint.rotation);
        Destroy(instance, _safetyDestroyDelay);
    }
}