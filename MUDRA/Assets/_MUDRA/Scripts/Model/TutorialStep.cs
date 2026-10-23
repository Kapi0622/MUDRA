/// <summary>
/// チュートリアル（練習バトル）の段階（B6）。上から順に進む。
/// 操作の基本（詠唱 → 発動 → 取り消し）を先に、敵の動きに合わせる操作（ガード・属性）を後に教える。
/// </summary>
public enum TutorialStep
{
    /// <summary>詠唱印を1つ組む</summary>
    Chant,

    /// <summary>印を並べて発動印で術を放つ</summary>
    Release,

    /// <summary>印を組んでから解除印で取り消す</summary>
    Cancel,

    /// <summary>敵の攻撃をガードで受ける。この段階から敵が攻撃してくる</summary>
    Guard,

    /// <summary>敵の弱点属性の術を当てる</summary>
    Element,

    /// <summary>全段階を達成した</summary>
    Completed,
}
