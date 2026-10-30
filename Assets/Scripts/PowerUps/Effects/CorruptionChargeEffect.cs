using UnityEngine;

/// <summary>Gives corruption charges: each one lets the player corrupt a capped card on a level-up (Glitch Core).</summary>
[CreateAssetMenu(fileName = "CorruptionChargeEffect", menuName = "BrickBreaker/Effects/Corruption Charge")]
public class CorruptionChargeEffect : PowerUpEffect
{
    [SerializeField, Min(1)] private int charges = 1;

    public override void Apply(PowerUpContext context) => PowerUpStats.CorruptionCharges += charges;

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) =>
        PowerUpStats.CorruptionCharges = Mathf.Max(0, PowerUpStats.CorruptionCharges - charges);

    public override string Describe(int stacks)
    {
        int left = PowerUpStats.CorruptionCharges;
        return $"+{charges * stacks} CORRUPTION CHARGE{(charges * stacks > 1 ? "S" : "")}, {left} LEFT";
    }
}
