/// <summary>
/// プレイヤーが受けたダメージの内容。BattleModel.OnPlayerDamaged で通知する。
/// HPの値変化（PlayerHp）だけでは「何によって」「ガードできたか」が分からず、
/// 被弾フラッシュとGuard成功の演出を出し分けられないため、別途この型で流す。
/// 敵の攻撃と暴発のセルフダメージを1つの型にまとめているのは SpellCastResult と同じ方針で、
/// 購読側が IsMisfire で振り分ければイベントの数が増えない。
/// </summary>
public readonly struct PlayerDamageInfo
{
    /// <summary>実際にHPから引いたダメージ量（ガード軽減後）</summary>
    public readonly int Damage;

    /// <summary>ガード受付中に受けたか。暴発時は常に false</summary>
    public readonly bool WasGuarded;

    /// <summary>敵の大技によるダメージか。暴発時は常に false</summary>
    public readonly bool IsHeavy;

    /// <summary>暴発によるセルフダメージか</summary>
    public readonly bool IsMisfire;

    public PlayerDamageInfo(int damage, bool wasGuarded, bool isHeavy, bool isMisfire)
    {
        Damage = damage;
        WasGuarded = wasGuarded;
        IsHeavy = isHeavy;
        IsMisfire = isMisfire;
    }
}
