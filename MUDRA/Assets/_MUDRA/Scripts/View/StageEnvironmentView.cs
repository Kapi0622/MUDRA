using System.Collections.Generic;
using UnityEngine;
using LitMotion;
using MUDRA.Data;

/// <summary>
/// バトルの3D環境と、セクション間の前進演出を担うView（3D化ステップ2b。B4のBackgroundViewを作り直したもの）。
///
/// 前進は「戦闘一式（リグ）」ごと次のマーカーへ動かして表す。
/// リグにはカメラ（仮想カメラ）・敵・プレイヤー側のエフェクト位置・ボス用の暗転板がまとまっているので、
/// 互いの位置関係を崩さずに丸ごと移動できる。環境（背景の板とマーカー）はワールドに固定したまま。
/// 揺れ（CameraShakeView）は仮想カメラの出力に足されるため、リグの移動とはぶつからない。
///
/// 環境はStageData.environmentPrefabがあればそれを使い、無ければ道中/ボスの背景画像から
/// 「書き割りの廊下」を自動で組む（セクションごとに板を1枚立て、奥行き方向に並べる）。
/// 前進中、カメラは目の前の板を通り抜けて次の部屋の板に着く。板に近づくほど大きく見えるので、
/// B4の「奥へズーム」に近い見え方になる。
///
/// 移動の長さはPresentationTimingData.advanceDuration（Modelの遷移待機に含まれる）。
/// 背景画像が未設定のステージでは既定の背景を使い、ボス戦は暗い赤に沈めて道中と区別する（B4から引き継ぎ）。
/// </summary>
public class StageEnvironmentView : MonoBehaviour
{
    // --- 自動生成の廊下 ---
    private const float SectionSpacing = 20f;       // マーカー同士の奥行き方向の間隔（ワールド単位）
    private const float PlateDepth = 10f;           // マーカー（敵の立ち位置）から背景の板までの奥行き
    private const string PlateSortingLayer = "Background";
    private const string GeneratedRootName = "Environment (Generated)";

    private static readonly Color BossFallbackTint = new Color(0.65f, 0.4f, 0.4f);

    [Tooltip("前進演出の長さ。Modelの遷移待機はここから計算される")]
    [SerializeField] private PresentationTimingData _timing;
    [Tooltip("マーカー位置へ動かす戦闘一式（仮想カメラ・敵・エフェクト位置・暗転板の親）")]
    [SerializeField] private Transform _rig;
    [Tooltip("リグ内の仮想カメラ。敵からカメラまでの距離を、背景の板の大きさの計算に使う")]
    [SerializeField] private Transform _virtualCamera;
    [Tooltip("画角と縦横比を読むカメラ（Main Camera）")]
    [SerializeField] private Camera _camera;
    [Tooltip("自動生成する板のマテリアル。Sprite-Unlit-Default を割り当てる")]
    [SerializeField] private Material _plateMaterial;
    [Tooltip("StageDataの背景が未設定のときに使う既定背景")]
    [SerializeField] private Sprite _fallbackSprite;

    private readonly List<Pose> _anchors = new();
    private GameObject _environment;
    private StageData _builtStage;
    private int _currentIndex = -1;
    private MotionHandle _advanceHandle;

    /// <summary>
    /// ステージの環境を用意する。同じステージで再度呼ばれたら何もしない。
    /// デバッグのステージジャンプでは別のステージで呼ばれるので、前の環境を捨てて作り直す。
    /// </summary>
    public void Build(StageData stage)
    {
        if (stage == _builtStage && _environment != null) return;

        TryCancel(ref _advanceHandle);
        if (_environment != null) Destroy(_environment);
        _anchors.Clear();
        _currentIndex = -1;
        _builtStage = stage;

        _environment = stage.environmentPrefab != null
            ? InstantiatePrefab(stage)
            : GenerateCorridor(stage);
    }

    /// <summary>
    /// リグを指定セクションのマーカーへ即座に置く。既にそのセクションにいれば何もしない
    /// （通常の進行では前進演出で着いているため）。デバッグのセクションジャンプでは前進中でも止めて置き直す。
    /// </summary>
    public void MoveToSection(int index)
    {
        if (index == _currentIndex) return;

        TryCancel(ref _advanceHandle);
        var pose = GetAnchor(index);
        _rig.SetPositionAndRotation(pose.position, pose.rotation);
        _currentIndex = index;
    }

