using UnityEngine;

/// <summary>The shield absorbs more bounces before it breaks (also adds to a shield already up).</summary>
[CreateAssetMenu(fileName = "ShieldPlusEffect", menuName = "BrickBreaker/Effects/Shield Plus")]
public class ShieldPlusEffect : PowerUpEffect
{
    [SerializeField, Min(1)] private int bounces = 1;

    public override void Apply(PowerUpContext context)
    {
        PowerUpStats.ShieldExtraBounces += bounces;
        if (ShieldBarrier.Current != null) ShieldBarrier.Current.AddBounces(bounces);
    }

    public override string Describe(int stacks) => $"SHIELD ABSORBS {1 + bounces * stacks} BOUNCES";
}
