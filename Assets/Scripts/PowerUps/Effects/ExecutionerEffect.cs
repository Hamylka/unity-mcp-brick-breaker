using UnityEngine;

/// <summary>Ball hits deal extra damage to bricks at 25% HP or less (picks add up).</summary>
[CreateAssetMenu(fileName = "ExecutionerEffect", menuName = "BrickBreaker/Effects/Executioner")]
public class ExecutionerEffect : PowerUpEffect
{
    [Tooltip("0.25 = +25% damage per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.25f;

    public override void Apply(PowerUpContext context) => PowerUpStats.ExecutionerBonus += percent;

    public override string Describe(int stacks) => $"+{Mathf.RoundToInt(percent * stacks * 100f)}% DAMAGE AT 25% HP OR LESS";
}
