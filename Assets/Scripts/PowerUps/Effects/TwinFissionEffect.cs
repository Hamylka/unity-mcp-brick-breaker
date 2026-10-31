using UnityEngine;

/// <summary>Every Multi-Ball pickup spawns more extra balls.</summary>
[CreateAssetMenu(fileName = "TwinFissionEffect", menuName = "BrickBreaker/Effects/Twin Fission")]
public class TwinFissionEffect : PowerUpEffect
{
    [SerializeField, Min(1)] private int balls = 1;

    public override void Apply(PowerUpContext context) => PowerUpStats.ExtraMultiBalls += balls;

    public override bool CanRemove => true;
    public override void Remove(PowerUpContext context) => PowerUpStats.ExtraMultiBalls = Mathf.Max(0, PowerUpStats.ExtraMultiBalls - balls);

    public override string Describe(int stacks) => $"MULTI-BALL SPAWNS +{balls * stacks} BALL{(balls * stacks > 1 ? "S" : "")}";
}
