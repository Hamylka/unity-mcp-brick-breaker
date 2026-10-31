using UnityEngine;

/// <summary>Each side-wall bounce adds damage to the ball's next brick hit (reset by that hit, at most 6 count).</summary>
[CreateAssetMenu(fileName = "RicochetEffect", menuName = "BrickBreaker/Effects/Ricochet")]
public class RicochetEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    [Tooltip("0.2 = +20% damage per side-wall bounce, per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.2f;

    public override void Apply(PowerUpContext context) => PowerUpStats.RicochetPercent += percent;

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.RicochetPercent = Mathf.Max(0f, PowerUpStats.RicochetPercent - percent);

    public override string Describe(int stacks) => $"+{Mathf.RoundToInt(percent * stacks * 100f)}% DMG PER SIDE BOUNCE";
}
