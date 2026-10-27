using UnityEngine;

/// <summary>Wall buff: the ball passes through bricks, hitting each one it enters without bouncing.</summary>
[CreateAssetMenu(menuName = "Brick Breaker/Wall Buff/Phase", fileName = "WallBuff_Phase")]
public class PhaseWallBuff : WallBuff
{
    public override void Apply(BallController ball) => ball.Phasing = true;

    public override void Remove(BallController ball) => ball.Phasing = false;
}
