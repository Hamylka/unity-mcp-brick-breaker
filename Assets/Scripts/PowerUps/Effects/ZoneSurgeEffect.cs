using UnityEngine;

/// <summary>
/// Zone Surge (merges Buff Lasting and Zone Beacon): wall buffs last longer, and buff zones come sooner and
/// are longer. It drives the same run stats as the two old cards, so the numbers stay in <see cref="PowerUpStats"/>
/// (zones: 8% sooner per pick, floor 8 s of 20, and 10% longer, max x2).
/// </summary>
[CreateAssetMenu(fileName = "ZoneSurgeEffect", menuName = "BrickBreaker/Effects/Zone Surge")]
public class ZoneSurgeEffect : PowerUpEffect
{
    [Tooltip("0.25 = +25% of the buff's base duration per pick (additive).")]
    [SerializeField, Min(0f)] private float buffPercent = 0.25f;

    public override void Apply(PowerUpContext context)
    {
        PowerUpStats.WallBuffDuration += buffPercent;
        PowerUpStats.ZoneBeaconPicks++;
    }

    public override bool CanRemove => true;

    public override void Remove(PowerUpContext context)
    {
        PowerUpStats.WallBuffDuration = Mathf.Max(0f, PowerUpStats.WallBuffDuration - buffPercent);
        PowerUpStats.ZoneBeaconPicks = Mathf.Max(0, PowerUpStats.ZoneBeaconPicks - 1);
    }

    public override string Describe(int stacks) =>
        $"BUFFS +{Mathf.RoundToInt(buffPercent * stacks * 100f)}% LONGER, " +
        $"ZONES {Mathf.RoundToInt((1f - PowerUpStats.ZoneIntervalFactorFor(stacks)) * 100f)}% SOONER, " +
        $"{Mathf.RoundToInt((PowerUpStats.ZoneLengthFactorFor(stacks) - 1f) * 100f)}% LONGER";
}
