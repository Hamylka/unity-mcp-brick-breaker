using UnityEngine;

[CreateAssetMenu(fileName = "PaddleWidthEffect", menuName = "BrickBreaker/Effects/Paddle Width")]
public class PaddleWidthEffect : PowerUpEffect
{
    [Tooltip("0.15 = 15% wider.")]
    [SerializeField, Min(0f)] private float percent = 0.15f;

    public override void Apply(PowerUpContext context)
    {
        if (context.Paddle != null) context.Paddle.MultiplyWidth(1f + percent);
    }

    public override string Describe(int stacks) => $"PADDLE WIDTH +{CompoundPercent(percent, stacks)}%";
}
