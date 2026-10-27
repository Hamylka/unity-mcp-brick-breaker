using UnityEngine;

/// <summary>Wall buff: the ball passes through bricks, hitting each one it enters without bouncing.</summary>
[CreateAssetMenu(menuName = "Brick Breaker/Wall Buff/Phase", fileName = "WallBuff_Phase")]
public class PhaseWallBuff : WallBuff
{
    [Tooltip("Damage factor while phasing (0.5 = half damage; fractions carry over on each brick).")]
    [Range(0.1f, 1f)] public float damageMultiplier = 0.5f;

    public override void Apply(BallController ball)
    {
        ball.Phasing = true;
        ball.DamageMultiplier = damageMultiplier;
        if (ball.TryGetComponent(out BallVisual visual)) visual.PhaseColor = color;
    }

    public override void Remove(BallController ball)
    {
        ball.Phasing = false;
        ball.DamageMultiplier = 1f;
    }
}
