using UnityEngine;

/// <summary>
/// 板（Quad）に貼った絵を、板の長さに合わせて横方向に繰り返す部品（3D化ステップ3。環境Prefab用）。
/// 土塀のように「長さがまちまちな板に、同じ絵を引き伸ばさずに貼りたい」ときに使う。
///
/// 繰り返し回数 = 板の幅 ÷ (板の高さ × 絵の縦横比)。絵の縦横比はマテリアルのテクスチャから読むので、
/// 板のScaleを変えるだけで回数が自動で合う。エディタ上でも反映されるようExecuteAlwaysにしている。
///
/// マテリアルを長さごとに分けずに済むよう、繰り返しはMaterialPropertyBlockで板ごとに持たせる。
/// テクスチャは取り込み設定で Wrap Mode U を Repeat にしておくこと（Clampだと端の色が伸びる）。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class TiledSurface : MonoBehaviour
{
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [Tooltip("絵の開始位置のずらし（0〜1）。同じ絵の塀が並んだとき、継ぎ目の位置を揃えないために使う")]
    [Range(0f, 1f)]
    [SerializeField] private float _offsetU;

    private MaterialPropertyBlock _block;

    private void OnEnable() => Apply();

    private void OnValidate() => Apply();

    /// <summary>板の大きさとテクスチャの縦横比から、横方向の繰り返し回数を決めて反映する。</summary>
    private void Apply()
    {
        var renderer = GetComponent<Renderer>();
        var material = renderer.sharedMaterial;
        if (material == null || !material.HasProperty(BaseMapId)) return;

        var texture = material.GetTexture(BaseMapId);
        var scale = transform.lossyScale;
        if (texture == null || Mathf.Approximately(scale.y, 0f)) return;

        float aspect = (float)texture.width / texture.height;
        float repeat = Mathf.Abs(scale.x) / (Mathf.Abs(scale.y) * aspect);

        _block ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(_block);
        _block.SetVector(BaseMapStId, new Vector4(repeat, 1f, _offsetU, 0f));
        renderer.SetPropertyBlock(_block);
    }
}
