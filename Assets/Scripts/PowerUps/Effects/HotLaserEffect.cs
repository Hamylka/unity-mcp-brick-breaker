using UnityEngine;

/// <summary>Laser bolts deal extra damage (picks add up).</summary>
[CreateAssetMenu(fileName = "HotLaserEffect", menuName = "BrickBreaker/Effects/Hot Laser")]
public class HotLaserEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    [Tooltip("0.25 = +25% damage per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.25f;

    public override void Apply(PowerUpContext context) => PowerUpStats.LaserDamageBonus += percent;
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.LaserDamageBonus = Mathf.Max(0f, PowerUpStats.LaserDamageBonus - percent);

    public override string Describe(int stacks) => $"LASER BOLTS +{Mathf.RoundToInt(percent * stacks * 100f)}% DAMAGE";
}
