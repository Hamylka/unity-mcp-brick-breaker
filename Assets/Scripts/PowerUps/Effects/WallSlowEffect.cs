using UnityEngine;

[CreateAssetMenu(fileName = "WallSlowEffect", menuName = "BrickBreaker/Effects/Wall Slow")]
public class WallSlowEffect : PowerUpEffect
{
    [Tooltip("0.25 = the wall waits 25% longer between steps.")]
    [SerializeField, Min(0f)] private float percent = 0.25f;

    public override void Apply(PowerUpContext context)
    {
        if (context.Grid != null) context.Grid.MultiplyStepInterval(1f + percent);
    }
}
