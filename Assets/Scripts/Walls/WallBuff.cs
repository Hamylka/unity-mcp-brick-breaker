using UnityEngine;

/// <summary>
/// A temporary buff a wall zone gives to the ball that bounces in it. One asset per buff type, stored in
/// <c>Resources/WallBuffs</c> (the spawner loads every asset there). To add a buff, subclass this, implement
/// <see cref="Apply"/> / <see cref="Remove"/> and create the asset.
/// </summary>
public abstract class WallBuff : ScriptableObject
{
    [Tooltip("Short name shown in the active bonus HUD.")]
    public string label = "BUFF";
    [Tooltip("Zone, ball aura and HUD ring color.")]
    [ColorUsage(false, true)] public Color color = new(1f, 0.8f, 0.2f, 1f);
    [Min(0.5f)] public float duration = 5f;
    [Tooltip("Relative chance of being picked when a zone spawns.")]
    [Min(0f)] public float weight = 1f;

    /// <summary>Starts the effect on the ball.</summary>
    public abstract void Apply(BallController ball);

    /// <summary>Undoes <see cref="Apply"/> (also called if the ball is destroyed early).</summary>
    public abstract void Remove(BallController ball);
}
