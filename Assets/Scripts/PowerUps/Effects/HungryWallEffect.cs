using UnityEngine;

/// <summary>Hungry Wall (Cursed--): new bricks get a bigger HP window (the wall is tougher).</summary>
[CreateAssetMenu(fileName = "HungryWallEffect", menuName = "BrickBreaker/Effects/Hungry Wall")]
public class HungryWallEffect : PowerUpEffect
{
    [Tooltip("0.25 = the HP window of new bricks is 25% bigger, per pick (compounding).")]
    [SerializeField, Range(0.05f, 2f)] private float percent = 0.25f;

    public override void Apply(PowerUpContext context) => PowerUpStats.WallHpFactor *= 1f + percent;

    public override string Describe(int stacks) => $"WALL BRICKS HAVE +{CompoundPercent(percent, stacks)}% HP";
}
