/// <summary>
/// 複数のViewで共有する、調整対象ではない値。
/// 見た目を見て詰める値（尺・色）はPresentationTimingData / BattlePaletteDataに置き、ここには置かない。
/// </summary>
public static class BattleUiConstants
{
    /// <summary>帯を画面外から入れる・画面外へ出すときの移動量。画面幅（参照解像度1920）より大きく取る</summary>
    public const float OffscreenSlideDistance = 2400f;

    /// <summary>大技を示す語。書式（区切り文字）は表示する場所ごとに付ける</summary>
    public const string HeavyAttackLabel = "大技";
}
