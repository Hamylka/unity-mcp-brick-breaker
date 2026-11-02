using UnityEngine;

/// <summary>Every damage source deals extra damage to bricks at 40% HP or less (picks add up).</summary>
[CreateAssetMenu(fileName = "ExecutionerEffect", menuName = "BrickBreaker/Effects/Executioner")]
public class ExecutionerEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    [Tooltip("0.25 = +25% damage per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.25f;

    public override void Apply(PowerUpContext context) => PowerUpStats.ExecutionerBonus += percent;
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.ExecutionerBonus = Mathf.Max(0f, PowerUpStats.ExecutionerBonus - percent);

    public override string Describe(int stacks) => $"+{Mathf.RoundToInt(percent * stacks * 100f)}% DAMAGE AT 40% HP OR LESS";
}
