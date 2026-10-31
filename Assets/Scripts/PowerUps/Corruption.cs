using System;
using System.Collections.Generic;

/// <summary>What a corruption gave: the outcome card (Exquisite or Cursed) and the level the corrupted card lost.</summary>
public readonly struct CorruptionResult
{
    public readonly PowerUpDefinition Source;
    public readonly PowerUpDefinition Outcome;
    /// <summary>Owned level of the corrupted card before / after (one level is the price).</summary>
    public readonly int LevelBefore;
    public readonly int LevelAfter;
    /// <summary>Chance (0-1) the roll had of giving an Exquisite card.</summary>
    public readonly float ExquisiteChance;

    public bool IsExquisite => Outcome != null && RarityStyle.IsExquisite(Outcome.Rarity);
    /// <summary>True when the corrupted card had a single level: it left the owned list (it can be offered again later).</summary>
    public bool SourceRemoved => LevelAfter <= 0;

    public CorruptionResult(PowerUpDefinition source, PowerUpDefinition outcome, int levelBefore, int levelAfter, float exquisiteChance)
    {
        Source = source;
        Outcome = outcome;
        LevelBefore = levelBefore;
        LevelAfter = levelAfter;
        ExquisiteChance = exquisiteChance;
    }
}

/// <summary>
/// What the card screen needs from <see cref="PowerUpManager"/> for corruption. The card screen asks; the manager owns
/// the charges, the owned levels and the outcome pools.
/// </summary>
public sealed class CorruptionHooks
{
    /// <summary>Corruption charges left (the CORRUPT button only shows while there is one).</summary>
    public Func<int> Charges;
    /// <summary>Every owned upgrade with its level (the corruption inventory).</summary>
    public Func<IReadOnlyDictionary<PowerUpDefinition, int>> Owned;
    /// <summary>True when this owned card can pay a level (normal pool, effect with a way back). Ignores the charges.</summary>
    public Func<PowerUpDefinition, bool> IsCorruptible;
    /// <summary>True when a charge is held, a card can pay and an outcome is left to give.</summary>
    public Func<bool> CanOpen;
    /// <summary>Chance (0-1) of an Exquisite outcome when corrupting this card, as the roll will use it.</summary>
    public Func<PowerUpDefinition, float> ExquisiteChance;
    /// <summary>Spends a charge, takes one level of the card back and rolls the outcome.</summary>
    public Func<PowerUpDefinition, CorruptionResult> Corrupt;
    /// <summary>Adds the outcome card to the run (on CONFIRM after the reveal).</summary>
    public Action<PowerUpDefinition> Grant;
}
