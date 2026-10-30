using UnityEngine;

/// <summary>Card rarities. Stored as numbers in the assets: add new ones at the end, never in between.</summary>
public enum PowerUpRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    /// <summary>Above Legendary, only from a corruption (weight 0).</summary>
    Exquisite,
    /// <summary>A real downside, only from a corruption (weight 0).</summary>
    Cursed
}

/// <summary>Colors and labels for each rarity.</summary>
public static class RarityStyle
{
    public static readonly Color ExquisiteColor = new(1.00f, 0.16f, 0.29f);
    public static readonly Color CursedColor = new(0.60f, 0.62f, 0.70f);

    public static Color GetColor(PowerUpRarity rarity)
    {
        switch (rarity)
        {
            case PowerUpRarity.Uncommon: return new Color(0.35f, 0.92f, 0.42f);
            case PowerUpRarity.Rare: return new Color(0.30f, 0.62f, 1.00f);
            case PowerUpRarity.Epic: return new Color(0.72f, 0.40f, 1.00f);
            case PowerUpRarity.Legendary: return new Color(1.00f, 0.82f, 0.20f);
            case PowerUpRarity.Exquisite: return ExquisiteColor;
            case PowerUpRarity.Cursed: return CursedColor;
            default: return new Color(0.82f, 0.84f, 0.88f);
        }
    }

    /// <summary>Card body color: dark navy, blood dark for Exquisite, near black for Cursed.</summary>
    public static Color GetBodyColor(PowerUpRarity rarity, Color normal)
    {
        switch (rarity)
        {
            case PowerUpRarity.Exquisite: return new Color(0.13f, 0.02f, 0.05f, 1f);
            case PowerUpRarity.Cursed: return new Color(0.045f, 0.045f, 0.06f, 1f);
            default: return normal;
        }
    }

    public static string GetLabel(PowerUpRarity rarity) => rarity.ToString().ToUpperInvariant();

    /// <summary>True for the rarities only a corruption can give (never rolled on a level-up).</summary>
    public static bool IsCorruption(PowerUpRarity rarity) => rarity >= PowerUpRarity.Exquisite;

    /// <summary>Order for "rarest first" lists: Exquisite on top, then Legendary to Common, Cursed last.</summary>
    public static int SortRank(PowerUpRarity rarity) => rarity == PowerUpRarity.Cursed ? -1 : (int)rarity;
}
