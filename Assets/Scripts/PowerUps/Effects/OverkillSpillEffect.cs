using UnityEngine;

/// <summary>Damage beyond the HP a brick had left passes to the brick behind it: 50%, 75%, 100% by pick.</summary>
[CreateAssetMenu(fileName = "OverkillSpillEffect", menuName = "BrickBreaker/Effects/Overkill Spill")]
public class OverkillSpillEffect : PowerUpEffect
{
    public override void Apply(PowerUpContext context)
    {
        PowerUpStats.OverkillPercent = PowerUpStats.OverkillPercent <= 0f ? 0.5f : Mathf.Min(1f, PowerUpStats.OverkillPercent + 0.25f);
        AreaDamage.For(context.Grid); // makes sure the geometry helper exists on the grid
    }

    public override string Describe(int stacks) =>
        $"{Mathf.Min(100, 25 + 25 * stacks)}% OF OVERKILL HITS THE BRICK BEHIND";
}
