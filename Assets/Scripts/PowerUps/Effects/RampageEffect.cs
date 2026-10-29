using UnityEngine;

/// <summary>Each brick a ball hits since it last touched the paddle adds damage to its next hit (reset on the paddle).</summary>
[CreateAssetMenu(fileName = "RampageEffect", menuName = "BrickBreaker/Effects/Rampage")]
public class RampageEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    [Tooltip("0.02 = +2% damage per brick hit since the paddle, per pick.")]
    [SerializeField, Min(0f)] private float percent = 0.02f;

    public override void Apply(PowerUpContext context) => PowerUpStats.RampagePercent += percent;

    public override string Describe(int stacks) => $"+{Mathf.RoundToInt(percent * stacks * 100f)}% DMG FOR EACH BRICK HIT, RESETS ON PADDLE";
}
