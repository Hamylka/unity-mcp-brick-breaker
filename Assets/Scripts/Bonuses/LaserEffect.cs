using UnityEngine;

/// <summary>
/// Turns the paddle into a laser cannon for a while: it fires a bolt straight ahead from its middle at a steady
/// rhythm. Each bolt damages the first brick it meets by the ball's current Damage stat.
/// Catching another laser refreshes the timer instead of stacking.
/// </summary>
[CreateAssetMenu(fileName = "LaserEffect", menuName = "BrickBreaker/Bonus Effects/Laser")]
public class LaserEffect : BonusEffect
{
    [Tooltip("Seconds the cannon stays active.")]
    [SerializeField, Min(1f)] private float duration = 15f;
    [Tooltip("Seconds between two shots.")]
    [SerializeField, Min(0.1f)] private float fireInterval = 1f;
    [Tooltip("Unlit material for the bolt. It is tinted with Bolt Color.")]
    [SerializeField] private Material boltMaterial;
    [SerializeField] private Color boltColor = new(1f, 0.2f, 0.6f, 1f);
    [SerializeField, Min(0.01f)] private float boltWidth = 0.14f;

    public override void Apply(BonusContext context)
    {
        if (context.Paddle == null) return;

        LaserCannon cannon = context.Paddle.GetComponent<LaserCannon>();
        if (cannon == null) cannon = context.Paddle.gameObject.AddComponent<LaserCannon>();
        cannon.Activate(duration, fireInterval, boltMaterial, boltColor, boltWidth);
    }
}
