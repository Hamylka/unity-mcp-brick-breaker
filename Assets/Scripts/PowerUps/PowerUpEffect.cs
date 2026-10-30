using System.Collections.Generic;
using UnityEngine;

/// <summary>Everything a power-up effect may need when it is applied.</summary>
public class PowerUpContext
{
    public PowerUpManager Manager;
    /// <summary>The card whose effect is being applied or removed.</summary>
    public PowerUpDefinition Source;
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

    /// <summary>
    /// True when <see cref="Remove"/> can take one level back. Only such unlimited cards can be corrupted (the cost
    /// of corrupting an unlimited card is one owned level of it).
    /// </summary>
    public virtual bool CanRemove => false;

    /// <summary>Undoes one <see cref="Apply"/> (one level lost to a corruption). Only called when <see cref="CanRemove"/>.</summary>
    public virtual void Remove(PowerUpContext context) { }

    /// <summary>True for cards that make the player kill faster. Every card offer contains at least one when possible.</summary>
    public virtual bool IsOffensive => false;

    /// <summary>
    /// Short text of what the power-up does in total after this many picks (pause screen), e.g. "+3 DAMAGE PER HIT".
    /// Null falls back to the definition's description.
    /// </summary>
    public virtual string Describe(int stacks) => null;

    /// <summary>Total bonus, in whole percent, of a multiplier applied once per pick (0.1 three times = +33%).</summary>
    protected static int CompoundPercent(float percent, int stacks) =>
        Mathf.RoundToInt((Mathf.Pow(1f + percent, stacks) - 1f) * 100f);
}
