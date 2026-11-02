using UnityEngine;

/// <summary>
/// Wall buff zones come sooner (8% per pick, never under 40% of the wait, 8 s of 20) and are 10% longer
/// per pick. The numbers live in <see cref="PowerUpStats"/>.
/// </summary>
[CreateAssetMenu(fileName = "ZoneBeaconEffect", menuName = "BrickBreaker/Effects/Zone Beacon")]
public class ZoneBeaconEffect : PowerUpEffect
{
    public override void Apply(PowerUpContext context) => PowerUpStats.ZoneBeaconPicks++;

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.ZoneBeaconPicks = Mathf.Max(0, PowerUpStats.ZoneBeaconPicks - 1);

    public override string Describe(int stacks) =>
        $"ZONES {Mathf.RoundToInt((1f - PowerUpStats.ZoneIntervalFactorFor(stacks)) * 100f)}% SOONER, " +
        $"{Mathf.RoundToInt((PowerUpStats.ZoneLengthFactorFor(stacks) - 1f) * 100f)}% LONGER";
}
