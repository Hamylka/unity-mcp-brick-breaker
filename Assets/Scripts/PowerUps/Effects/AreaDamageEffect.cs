using UnityEngine;

/// <summary>Area-of-effect power-ups. One class, one asset per power-up: pick the kind and its amount.</summary>
[CreateAssetMenu(fileName = "AreaDamageEffect", menuName = "BrickBreaker/Effects/Area Damage")]
public class AreaDamageEffect : PowerUpEffect
{
    public override bool IsOffensive => true;

    public enum Kind
    {
        /// <summary>Amount = share of the damage dealt that splashes onto the neighbours (0.25 = 25%).</summary>
        Shockwave,
        /// <summary>Amount = arc damage, as a share of the ball's hit damage (0.2 = 20%, does not stack). First pick = 2 arcs, each pick adds one.</summary>
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
            case Kind.ChainLightning: area.AddChain(amount); break;
            case Kind.Piercing: area.AddPierce(Mathf.RoundToInt(amount)); break;
            case Kind.Volatile: area.AddBlast(amount); break;
            case Kind.Cleave: area.AddCleave(); break;
            case Kind.Meltdown: area.EnableMeltdown(); break;
        }
    }

    public override string Describe(int stacks)
    {
        int percent = Mathf.RoundToInt(amount * 100f);
        switch (kind)
        {
            case Kind.Shockwave: return $"{Mathf.RoundToInt(AreaDamage.SplashFor(amount, stacks) * 100f)}% SPLASH TO NEIGHBOURS";
            case Kind.ChainLightning: return $"ARCS TO {stacks + 1} BRICKS FOR {percent}% DMG";
            case Kind.Piercing:
                int depth = Mathf.RoundToInt(amount) * stacks;
                return $"PIERCES {depth} MORE BRICK{(depth > 1 ? "S" : "")}";
            case Kind.Volatile: return $"DEAD BRICKS BLAST 3X3 FOR {percent * stacks}% DMG";
            case Kind.Cleave: return $"EVERY {AreaDamage.CleaveEveryFor(stacks)} HITS SWEEP THE ROW";
            case Kind.Meltdown: return "HITS BURN, FIRE SPREADS ON DEATH";
            default: return null;
        }
    }
}
