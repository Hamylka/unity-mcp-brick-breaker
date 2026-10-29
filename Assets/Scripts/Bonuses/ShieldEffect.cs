using UnityEngine;

/// <summary>
/// Rare protection: raises a barrier behind the paddle that bounces a falling ball back once, then breaks.
/// Only one shield can exist at a time; while it is up this bonus does not drop.
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

    public override bool CanDrop => ShieldBarrier.Current == null;

    public override void Apply(BonusContext context)
    {
        if (ShieldBarrier.Current != null) return; // already protected, one at a time
        ShieldBarrier.Create(barrierMaterial, barrierZ, width, thickness);
        SoundManager.Play(Sfx.ShieldUp);
    }
}
