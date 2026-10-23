using System.Collections.Generic;
using UnityEngine;

/// <summary>Everything a bonus effect may need when it is caught.</summary>
public readonly struct BonusContext
{
    public readonly PaddleController Paddle;
    public readonly IReadOnlyList<BallController> Balls;

    public BonusContext(PaddleController paddle, IReadOnlyList<BallController> balls)
    {
        Paddle = paddle;
        Balls = balls;
    }
}

/// <summary>
/// Base class for bonus abilities (laser, multi-ball, wider paddle...).
/// Create each ability as a ScriptableObject asset and assign it to a <see cref="BonusDefinition"/>.
/// </summary>
public abstract class BonusEffect : ScriptableObject
{
    public abstract void Apply(BonusContext context);
}
