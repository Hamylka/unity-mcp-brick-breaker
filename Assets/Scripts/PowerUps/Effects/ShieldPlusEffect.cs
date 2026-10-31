using UnityEngine;
using UnityEngine.Serialization;

/// <summary>The shield can hold more charges (3 by default, +2 with this card). It gives no charge by itself.</summary>
[CreateAssetMenu(fileName = "ShieldPlusEffect", menuName = "BrickBreaker/Effects/Shield Plus")]
public class ShieldPlusEffect : PowerUpEffect
{
    [Tooltip("Charges added to the shield's maximum, per pick.")]
    [FormerlySerializedAs("bounces")]
    [SerializeField, Min(1)] private int extraCharges = 2;

    public override void Apply(PowerUpContext context) => PowerUpStats.ShieldExtraCharges += extraCharges;
    public override bool CanRemove => true;
    // A shield already up keeps the charges it holds; the new maximum only limits the next ones.
    public override void Remove(PowerUpContext context) => PowerUpStats.ShieldExtraCharges = Mathf.Max(0, PowerUpStats.ShieldExtraCharges - extraCharges);

    public override string Describe(int stacks) => $"SHIELD HOLDS UP TO {PowerUpStats.BaseShieldCharges + extraCharges * stacks} CHARGES";
}
