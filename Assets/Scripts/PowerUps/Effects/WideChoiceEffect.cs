using UnityEngine;

/// <summary>One more card on every level-up offer.</summary>
[CreateAssetMenu(fileName = "WideChoiceEffect", menuName = "BrickBreaker/Effects/Wide Choice")]
public class WideChoiceEffect : PowerUpEffect
{
    public override void Apply(PowerUpContext context)
    {
        if (context.Manager != null) context.Manager.BonusChoices += 1;
    }

    public override string Describe(int stacks) => $"+{stacks} CARD PER LEVEL-UP";
}
