using UnityEngine;

/// <summary>
/// Wall buff zones come sooner (3% on the first pick, diminishing, never under 45% of the wait) and are 4% longer
/// per pick. The numbers live in <see cref="PowerUpStats"/>.
/// </summary>
[CreateAssetMenu(fileName = "ZoneBeaconEffect", menuName = "BrickBreaker/Effects/Zone Beacon")]
public class ZoneBeaconEffect : PowerUpEffect
{
    public override void Apply(PowerUpContext context) => PowerUpStats.ZoneBeaconPicks++;

    public override string Describe(int stacks) =>
        $"ZONES {Mathf.RoundToInt((1f - PowerUpStats.ZoneIntervalFactorFor(stacks)) * 100f)}% SOONER, " +
        $"{Mathf.RoundToInt((PowerUpStats.ZoneLengthFactorFor(stacks) - 1f) * 100f)}% LONGER";
}
