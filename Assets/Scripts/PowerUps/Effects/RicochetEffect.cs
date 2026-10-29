using UnityEngine;

/// <summary>Each wall bounce adds damage to the ball's next brick hit (reset by that hit).</summary>
[CreateAssetMenu(fileName = "RicochetEffect", menuName = "BrickBreaker/Effects/Ricochet")]
public class RicochetEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    [Tooltip("0.1 = +10% damage per wall bounce, per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.1f;

    public override void Apply(PowerUpContext context) => PowerUpStats.RicochetPercent += percent;

    public override string Describe(int stacks) => $"+{Mathf.RoundToInt(percent * stacks * 100f)}% DMG PER WALL BOUNCE";
}
