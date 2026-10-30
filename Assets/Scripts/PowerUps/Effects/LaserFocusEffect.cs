using UnityEngine;

/// <summary>The Laser fires faster. Picks add up linearly on the base fire rate (+20%, +40%, +60%...).</summary>
[CreateAssetMenu(fileName = "LaserFocusEffect", menuName = "BrickBreaker/Effects/Laser Focus")]
public class LaserFocusEffect : PowerUpEffect
{
    [Tooltip("0.2 = +20% of the base fire rate per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.2f;

    public override void Apply(PowerUpContext context) => PowerUpStats.LaserFireRate += percent;

    public override string Describe(int stacks) => $"LASER FIRES {Mathf.RoundToInt(percent * stacks * 100f)}% FASTER";
}
