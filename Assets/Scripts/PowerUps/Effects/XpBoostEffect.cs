using UnityEngine;

[CreateAssetMenu(fileName = "XpBoostEffect", menuName = "BrickBreaker/Effects/XP Boost")]
public class XpBoostEffect : PowerUpEffect
{
    [Tooltip("0.2 = +20% XP from bricks.")]
    [SerializeField, Range(-0.5f, 2f)] private float percent = 0.2f;

    public override void Apply(PowerUpContext context)
    {
        if (context.Experience != null) context.Experience.AddXpMultiplier(percent);
    }
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context)
    {
        if (context.Experience != null) context.Experience.AddXpMultiplier(-percent);
    }

    // A negative percent lowers XP (Dim Orbs, Cursed); ExperienceSystem never goes under MinXpMultiplier.
    public override string Describe(int stacks)
    {
        float total = Mathf.Max(PowerUpStats.MinXpMultiplier - 1f, percent * stacks);
        return $"XP GAINED {Mathf.RoundToInt(total * 100f):+0;-0}%";
    }
}
