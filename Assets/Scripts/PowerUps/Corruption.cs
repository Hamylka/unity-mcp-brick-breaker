using System;

/// <summary>What a corruption gave: the outcome card (Exquisite or Cursed) and the corrupted card's lowered cap.</summary>
public readonly struct CorruptionResult
{
    public readonly PowerUpDefinition Source;
    public readonly PowerUpDefinition Outcome;
    public readonly int OldMax;
    public readonly int NewMax;
    /// <summary>True when the corrupted card can no longer be offered this run (its cap reached what is owned).</summary>
    public readonly bool SourceGone;

    public bool IsExquisite => Outcome != null && Outcome.Rarity == PowerUpRarity.Exquisite;

    public CorruptionResult(PowerUpDefinition source, PowerUpDefinition outcome, int oldMax, int newMax, bool sourceGone)
    {
        Source = source;
        Outcome = outcome;
        OldMax = oldMax;
        NewMax = newMax;
        SourceGone = sourceGone;
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
    /// <summary>Spends a charge, lowers the card's cap and rolls the outcome.</summary>
    public Func<PowerUpDefinition, CorruptionResult> Corrupt;
}
