using UnityEngine;

/// <summary>
/// Laser Focus: each pick adds a parallel bolt lane to the Laser (2 lanes at pick 1, 3 at pick 2...), spread across
/// the paddle width. Every lane deals the full laser damage. The fire rate belongs to <see cref="LaserFocusEffect"/> (Laser Frenzy).
/// </summary>
[CreateAssetMenu(fileName = "LaserLanesEffect", menuName = "BrickBreaker/Effects/Laser Lanes")]
public class LaserLanesEffect : PowerUpEffect
{
    public override void Apply(PowerUpContext context) => PowerUpStats.LaserLanes++;

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.LaserLanes = Mathf.Max(1, PowerUpStats.LaserLanes - 1);

    public override string Describe(int stacks) => $"LASER FIRES {stacks + 1} PARALLEL BOLTS";
}
