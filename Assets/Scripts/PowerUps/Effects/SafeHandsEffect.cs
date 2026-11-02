using UnityEngine;

/// <summary>
/// Safe Hands (merges Slow Fall and Catch Zone): bonus items fall slower and the paddle catches them in a wider area.
/// Both numbers compound per pick, like the two cards it replaces.
/// </summary>
[CreateAssetMenu(fileName = "SafeHandsEffect", menuName = "BrickBreaker/Effects/Safe Hands")]
public class SafeHandsEffect : PowerUpEffect
{
    [Tooltip("0.3 = items fall 30% slower per pick.")]
    [SerializeField, Range(0f, 0.9f)] private float fallPercent = 0.3f;
    [Tooltip("0.3 = 30% wider catch area per pick.")]
    [SerializeField, Min(0f)] private float catchPercent = 0.3f;

    public override void Apply(PowerUpContext context)
    {
        PowerUpStats.BonusFallSpeed = Mathf.Min(PowerUpStats.MaxBonusFallSpeed, PowerUpStats.BonusFallSpeed * (1f - fallPercent));
        PowerUpStats.CatchZoneScale *= 1f + catchPercent;
    }

    public override bool CanRemove => true;

    public override void Remove(PowerUpContext context)
    {
        PowerUpStats.BonusFallSpeed = Mathf.Min(PowerUpStats.MaxBonusFallSpeed, PowerUpStats.BonusFallSpeed / (1f - fallPercent));
        PowerUpStats.CatchZoneScale /= 1f + catchPercent;
    }

    public override string Describe(int stacks)
    {
        int slower = Mathf.RoundToInt((1f - Mathf.Pow(1f - fallPercent, stacks)) * 100f);
        return $"BONUSES FALL {slower}% SLOWER, CATCH AREA +{CompoundPercent(catchPercent, stacks)}%";
    }
}
