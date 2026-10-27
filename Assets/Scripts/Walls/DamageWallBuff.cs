using UnityEngine;

/// <summary>Wall buff: multiplies the ball's damage per hit while it lasts.</summary>
[CreateAssetMenu(menuName = "Brick Breaker/Wall Buff/Damage", fileName = "WallBuff_Damage")]
public class DamageWallBuff : WallBuff
{
    [Min(1f)] public float multiplier = 2f;

    public override void Apply(BallController ball) => ball.DamageMultiplier = multiplier;

    public override void Remove(BallController ball) => ball.DamageMultiplier = 1f;
}
