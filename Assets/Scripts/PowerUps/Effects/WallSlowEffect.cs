using UnityEngine;

[CreateAssetMenu(fileName = "WallSlowEffect", menuName = "BrickBreaker/Effects/Wall Slow")]
public class WallSlowEffect : PowerUpEffect
{
    [Tooltip("0.25 = the wall waits 25% longer between steps.")]
    [SerializeField, Range(-0.5f, 5f)] private float percent = 0.25f;

    public override void Apply(PowerUpContext context)
    {
        if (context.Grid != null) context.Grid.MultiplyStepInterval(1f + percent);
    }

    // A negative percent makes the wall step sooner (Rushing Wall, Cursed), floored by BrickGrid at MinWallStepFactor.
    public override string Describe(int stacks)
    {
        int total = Mathf.Max(CompoundPercent(percent, stacks), Mathf.RoundToInt((PowerUpStats.MinWallStepFactor - 1f) * 100f));
        return total >= 0 ? $"WALL STEPS +{total}% SLOWER" : $"WALL STEPS {-total}% FASTER";
    }
}
