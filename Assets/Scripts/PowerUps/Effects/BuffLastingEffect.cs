using UnityEngine;

/// <summary>Wall buffs (2x damage, Phase) last longer. Additive per pick, no cap: the zone timer limits the uptime.</summary>
[CreateAssetMenu(fileName = "BuffLastingEffect", menuName = "BrickBreaker/Effects/Buff Lasting")]
public class BuffLastingEffect : PowerUpEffect
{
    [Tooltip("0.25 = +25% of the buff's base duration per pick (additive).")]
    [SerializeField, Min(0f)] private float percent = 0.25f;

    public override void Apply(PowerUpContext context) => PowerUpStats.WallBuffDuration += percent;

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.WallBuffDuration = Mathf.Max(0f, PowerUpStats.WallBuffDuration - percent);

    public override string Describe(int stacks) => $"WALL BUFFS LAST +{Mathf.RoundToInt(percent * stacks * 100f)}% LONGER";
}
