using UnityEngine;

/// <summary>The paddle catches bonus items in a wider area (pickup only, the ball hitbox is unchanged).</summary>
[CreateAssetMenu(fileName = "CatchZoneEffect", menuName = "BrickBreaker/Effects/Catch Zone")]
public class CatchZoneEffect : PowerUpEffect
{
    [Tooltip("0.3 = 30% wider catch area per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.3f;

    public override void Apply(PowerUpContext context) => PowerUpStats.CatchZoneScale *= 1f + percent;

    public override string Describe(int stacks) => $"BONUS CATCH AREA +{CompoundPercent(percent, stacks)}%";
}
