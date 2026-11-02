using UnityEngine;

/// <summary>Brittle Pact (Cursed--): the Shield bonus stops dropping and Shield Plus and Phoenix Ball are lost for the run.</summary>
[CreateAssetMenu(fileName = "BrittlePactEffect", menuName = "BrickBreaker/Effects/Brittle Pact")]
public class BrittlePactEffect : PowerUpEffect
{
    public override void Apply(PowerUpContext context) => PowerUpStats.BrittlePact = true;

    public override string Describe(int stacks) => "NO SHIELD DROPS, SHIELD PLUS OR PHOENIX";
}
