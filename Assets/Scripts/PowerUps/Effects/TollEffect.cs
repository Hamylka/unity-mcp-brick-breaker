using UnityEngine;

/// <summary>Toll (Cursed--): every level-up pushes the wall a few rows toward the paddle (the step the wall makes by itself).</summary>
[CreateAssetMenu(fileName = "TollEffect", menuName = "BrickBreaker/Effects/Toll")]
public class TollEffect : PowerUpEffect
{
    [Tooltip("Rows the wall is pushed on every level-up, per pick.")]
    [SerializeField, Min(1)] private int rows = 2;

    public override void Apply(PowerUpContext context) => PowerUpStats.TollRows += rows;

    public override string Describe(int stacks) => $"EVERY LEVEL-UP PUSHES THE WALL {rows * stacks} ROWS";
}
