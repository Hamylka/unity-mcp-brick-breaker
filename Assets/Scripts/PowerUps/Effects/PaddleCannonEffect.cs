using UnityEngine;

/// <summary>Paddle bounces fire bolts from lanes across the paddle (see <see cref="PaddleCannon"/>).</summary>
[CreateAssetMenu(fileName = "PaddleCannonEffect", menuName = "BrickBreaker/Effects/Paddle Cannon")]
public class PaddleCannonEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    public override void Apply(PowerUpContext context) => PowerUpStats.CannonPicks++;
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.CannonPicks = Mathf.Max(0, PowerUpStats.CannonPicks - 1);

    public override string Describe(int stacks)
    {
        int lanes = PaddleCannon.LanesFor(stacks);
        int percent = Mathf.RoundToInt(50f * PaddleCannon.DamageFactorFor(stacks)); // bolt damage vs the ball's
        return $"{lanes} BOLT{(lanes > 1 ? "S" : "")} PER BOUNCE, {percent}% DMG EACH";
    }
}
