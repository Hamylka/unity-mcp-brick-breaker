using UnityEngine;

/// <summary>
/// Bonus Extender: catching a bonus extends every <i>other</i> timed bonus still running (Laser, Multi-Ball extras)
/// by a share of its full duration per pick. The work is done in <see cref="BonusDropper"/> on pickup; the Shield has no timer.
/// </summary>
[CreateAssetMenu(fileName = "BonusExtenderEffect", menuName = "BrickBreaker/Effects/Bonus Extender")]
public class BonusExtenderEffect : PowerUpEffect
{
    [Tooltip("0.1 = +10% of a running bonus's full duration per pick, when another bonus is caught.")]
    [SerializeField, Min(0f)] private float percent = 0.1f;

    public override void Apply(PowerUpContext context) => PowerUpStats.BonusExtendFraction += percent;
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.BonusExtendFraction = Mathf.Max(0f, PowerUpStats.BonusExtendFraction - percent);

    public override string Describe(int stacks) => $"CATCHING A BONUS EXTENDS THE OTHERS +{Mathf.RoundToInt(percent * stacks * 100f)}%";
}
