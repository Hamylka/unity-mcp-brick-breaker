using UnityEngine;

/// <summary>
/// Rare protection: raises a barrier behind the paddle that bounces a falling ball back, using one charge per bounce.
/// Only one barrier exists at a time; each Shield caught while it is up adds a charge (max 3, 5 with Shield Plus).
/// </summary>
[CreateAssetMenu(fileName = "ShieldEffect", menuName = "BrickBreaker/Bonus Effects/Shield")]
public class ShieldEffect : BonusEffect
{
    [Tooltip("Material of the barrier (a glowing neon one).")]
    [SerializeField] private Material barrierMaterial;
    [Tooltip("World Z of the barrier. Behind the paddle at full pull-back (-1.5) and above the screen bottom edge (about -2.4 at 9:16), so it stays visible.")]
    [SerializeField] private float barrierZ = -2.0f;
    [Tooltip("Total width, a bit wider than the playfield so no ball slips around it.")]
    [SerializeField, Min(1f)] private float width = 9f;
    [SerializeField, Min(0.01f)] private float thickness = 0.12f;

    public override bool CanDrop => !PowerUpStats.BrittlePact; // Brittle Pact: no more Shield drops

    public override void Apply(BonusContext context)
    {
        if (ShieldBarrier.Current != null)
        {
            // Caught while a shield is up: one more charge, up to the maximum (3, or 5 with Shield Plus).
            ShieldBarrier.Current.AddCharge();
            SoundManager.Play(Sfx.ShieldUp);
            return;
        }
        ShieldBarrier.Create(barrierMaterial, barrierZ, width, thickness);
        SoundManager.Play(Sfx.ShieldUp);
    }
}
