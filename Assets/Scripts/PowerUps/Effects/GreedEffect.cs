using UnityEngine;

/// <summary>More bonus drops (capped by <see cref="BonusDropper"/>) and longer timed bonuses.</summary>
[CreateAssetMenu(fileName = "GreedEffect", menuName = "BrickBreaker/Effects/Greed")]
public class GreedEffect : PowerUpEffect
{
    [Tooltip("Added to the bonus drop chance per pick (0.01 = +1%).")]
    [SerializeField, Min(0f)] private float dropChance = 0.01f;
    [Tooltip("Added to timed bonus durations per pick (0.1 = +10%, additive with Bonus Extender).")]
    [SerializeField, Min(0f)] private float duration = 0.1f;

    public override void Apply(PowerUpContext context)
    {
        PowerUpStats.BonusDropBonus += dropChance;
        PowerUpStats.BonusDuration += duration;
    }

    public override string Describe(int stacks) =>
        $"+{Mathf.RoundToInt(dropChance * stacks * 100f)}% DROPS, +{Mathf.RoundToInt(duration * stacks * 100f)}% DURATION";
}
