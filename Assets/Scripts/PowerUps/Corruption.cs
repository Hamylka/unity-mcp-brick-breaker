using System;

/// <summary>What a Cursed result pays back, on top of the outcome card itself (applied when the outcome is confirmed).</summary>
public enum CorruptionDowry
{
    None,
    /// <summary>Cursed: one extra reroll on the next level-up.</summary>
    Reroll,
    /// <summary>Cursed-: the next level-up offers at least one Rare or better card.</summary>
    RareOffer,
    /// <summary>Cursed--: the next level-up offers at least one Legendary card.</summary>
    LegendaryOffer
}

public static class CorruptionDowryInfo
{
    public static CorruptionDowry ForTier(int tier) =>
        tier >= 2 ? CorruptionDowry.LegendaryOffer : tier == 1 ? CorruptionDowry.RareOffer : CorruptionDowry.Reroll;

    /// <summary>Short text for the card screen.</summary>
    public static string Text(CorruptionDowry dowry)
    {
        switch (dowry)
        {
            case CorruptionDowry.Reroll: return "+1 REROLL NEXT LEVEL-UP";
            case CorruptionDowry.RareOffer: return "RARE+ CARD IN THE NEXT OFFER";
            case CorruptionDowry.LegendaryOffer: return "LEGENDARY CARD IN THE NEXT OFFER";
            default: return "";
        }
    }
}

/// <summary>What a corruption gave: the outcome card (Exquisite or Cursed) and the corrupted card's lowered cap.</summary>
public readonly struct CorruptionResult
{
    public readonly PowerUpDefinition Source;
    public readonly PowerUpDefinition Outcome;
    public readonly int OldMax;
    public readonly int NewMax;
    /// <summary>True when the corrupted card can no longer be offered this run (its cap reached what is owned).</summary>
    public readonly bool SourceGone;
    /// <summary>Chance (0-1) the roll had of giving an Exquisite card.</summary>
    public readonly float ExquisiteChance;
    /// <summary>What a Cursed outcome pays back (None for an Exquisite one).</summary>
    public readonly CorruptionDowry Dowry;

    public bool IsExquisite => Outcome != null && RarityStyle.IsExquisite(Outcome.Rarity);

    public CorruptionResult(PowerUpDefinition source, PowerUpDefinition outcome, int oldMax, int newMax, bool sourceGone,
        float exquisiteChance, CorruptionDowry dowry)
    {
        Source = source;
        Outcome = outcome;
        OldMax = oldMax;
        NewMax = newMax;
        SourceGone = sourceGone;
        ExquisiteChance = exquisiteChance;
        Dowry = dowry;
    }
}

/// <summary>What the card screen shows before a card is corrupted: the odds of each side and what a Cursed result pays.</summary>
public readonly struct CorruptionOdds
{
    public readonly float ExquisiteChance;
    public readonly PowerUpRarity ExquisiteRarity;
    public readonly PowerUpRarity CursedRarity;
    public readonly CorruptionDowry Dowry;

    public CorruptionOdds(float exquisiteChance, PowerUpRarity exquisiteRarity, PowerUpRarity cursedRarity, CorruptionDowry dowry)
    {
        ExquisiteChance = exquisiteChance;
        ExquisiteRarity = exquisiteRarity;
        CursedRarity = cursedRarity;
        Dowry = dowry;
    }
}

/// <summary>
/// What the card screen needs from <see cref="PowerUpManager"/> for corruption and for the run's lowered caps.
/// The card screen asks; the manager owns the charges, the caps and the outcome pools.
/// </summary>
public sealed class CorruptionHooks
{
    /// <summary>Corruption charges left (the CORRUPT button only shows while there is one).</summary>
    public Func<int> Charges;
    /// <summary>A card's max level this run (its cap minus earlier corruptions; 0 = unlimited).</summary>
    public Func<PowerUpDefinition, int> MaxStacks;
    /// <summary>True when this card may be corrupted now.</summary>
    public Func<PowerUpDefinition, bool> CanCorrupt;
    /// <summary>The odds and the dowry of corrupting this card, as the roll will use them.</summary>
    public Func<PowerUpDefinition, CorruptionOdds> Odds;
    /// <summary>Spends a charge, lowers the card's cap and rolls the outcome.</summary>
    public Func<PowerUpDefinition, CorruptionResult> Corrupt;
}
