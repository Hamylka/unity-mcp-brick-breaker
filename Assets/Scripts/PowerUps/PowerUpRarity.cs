using UnityEngine;

/// <summary>Card rarities. Stored as numbers in the assets: add new ones at the end, never in between.</summary>
public enum PowerUpRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    /// <summary>Above Legendary, only from a corruption (weight 0). Minor tier.</summary>
    Exquisite,
    /// <summary>A real downside, only from a corruption (weight 0). Minor tier.</summary>
    Cursed,
    /// <summary>Exquisite+, Major tier (corrupting a Rare or Epic card).</summary>
    ExquisitePlus,
    /// <summary>Exquisite++, Grand tier (corrupting a Legendary card).</summary>
    ExquisitePlusPlus,
    /// <summary>Cursed-, Major tier.</summary>
    CursedMinus,
    /// <summary>Cursed--, Grand tier.</summary>
    CursedMinusMinus
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
            case PowerUpRarity.ExquisitePlus: return Color.Lerp(ExquisiteColor, Color.white, 0.22f);
            case PowerUpRarity.ExquisitePlusPlus: return Color.Lerp(ExquisiteColor, Color.white, 0.45f);
            case PowerUpRarity.Cursed: return CursedColor;
            case PowerUpRarity.CursedMinus: return new Color(0.52f, 0.50f, 0.64f);
            case PowerUpRarity.CursedMinusMinus: return new Color(0.45f, 0.38f, 0.58f);
            default: return new Color(0.82f, 0.84f, 0.88f);
        }
    }

    /// <summary>Card body color: dark navy, blood dark for Exquisite, near black for Cursed (a little deeper each tier).</summary>
    public static Color GetBodyColor(PowerUpRarity rarity, Color normal)
    {
        switch (rarity)
        {
            case PowerUpRarity.Exquisite: return new Color(0.13f, 0.02f, 0.05f, 1f);
            case PowerUpRarity.ExquisitePlus: return new Color(0.17f, 0.03f, 0.07f, 1f);
            case PowerUpRarity.ExquisitePlusPlus: return new Color(0.22f, 0.04f, 0.09f, 1f);
            case PowerUpRarity.Cursed: return new Color(0.045f, 0.045f, 0.06f, 1f);
            case PowerUpRarity.CursedMinus: return new Color(0.05f, 0.04f, 0.07f, 1f);
            case PowerUpRarity.CursedMinusMinus: return new Color(0.06f, 0.03f, 0.08f, 1f);
            default: return normal;
        }
    }

    /// <summary>Name on the card: the tiers carry their signs ("EXQUISITE+", "CURSED--").</summary>
    public static string GetLabel(PowerUpRarity rarity)
    {
        switch (rarity)
        {
            case PowerUpRarity.ExquisitePlus: return "EXQUISITE+";
            case PowerUpRarity.ExquisitePlusPlus: return "EXQUISITE++";
            case PowerUpRarity.CursedMinus: return "CURSED-";
            case PowerUpRarity.CursedMinusMinus: return "CURSED--";
            default: return rarity.ToString().ToUpperInvariant();
        }
    }

    /// <summary>True for the rarities only a corruption can give (never rolled on a level-up).</summary>
    public static bool IsCorruption(PowerUpRarity rarity) => rarity >= PowerUpRarity.Exquisite;

    public static bool IsExquisite(PowerUpRarity rarity) =>
        rarity == PowerUpRarity.Exquisite || rarity == PowerUpRarity.ExquisitePlus || rarity == PowerUpRarity.ExquisitePlusPlus;

    public static bool IsCursed(PowerUpRarity rarity) =>
        rarity == PowerUpRarity.Cursed || rarity == PowerUpRarity.CursedMinus || rarity == PowerUpRarity.CursedMinusMinus;

    /// <summary>Tier of a corruption outcome: 0 Minor, 1 Major, 2 Grand. -1 for a normal rarity.</summary>
    public static int OutcomeTier(PowerUpRarity rarity)
    {
        switch (rarity)
        {
            case PowerUpRarity.Exquisite:
            case PowerUpRarity.Cursed: return 0;
            case PowerUpRarity.ExquisitePlus:
            case PowerUpRarity.CursedMinus: return 1;
            case PowerUpRarity.ExquisitePlusPlus:
            case PowerUpRarity.CursedMinusMinus: return 2;
            default: return -1;
        }
    }

    /// <summary>Which outcome tier corrupting a card of this rarity gives: Common and Uncommon Minor, Rare and Epic Major, Legendary Grand.</summary>
    public static int SourceTier(PowerUpRarity source) =>
        source >= PowerUpRarity.Legendary ? 2 : source >= PowerUpRarity.Rare ? 1 : 0;

    public static PowerUpRarity ExquisiteOfTier(int tier) =>
        tier >= 2 ? PowerUpRarity.ExquisitePlusPlus : tier == 1 ? PowerUpRarity.ExquisitePlus : PowerUpRarity.Exquisite;

    public static PowerUpRarity CursedOfTier(int tier) =>
        tier >= 2 ? PowerUpRarity.CursedMinusMinus : tier == 1 ? PowerUpRarity.CursedMinus : PowerUpRarity.Cursed;

    /// <summary>Order for "rarest first" lists: Exquisite tiers on top, then Legendary to Common, the Cursed tiers last.</summary>
    public static int SortRank(PowerUpRarity rarity)
    {
        switch (rarity)
        {
            case PowerUpRarity.ExquisitePlus: return 6;
            case PowerUpRarity.ExquisitePlusPlus: return 7;
            case PowerUpRarity.Cursed: return -1;
            case PowerUpRarity.CursedMinus: return -2;
            case PowerUpRarity.CursedMinusMinus: return -3;
            default: return (int)rarity;
        }
    }
}
