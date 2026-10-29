using UnityEngine;

[CreateAssetMenu(fileName = "BallSpeedEffect", menuName = "BrickBreaker/Effects/Ball Speed")]
public class BallSpeedEffect : PowerUpEffect
{
    [Tooltip("0.1 = +10% base speed.")]
    [SerializeField, Min(0f)] private float percent = 0.1f;

    public override void Apply(PowerUpContext context)
    {
        foreach (BallController ball in context.Balls) ball.MultiplyBaseSpeed(1f + percent);
    }

    public override string Describe(int stacks) => $"BALL SPEED +{CompoundPercent(percent, stacks)}%";
}
