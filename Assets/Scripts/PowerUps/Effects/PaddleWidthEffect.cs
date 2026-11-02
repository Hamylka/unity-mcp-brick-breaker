using UnityEngine;

/// <summary>Adds a share of the paddle's start width per pick; every card of this kind adds up (Wide Paddle, Withered Paddle).</summary>
[CreateAssetMenu(fileName = "PaddleWidthEffect", menuName = "BrickBreaker/Effects/Paddle Width")]
public class PaddleWidthEffect : PowerUpEffect
{
    [Tooltip("0.2 = +20% of the start width per pick (not compounding).")]
    [SerializeField, Range(-0.5f, 2f)] private float percent = 0.2f;

    public override void Apply(PowerUpContext context)
    {
        if (context.Paddle != null) context.Paddle.AddWidth(percent);
    }
    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context)
    {
        if (context.Paddle != null) context.Paddle.AddWidth(-percent);
    }

    public override string Describe(int stacks) => $"PADDLE WIDTH {Mathf.RoundToInt(percent * stacks * 100f):+0;-0}%";
}
