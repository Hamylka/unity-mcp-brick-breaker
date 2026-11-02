using UnityEngine;

/// <summary>The first brick a ball hits after the paddle takes extra damage (picks add up).</summary>
[CreateAssetMenu(fileName = "LaunchEffect", menuName = "BrickBreaker/Effects/Launch")]
public class LaunchEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    [Tooltip("0.3 = +30% damage per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.3f;

    public override void Apply(PowerUpContext context) => PowerUpStats.LaunchPercent += percent;
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.LaunchPercent = Mathf.Max(0f, PowerUpStats.LaunchPercent - percent);

    public override string Describe(int stacks) => $"+{Mathf.RoundToInt(percent * stacks * 100f)}% DMG ON FIRST HIT AFTER PADDLE";
}
