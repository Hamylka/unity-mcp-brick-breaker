using UnityEngine;

/// <summary>The paddle follows the finger faster (smoothing time shrinks).</summary>
[CreateAssetMenu(fileName = "PaddleSpeedEffect", menuName = "BrickBreaker/Effects/Paddle Speed")]
public class PaddleSpeedEffect : PowerUpEffect
{
    [Tooltip("0.15 = 15% faster per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.15f;

    public override void Apply(PowerUpContext context)
    {
        if (context.Paddle != null) context.Paddle.MultiplyFollowSpeed(1f + percent);
    }
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context)
    {
        if (context.Paddle != null) context.Paddle.MultiplyFollowSpeed(1f / (1f + percent));
    }

    public override string Describe(int stacks) => $"PADDLE FOLLOWS {CompoundPercent(percent, stacks)}% FASTER";
}
