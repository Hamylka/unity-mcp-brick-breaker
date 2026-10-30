using UnityEngine;

[CreateAssetMenu(fileName = "BallSpeedEffect", menuName = "BrickBreaker/Effects/Ball Speed")]
public class BallSpeedEffect : PowerUpEffect
{
    [Tooltip("0.1 = +10% base speed, -0.1 = 10% slower (Steady Ball).")]
    [SerializeField, Range(-0.9f, 5f)] private float percent = 0.1f;

    public override void Apply(PowerUpContext context)
    {
        foreach (BallController ball in context.Balls) ball.MultiplyBaseSpeed(1f + percent);
    }

    public override string Describe(int stacks) => $"BALL SPEED {CompoundPercent(percent, stacks):+0;-0}%";
}
