using UnityEngine;

/// <summary>Falling bonus items drift sideways toward the paddle.</summary>
[CreateAssetMenu(fileName = "BonusMagnetEffect", menuName = "BrickBreaker/Effects/Bonus Magnet")]
public class BonusMagnetEffect : PowerUpEffect
{
    public override void Apply(PowerUpContext context) => PowerUpStats.BonusMagnet = true;

    public override string Describe(int stacks) => "BONUSES DRIFT TOWARD THE PADDLE";
}
