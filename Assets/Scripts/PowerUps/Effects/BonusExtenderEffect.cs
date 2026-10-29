using UnityEngine;

/// <summary>Timed bonuses (Laser, Multi-Ball) last longer. The Shield has no timer.</summary>
[CreateAssetMenu(fileName = "BonusExtenderEffect", menuName = "BrickBreaker/Effects/Bonus Extender")]
public class BonusExtenderEffect : PowerUpEffect
{
    [Tooltip("0.2 = +20% duration per pick (additive).")]
    [SerializeField, Min(0f)] private float percent = 0.2f;

    public override void Apply(PowerUpContext context) => PowerUpStats.BonusDuration += percent;

    public override string Describe(int stacks) => $"BONUS DURATION +{Mathf.RoundToInt(percent * stacks * 100f)}%";
}
