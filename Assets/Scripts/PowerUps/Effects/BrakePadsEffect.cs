using UnityEngine;

/// <summary>
/// The wall waits a little longer between steps. The gain shrinks with every pick (+2%, then less each time,
/// never more than +50% in total): the numbers live in <see cref="PowerUpStats.WallStepFactorFor"/>.
/// </summary>
[CreateAssetMenu(fileName = "BrakePadsEffect", menuName = "BrickBreaker/Effects/Brake Pads")]
public class BrakePadsEffect : PowerUpEffect
{
    public override void Apply(PowerUpContext context) => PowerUpStats.BrakePicks++;

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.BrakePicks = Mathf.Max(0, PowerUpStats.BrakePicks - 1);

    public override string Describe(int stacks) =>
        $"WALL STEPS +{Mathf.RoundToInt((PowerUpStats.WallStepFactorFor(stacks) - 1f) * 100f)}% SLOWER";
}
