using UnityEngine;

/// <summary>The Laser fires faster (picks compound).</summary>
[CreateAssetMenu(fileName = "LaserFocusEffect", menuName = "BrickBreaker/Effects/Laser Focus")]
public class LaserFocusEffect : PowerUpEffect
{
    [Tooltip("0.2 = 20% faster per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.2f;

    public override void Apply(PowerUpContext context) => PowerUpStats.LaserFireRate *= 1f + percent;

    public override string Describe(int stacks) => $"LASER FIRES {CompoundPercent(percent, stacks)}% FASTER";
}
