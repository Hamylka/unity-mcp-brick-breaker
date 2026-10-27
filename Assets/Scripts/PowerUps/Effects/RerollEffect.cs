using UnityEngine;

/// <summary>Adds free rerolls to every level-up from now on (stacks with the base count).</summary>
[CreateAssetMenu(fileName = "RerollEffect", menuName = "BrickBreaker/Effects/Rerolls")]
public class RerollEffect : PowerUpEffect
{
    [Tooltip("Extra rerolls per level-up granted by each pick.")]
    [SerializeField, Min(1)] private int amount = 2;

    public override void Apply(PowerUpContext context)
    {
        if (context.Manager != null) context.Manager.BonusRerolls += amount;
    }
}
