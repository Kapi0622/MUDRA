using MUDRA.Data;

/// <summary>
/// ダメージ計算のStrategyインターフェース。
/// 弱点倍率は敵と術の組で決まるので各Strategyが見る。それ以外の倍率は <see cref="DamageBonus"/> で受け取る。
/// </summary>
public interface IDamageCalculator
{
    DamageResult Calculate(SpellData spellData, EnemyData enemyData, DamageBonus bonus);
}