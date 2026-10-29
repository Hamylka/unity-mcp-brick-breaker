using UnityEngine;

/// <summary>One more orb circling every ball; orbs damage the bricks they touch (see <see cref="SatelliteSystem"/>).</summary>
[CreateAssetMenu(fileName = "SatelliteEffect", menuName = "BrickBreaker/Effects/Satellite")]
public class SatelliteEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    public override void Apply(PowerUpContext context)
    {
        PowerUpStats.SatelliteOrbs++;
        AreaDamage.For(context.Grid);
        SatelliteSystem.Ensure();
    }

    public override string Describe(int stacks) => $"{stacks} ORB{(stacks > 1 ? "S" : "")} HIT BRICKS FOR 25% DMG";
}
