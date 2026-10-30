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

    public override string Describe(int stacks) => $"XP GAINED {Mathf.RoundToInt(percent * stacks * 100f):+0;-0}%";
}
