using UnityEngine;

/// <summary>
/// Run-wide numbers that power-up cards change and that other systems read (falling bonuses, shield, laser,
/// multi-ball...). Reset when a run starts (<see cref="PowerUpManager"/> enables). Each card's effect writes its
/// own field; the system it affects reads it, so neither needs to know the other.
/// </summary>
public static class PowerUpStats
{
    /// <summary>Speed factor of falling bonus items (Slow Fall).</summary>
    public static float BonusFallSpeed { get; set; } = 1f;

    /// <summary>Width factor of the area in which the paddle catches bonus items (Catch Zone).</summary>
    public static float CatchZoneScale { get; set; } = 1f;

    /// <summary>Extra damage fraction on bricks at or under <see cref="ExecutionerThreshold"/> of their HP (Executioner).</summary>
    public static float ExecutionerBonus { get; set; }
    public const float ExecutionerThreshold = 0.25f;

    /// <summary>Extra bounces a new shield absorbs before it breaks (Shield Plus).</summary>
    public static int ShieldExtraBounces { get; set; }

    /// <summary>Laser fire-rate factor (Laser Focus). 1.2 = a shot every 1/1.2 of the base interval.</summary>
    public static float LaserFireRate { get; set; } = 1f;

    /// <summary>Extra balls every Multi-Ball pickup adds (Twin Fission).</summary>
    public static int ExtraMultiBalls { get; set; }

    /// <summary>Duration factor of timed bonuses (Bonus Extender): Laser and Multi-Ball.</summary>
    public static float BonusDuration { get; set; } = 1f;

    public static void Reset()
    {
        BonusFallSpeed = 1f;
        CatchZoneScale = 1f;
        ExecutionerBonus = 0f;
        ShieldExtraBounces = 0;
        LaserFireRate = 1f;
        ExtraMultiBalls = 0;
        BonusDuration = 1f;
    }

    /// <summary>Damage a ball hit deals to <paramref name="brick"/> once Executioner is counted (rounded up).</summary>
    public static int WithExecutioner(Brick brick, int damage)
    {
        if (ExecutionerBonus <= 0f || brick == null) return damage;
        if (brick.HitPoints > brick.MaxHitPoints * ExecutionerThreshold) return damage;
        return Mathf.CeilToInt(damage * (1f + ExecutionerBonus) - 0.001f);
    }
}
