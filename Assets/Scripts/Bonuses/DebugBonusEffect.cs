using UnityEngine;

/// <summary>Placeholder effect that only logs. Replace with real abilities later.</summary>
[CreateAssetMenu(fileName = "DebugBonusEffect", menuName = "BrickBreaker/Bonus Effects/Debug")]
public class DebugBonusEffect : BonusEffect
{
    public override void Apply(BonusContext context)
    {
        Debug.Log($"Bonus caught: {name} ({context.Balls.Count} ball(s) in play)");
    }
}
