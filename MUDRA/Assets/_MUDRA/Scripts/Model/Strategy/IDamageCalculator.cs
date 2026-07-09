using MUDRA.Data;

/// <summary>
/// ダメージ計算のStrategyインターフェース。
/// </summary>
public interface IDamageCalculator
{
    DamageResult Calculate(SpellData spellData, EnemyData enemyData, float speedBonus, int comboCount);
}