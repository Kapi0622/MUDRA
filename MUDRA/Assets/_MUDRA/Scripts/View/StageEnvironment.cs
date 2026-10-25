using UnityEngine;

/// <summary>
/// 3D環境Prefabのルートに付ける部品（3D化ステップ2b）。セクションごとのマーカーを持つ。
/// マーカーは「そのセクションで敵が立つ位置と向き」。StageEnvironmentViewが戦闘一式（リグ）をここへ動かす。
/// マーカーの数はステージのセクション数に揃える。足りない分は最後のマーカーが使われる。
///
/// StageData.environmentPrefabに設定する。未設定のステージは背景画像から自動で廊下が組まれるので、この部品は要らない。
/// </summary>
public class StageEnvironment : MonoBehaviour
{
    [Tooltip("セクション順のマーカー。リグ（カメラ・敵）がこの位置と向きへ移動する")]
    [SerializeField] private Transform[] _sectionAnchors;

    /// <summary>マーカーの数</summary>
    public int AnchorCount => _sectionAnchors != null ? _sectionAnchors.Length : 0;

    /// <summary>index番目のマーカー。範囲は呼び出し側で確かめる</summary>
    public Transform GetAnchor(int index) => _sectionAnchors[index];
}
