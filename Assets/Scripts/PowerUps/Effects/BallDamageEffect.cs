using UnityEngine;

[CreateAssetMenu(fileName = "BallDamageEffect", menuName = "BrickBreaker/Effects/Ball Damage")]
public class BallDamageEffect : PowerUpEffect
{
    [SerializeField, Min(1)] private int amount = 1;

    public override void Apply(PowerUpContext context)
    {
        foreach (BallController ball in context.Balls) ball.AddDamage(amount);
    }

    public override string Describe(int stacks) => $"+{amount * stacks} DAMAGE PER HIT";
}