    /// <summary>
    /// 次のセクションへの前進演出を再生する。撃破演出の後に始めたいので開始を遅らせられるようにしている。
    /// ゲーム内時間で進むので、ポーズ中は止まる。
    /// </summary>
    public void PlayAdvance(int toIndex, float delay)
    {
        TryCancel(ref _advanceHandle);

        var from = new Pose(_rig.position, _rig.rotation);
        var to = GetAnchor(toIndex);
        _currentIndex = toIndex;

        _advanceHandle = LMotion.Create(0f, 1f, _timing.advanceDuration)
            .WithDelay(delay)
            .WithEase(Ease.InOutSine)
            .Bind(t => _rig.SetPositionAndRotation(
                Vector3.LerpUnclamped(from.position, to.position, t),
                Quaternion.SlerpUnclamped(from.rotation, to.rotation, t)));
    }

    /// <summary>
    /// セクション番号のマーカーを返す。マーカーが足りなければ最後のものを使う（Buildで警告済み）。
    /// </summary>
    private Pose GetAnchor(int index)
    {
        if (_anchors.Count == 0) return Pose.identity;
        return _anchors[Mathf.Clamp(index, 0, _anchors.Count - 1)];
    }

    private GameObject InstantiatePrefab(StageData stage)
    {
        var instance = Instantiate(stage.environmentPrefab, transform);
        if (!instance.TryGetComponent<StageEnvironment>(out var environment) || environment.AnchorCount == 0)
        {
            Debug.LogError($"[StageEnvironmentView] {stage.stageName} の environmentPrefab に StageEnvironment（マーカー）がありません");
            return instance;
        }

        for (int i = 0; i < environment.AnchorCount; i++)
        {
            var anchor = environment.GetAnchor(i);
            _anchors.Add(new Pose(anchor.position, anchor.rotation));
        }

        int sectionCount = stage.sections != null ? stage.sections.Length : 0;
        if (environment.AnchorCount < sectionCount)
        {
            Debug.LogWarning(
                $"[StageEnvironmentView] {stage.stageName} のマーカーが足りません" +
                $"（マーカー: {environment.AnchorCount} / セクション: {sectionCount}）。足りない分は最後のマーカーを使います");
        }
        return instance;
    }

    /// <summary>
    /// 背景画像から書き割りの廊下を組む。セクションiのマーカーは奥へ i × SectionSpacing、
    /// 板はマーカーのさらに奥 PlateDepth に、カメラから見て画面を隙間なく覆う大きさで立てる。
    /// 板は全て同じSorting Layer・Orderなので、カメラから遠い順に描かれ、手前の板が奥の板を隠す。
    /// </summary>
    private GameObject GenerateCorridor(StageData stage)
    {
        var root = new GameObject(GeneratedRootName);
        root.transform.SetParent(transform, false);

        int sectionCount = stage.sections != null ? stage.sections.Length : 0;
        float plateDistance = PlateDepth - _virtualCamera.localPosition.z;   // カメラから板までの距離

        for (int i = 0; i < sectionCount; i++)
        {
            var anchorPosition = root.transform.TransformPoint(new Vector3(0f, 0f, i * SectionSpacing));
            _anchors.Add(new Pose(anchorPosition, root.transform.rotation));

            bool isBoss = stage.sections[i].isBoss;
            var sprite = isBoss ? stage.bossBackgroundSprite : stage.roadBackgroundSprite;
            CreatePlate(root.transform, i, sprite, isBoss, plateDistance);
        }

        return root;
    }

    private void CreatePlate(Transform parent, int index, Sprite sprite, bool isBoss, float plateDistance)
    {
        var plate = new GameObject($"Plate_{index:00}");
        plate.transform.SetParent(parent, false);
        plate.transform.localPosition = new Vector3(0f, 0f, index * SectionSpacing + PlateDepth);

        var renderer = plate.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : _fallbackSprite;
        renderer.color = sprite == null && isBoss ? BossFallbackTint : Color.white;
        renderer.sharedMaterial = _plateMaterial;
        renderer.sortingLayerName = PlateSortingLayer;

        float scale = CalculateCoverScale(renderer.sprite, plateDistance);
        plate.transform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// 距離distanceの位置で、カメラの表示範囲を隙間なく覆う拡大率（cover fit）を求める。
    /// Perspectiveでは 見える高さ = 2 × 距離 × tan(画角/2)。はみ出した分は上下か左右が切れる。
    /// </summary>
    private float CalculateCoverScale(Sprite sprite, float distance)
    {
        if (sprite == null || _camera == null) return 1f;

        float viewHeight = 2f * distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * _camera.aspect;
        var size = sprite.bounds.size;
        return Mathf.Max(viewWidth / size.x, viewHeight / size.y);
    }

    /// <summary>
    /// MotionHandleは構造体のため、IsActive()で有効性を確認してからCancelする。
    /// </summary>
    private static void TryCancel(ref MotionHandle handle)
    {
        if (handle.IsActive()) handle.Cancel();
    }

    private void OnDestroy()
    {
        TryCancel(ref _advanceHandle);
    }
}
