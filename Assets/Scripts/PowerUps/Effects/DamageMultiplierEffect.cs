using UnityEngine;

/// <summary>
/// Multiplies the ball's permanent damage by a percent on every pick, at once (picks compound: +10% three times
/// is x1.33). Rounded up in <see cref="BallController.Damage"/> so damage stays whole.
/// </summary>
[CreateAssetMenu(fileName = "DamageMultiplierEffect", menuName = "BrickBreaker/Effects/Damage Multiplier")]
public class DamageMultiplierEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    [Tooltip("0.1 = +10% total ball damage per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.1f;

    public override void Apply(PowerUpContext context)
    {
        foreach (BallController ball in context.Balls) ball.MultiplyDamage(1f + percent);
    }

    public override string Describe(int stacks) =>
        $"+{Mathf.RoundToInt(percent * 100f)}% DAMAGE, TOTAL X{Mathf.Pow(1f + percent, stacks):0.0#}";
}
