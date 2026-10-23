using UnityEngine;

public enum PowerUpRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>Colors and labels for each rarity.</summary>
public static class RarityStyle
{
    public static Color GetColor(PowerUpRarity rarity)
    {
        switch (rarity)
        {
            case PowerUpRarity.Uncommon: return new Color(0.35f, 0.92f, 0.42f);
            case PowerUpRarity.Rare: return new Color(0.30f, 0.62f, 1.00f);
            case PowerUpRarity.Epic: return new Color(0.72f, 0.40f, 1.00f);
            case PowerUpRarity.Legendary: return new Color(1.00f, 0.82f, 0.20f);
            default: return new Color(0.82f, 0.84f, 0.88f);
        }
    }

    public static string GetLabel(PowerUpRarity rarity) => rarity.ToString().ToUpperInvariant();
}
