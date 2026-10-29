using UnityEngine;

/// <summary>
/// Raises the base ball damage gained on every following level-up (stacks with the passive +1 and with itself:
/// two picks of +1 mean +2 on top of the passive each level-up). Nothing changes on the pick itself.
/// </summary>
[CreateAssetMenu(fileName = "DamagePerLevelEffect", menuName = "BrickBreaker/Effects/Damage Per Level")]
public class DamagePerLevelEffect : PowerUpEffect
{
    [Tooltip("Base damage added on every level-up after this pick.")]
    [SerializeField, Min(1)] private int amount = 1;

    public override void Apply(PowerUpContext context)
    {
        if (context.Manager != null) context.Manager.BonusDamagePerLevel += amount;
    }

    public override string Describe(int stacks) => $"+{amount * stacks} DAMAGE PER LEVEL-UP";
}
