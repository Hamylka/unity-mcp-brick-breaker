using UnityEngine;

/// <summary>Once per run, losing the last ball puts a new one on the paddle instead of ending the run.</summary>
[CreateAssetMenu(fileName = "PhoenixBallEffect", menuName = "BrickBreaker/Effects/Phoenix Ball")]
public class PhoenixBallEffect : PowerUpEffect
{
    public override void Apply(PowerUpContext context) => PowerUpStats.PhoenixReady = true;

    public override string Describe(int stacks) => PowerUpStats.BrittlePact ? "LOST TO THE PACT" : PowerUpStats.PhoenixReady ? "REVIVES THE BALL ONCE" : "USED";
}
