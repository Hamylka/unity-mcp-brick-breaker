using UnityEngine;

[CreateAssetMenu(fileName = "PaddleWidthEffect", menuName = "BrickBreaker/Effects/Paddle Width")]
public class PaddleWidthEffect : PowerUpEffect
{
    [Tooltip("0.15 = 15% wider.")]
    [SerializeField, Range(-0.5f, 2f)] private float percent = 0.15f;

    public override void Apply(PowerUpContext context)
    {
        if (context.Paddle != null) context.Paddle.MultiplyWidth(1f + percent);
    }
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context)
    {
        if (context.Paddle != null) context.Paddle.MultiplyWidth(1f / (1f + percent));
    }

    public override string Describe(int stacks) => $"PADDLE WIDTH {CompoundPercent(percent, stacks):+0;-0}%";
}
