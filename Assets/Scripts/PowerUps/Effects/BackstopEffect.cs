using UnityEngine;

/// <summary>Each back-wall bounce adds damage to the ball's next brick hit (reset by that hit, at most 6 count).</summary>
[CreateAssetMenu(fileName = "BackstopEffect", menuName = "BrickBreaker/Effects/Backstop")]
public class BackstopEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    [Tooltip("0.4 = +40% damage per back-wall bounce, per pick (the back wall is hit rarely).")]
    [SerializeField, Min(0f)] private float percent = 0.4f;

    public override void Apply(PowerUpContext context) => PowerUpStats.BackstopPercent += percent;

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.BackstopPercent = Mathf.Max(0f, PowerUpStats.BackstopPercent - percent);

    public override string Describe(int stacks) => $"+{Mathf.RoundToInt(percent * stacks * 100f)}% DMG PER BACK BOUNCE";
}
