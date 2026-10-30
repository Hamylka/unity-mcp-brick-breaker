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

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context)
    {
        foreach (BallController ball in context.Balls) ball.MultiplyBaseSpeed(1f / (1f + percent));
    }

    // Slowing stops at the ball's speed floor, so the text never promises more than that.
    public override string Describe(int stacks)
    {
        int total = CompoundPercent(percent, stacks);
        int floor = Mathf.RoundToInt((BallController.DefaultMinSpeedFactor - 1f) * 100f);
        return $"BALL SPEED {Mathf.Max(total, floor):+0;-0}%";
    }
}
