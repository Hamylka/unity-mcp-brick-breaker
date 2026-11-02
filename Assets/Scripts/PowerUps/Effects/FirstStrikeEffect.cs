using UnityEngine;

/// <summary>Ball hits deal extra damage to bricks still at full HP (picks add up).</summary>
[CreateAssetMenu(fileName = "FirstStrikeEffect", menuName = "BrickBreaker/Effects/First Strike")]
public class FirstStrikeEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    [Tooltip("0.15 = +15% damage per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.15f;

    public override void Apply(PowerUpContext context) => PowerUpStats.FirstStrikePercent += percent;
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.FirstStrikePercent = Mathf.Max(0f, PowerUpStats.FirstStrikePercent - percent);

    public override string Describe(int stacks) => $"+{Mathf.RoundToInt(percent * stacks * 100f)}% BALL DMG ON FULL HP BRICKS";
}
