using UnityEngine;

/// <summary>Falling bonus items fall slower by a percent per pick (compounding).</summary>
[CreateAssetMenu(fileName = "SlowFallEffect", menuName = "BrickBreaker/Effects/Slow Fall")]
public class SlowFallEffect : PowerUpEffect
{
    [Tooltip("0.15 = 15% slower per pick.")]
    [SerializeField, Range(-1f, 0.9f)] private float percent = 0.15f;

    // A negative percent makes them fall faster (Leaden Bonuses, Cursed), never more than MaxBonusFallSpeed.
    public override void Apply(PowerUpContext context) =>
        PowerUpStats.BonusFallSpeed = Mathf.Min(PowerUpStats.MaxBonusFallSpeed, PowerUpStats.BonusFallSpeed * (1f - percent));

    public override string Describe(int stacks)
    {
        float factor = Mathf.Min(PowerUpStats.MaxBonusFallSpeed, Mathf.Pow(1f - percent, stacks));
        int slower = Mathf.RoundToInt((1f - factor) * 100f);
        return slower >= 0 ? $"BONUSES FALL {slower}% SLOWER" : $"BONUSES FALL {-slower}% FASTER";
    }
}
