using UnityEngine;

/// <summary>
/// Every ball gets smaller by a share of its start size per pick (linear: -4%, -8%...). The card is capped at 10 stacks,
/// so the ball never goes under <see cref="PowerUpStats.MinBallSize"/>. A smaller ball slips into gaps and hits more bricks.
/// </summary>
[CreateAssetMenu(fileName = "MarbleEffect", menuName = "BrickBreaker/Effects/Marble")]
public class MarbleEffect : PowerUpEffect
{
    [Tooltip("0.04 = 4% of the start size per pick.")]
    [SerializeField, Range(0f, 0.1f)] private float percent = 0.04f;

    public override void Apply(PowerUpContext context) =>
        PowerUpStats.BallSize = Mathf.Max(PowerUpStats.MinBallSize, PowerUpStats.BallSize - percent);

    public override string Describe(int stacks) =>
        $"BALL SIZE -{Mathf.RoundToInt(Mathf.Min(1f - PowerUpStats.MinBallSize, percent * stacks) * 100f)}%";
}
