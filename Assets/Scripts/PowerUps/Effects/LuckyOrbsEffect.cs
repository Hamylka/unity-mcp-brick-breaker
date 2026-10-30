using UnityEngine;

/// <summary>Every XP orb has a chance to pay double (a bigger gold orb). The total chance is capped.</summary>
[CreateAssetMenu(fileName = "LuckyOrbsEffect", menuName = "BrickBreaker/Effects/Lucky Orbs")]
public class LuckyOrbsEffect : PowerUpEffect
{
    [Tooltip("0.02 = +2% chance per pick that an orb pays double.")]
    [SerializeField, Range(0f, 0.3f)] private float chance = 0.02f;

    public override void Apply(PowerUpContext context) =>
        PowerUpStats.LuckyOrbChance = Mathf.Min(PowerUpStats.MaxLuckyOrbChance, PowerUpStats.LuckyOrbChance + chance);

    // Picks beyond the cap added nothing, so the manager's remaining picks decide the chance: rebuilt from the stacks.
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context)
    {
        int left = context.Manager != null ? Mathf.Max(0, context.Manager.GetStacks(context.Source) - 1) : 0;
        PowerUpStats.LuckyOrbChance = Mathf.Min(PowerUpStats.MaxLuckyOrbChance, chance * left);
    }

    public override string Describe(int stacks) =>
        $"{Mathf.RoundToInt(Mathf.Min(PowerUpStats.MaxLuckyOrbChance, chance * stacks) * 100f)}% OF XP ORBS PAY DOUBLE";
}
