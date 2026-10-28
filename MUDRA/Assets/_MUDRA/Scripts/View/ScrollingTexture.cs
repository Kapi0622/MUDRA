using UnityEngine;

/// <summary>
/// 板や床に貼った絵を時間で流す部品（Stage02〜04の環境Prefab用）。水面・煙・光の筋の揺らぎに使う。
///
/// 絵の位置（_BaseMap_STのオフセット）だけを動かし、繰り返し回数は元の値を保つ。
/// TiledSurfaceと同じ板に付けても、TiledSurfaceが決めた繰り返しを壊さない（Startの時点の値を基準にする）。
/// ゲーム内時間（Time.time）で流すので、ポーズ・ヒットストップ中は止まる。
/// テクスチャは流す方向の Wrap Mode を Repeat にしておくこと。
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ScrollingTexture : MonoBehaviour
{
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [Tooltip("1秒あたりに流れる量（絵1枚分 = 1）。xが横、yが縦")]
    [SerializeField] private Vector2 _speed = new(0f, 0.05f);

    private Renderer _renderer;
    private MaterialPropertyBlock _block;
    private Vector4 _baseSt;

    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        _block = new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(_block);

        // TiledSurfaceが先に繰り返しを書いていればそれを、無ければマテリアルの値を基準にする
        var material = _renderer.sharedMaterial;
        if (_block.HasVector(BaseMapStId))
        {
            _baseSt = _block.GetVector(BaseMapStId);
        }
        else if (material != null && material.HasProperty(BaseMapId))
        {
            var scale = material.GetTextureScale(BaseMapId);
            var offset = material.GetTextureOffset(BaseMapId);
            _baseSt = new Vector4(scale.x, scale.y, offset.x, offset.y);
        }
        else
        {
            _baseSt = new Vector4(1f, 1f, 0f, 0f);
        }
    }

    private void Update()
    {
        // 長時間たっても小数の精度が落ちないよう、0〜1に丸める
        float x = Mathf.Repeat(_baseSt.z + _speed.x * Time.time, 1f);
        float y = Mathf.Repeat(_baseSt.w + _speed.y * Time.time, 1f);

        _renderer.GetPropertyBlock(_block);
        _block.SetVector(BaseMapStId, new Vector4(_baseSt.x, _baseSt.y, x, y));
        _renderer.SetPropertyBlock(_block);
    }
}
