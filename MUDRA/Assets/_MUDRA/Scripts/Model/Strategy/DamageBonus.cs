/// <summary>
/// 術のダメージに乗せる倍率のうち、敵の弱点以外のもの（速度ボーナス・コンボ・連続減衰）をまとめたもの。
/// BattleModel が算出して各 Strategy に渡す。
/// 以前は各 Strategy がコンボ数から倍率を自前で計算していたが、上限（B8）と連続減衰を足すと
/// 同じ式が3か所に散らばるため、算出は BattleModel の1か所に寄せ、Strategy は掛けるだけにした。
/// </summary>
public readonly struct DamageBonus
{
    /// <summary>速度ボーナス倍率。SpellSequenceModel が算出した値</summary>
    public readonly float Speed;

    /// <summary>コンボ倍率（上限適用済み）</summary>
    public readonly float Combo;

    /// <summary>連続減衰の倍率。直前と同じ属性なら1未満、そうでなければ1</summary>
    public readonly float Repeat;

    /// <summary>3つの倍率の積。Strategy はこれを「初撃」に掛ける</summary>
    public float Total => Speed * Combo * Repeat;

    /// <summary>速度ボーナスが乗ったか（「迅速！」の表示用）</summary>
    public bool HasSpeedBonus => Speed > 1.0f;

    /// <summary>連続減衰がかかったか（「連続↓」の表示用）</summary>
    public bool IsRepeated => Repeat < 1.0f;

    public DamageBonus(float speed, float combo, float repeat)
    {
        Speed = speed;
        Combo = combo;
        Repeat = repeat;
    }
}
