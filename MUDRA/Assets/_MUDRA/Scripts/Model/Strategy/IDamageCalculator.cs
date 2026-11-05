using MUDRA.Data;

/// <summary>
/// ダメージ計算のStrategyインターフェース。
/// 相性倍率（ElementAffinityとBalanceDataで決まる）と、それ以外の倍率（<see cref="DamageBonus"/>）を受け取って掛けるだけで、
/// 受ける側が誰かは知らない（B8.3。対戦で受ける側がプレイヤーになっても同じStrategyを使えるように）。
/// 関係（Relation）は呼び出し側のBattleModelが結果に埋める。
/// </summary>
public interface IDamageCalculator
{
    DamageResult Calculate(SpellData spellData, float affinity, DamageBonus bonus);
}