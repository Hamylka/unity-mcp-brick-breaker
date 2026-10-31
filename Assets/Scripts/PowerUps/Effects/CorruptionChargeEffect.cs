using UnityEngine;

/// <summary>Gives corruption charges: each one lets the player corrupt a capped card on a level-up (Glitch Core).</summary>
[CreateAssetMenu(fileName = "CorruptionChargeEffect", menuName = "BrickBreaker/Effects/Corruption Charge")]
public class CorruptionChargeEffect : PowerUpEffect
{
    [SerializeField, Min(1)] private int charges = 1;

    // The charge is held back until the card screen has faded out (PowerUpManager releases it), so the CORRUPT button
    // does not show up while the screen closes after picking Glitch Core.
    public override void Apply(PowerUpContext context) => PowerUpStats.PendingCorruptionCharges += charges;

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context)
    {
        // Take back a charge still waiting first, then a held one.
        int fromPending = Mathf.Min(PowerUpStats.PendingCorruptionCharges, charges);
        PowerUpStats.PendingCorruptionCharges -= fromPending;
        PowerUpStats.CorruptionCharges = Mathf.Max(0, PowerUpStats.CorruptionCharges - (charges - fromPending));
    }

    public override string Describe(int stacks)
    {
        int left = PowerUpStats.CorruptionCharges + PowerUpStats.PendingCorruptionCharges;
        return $"+{charges * stacks} CORRUPTION CHARGE{(charges * stacks > 1 ? "S" : "")}, {left} LEFT";
    }
}
