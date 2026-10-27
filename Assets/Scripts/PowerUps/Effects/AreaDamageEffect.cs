using UnityEngine;

/// <summary>Area-of-effect power-ups. One class, one asset per power-up: pick the kind and its amount.</summary>
[CreateAssetMenu(fileName = "AreaDamageEffect", menuName = "BrickBreaker/Effects/Area Damage")]
public class AreaDamageEffect : PowerUpEffect
{
    public enum Kind
    {
        /// <summary>Amount = share of the damage dealt that splashes onto the neighbours (0.25 = 25%).</summary>
        Shockwave,
        /// <summary>Amount = arc damage added per pick, as a share of the ball's hit damage (0.2 = 20%). Each pick also adds one arc.</summary>
        ChainLightning,
        /// <summary>Amount = extra bricks the shot goes through, in the same column.</summary>
        Piercing,
        /// <summary>Amount = blast damage added per pick, as a share of the ball's hit damage (0.5 = 50%), dealt to the 3x3 block.</summary>
        Volatile,
        /// <summary>Every few hits sweep the whole row. Amount is unused: each pick shortens the delay.</summary>
        Cleave,
        /// <summary>Hit bricks burn, and a burning brick that dies sets its neighbours alight. Amount is unused.</summary>
        Meltdown
    }

    [SerializeField] private Kind kind = Kind.Shockwave;
    [SerializeField, Min(0f)] private float amount = 0.25f;

    public override void Apply(PowerUpContext context)
    {
        AreaDamage area = AreaDamage.For(context.Grid);
        if (area == null) return;

        switch (kind)
        {
            case Kind.Shockwave: area.AddSplash(amount); break;
            case Kind.ChainLightning: area.AddChain(1, amount); break;
            case Kind.Piercing: area.AddPierce(Mathf.RoundToInt(amount)); break;
            case Kind.Volatile: area.AddBlast(amount); break;
            case Kind.Cleave: area.AddCleave(); break;
            case Kind.Meltdown: area.EnableMeltdown(); break;
        }
    }
}
