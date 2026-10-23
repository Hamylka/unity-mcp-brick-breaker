using UnityEngine;

/// <summary>
/// Base class for anything that wants to customise how the ball bounces off it
/// (paddle, walls, bricks, bumpers...). The ball computes the default reflection first,
/// then calls <see cref="OnBallHit"/> so the surface can override direction, speed or trigger effects.
/// Surfaces are found on the collider's GameObject or any of its parents.
/// </summary>
public abstract class BallSurface : MonoBehaviour
{
    /// <summary>Called right after the default reflection has been applied to the ball.</summary>
    public abstract void OnBallHit(BallController ball, Collision collision);
}
