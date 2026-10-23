using System.Collections.Generic;
using UnityEngine;

/// <summary>Everything a power-up effect may need when it is applied.</summary>
public class PowerUpContext
{
    public PaddleController Paddle;
    public BrickGrid Grid;
    public ExperienceSystem Experience;
    public IReadOnlyList<BallController> Balls;
}

/// <summary>
/// Base class for what a power-up does. Create each as a ScriptableObject asset and assign it to a
/// <see cref="PowerUpDefinition"/>. A new kind of power-up only needs a new subclass.
/// </summary>
public abstract class PowerUpEffect : ScriptableObject
{
    public abstract void Apply(PowerUpContext context);
}
