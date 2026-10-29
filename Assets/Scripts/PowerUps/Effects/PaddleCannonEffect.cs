using UnityEngine;

/// <summary>Paddle bounces fire bolts from lanes across the paddle (see <see cref="PaddleCannon"/>).</summary>
[CreateAssetMenu(fileName = "PaddleCannonEffect", menuName = "BrickBreaker/Effects/Paddle Cannon")]
public class PaddleCannonEffect : PowerUpEffect
{
    public override void Apply(PowerUpContext context) => PowerUpStats.CannonPicks++;

    public override string Describe(int stacks)
    {
        int lanes = PaddleCannon.LanesFor(stacks);
        int bonus = Mathf.RoundToInt((PaddleCannon.DamageFactorFor(stacks) - 1f) * 100f);
        return bonus > 0 ? $"{lanes} BOLTS PER BOUNCE, +{bonus}% DMG" : $"{lanes} BOLT{(lanes > 1 ? "S" : "")} PER BOUNCE, 50% DMG";
    }
}
