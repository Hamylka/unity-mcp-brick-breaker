using UnityEngine;

/// <summary>Every few ball hits repeat once for free on the same brick (interval in <see cref="PowerUpStats.EchoInterval"/>).</summary>
[CreateAssetMenu(fileName = "EchoEffect", menuName = "BrickBreaker/Effects/Echo")]
public class EchoEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    public override void Apply(PowerUpContext context) => PowerUpStats.EchoPicks++;

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.EchoPicks = Mathf.Max(0, PowerUpStats.EchoPicks - 1);

    public override string Describe(int stacks)
    {
        return $"EVERY {PowerUpStats.EchoInterval(stacks)} HITS REPEAT {stacks}X FREE";
    }
}
