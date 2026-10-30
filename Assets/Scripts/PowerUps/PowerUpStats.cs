using UnityEngine;

/// <summary>
/// Run-wide numbers that power-up cards change and that other systems read (falling bonuses, shield, laser,
/// multi-ball...). Reset when a run starts (<see cref="PowerUpManager"/> enables). Each card's effect writes its
/// own field; the system it affects reads it, so neither needs to know the other.
/// </summary>
public static class PowerUpStats
{
    /// <summary>Speed factor of falling bonus items (Slow Fall; Leaden Bonuses raises it, up to <see cref="MaxBonusFallSpeed"/>).</summary>
    public static float BonusFallSpeed { get; set; } = 1f;
    public const float MaxBonusFallSpeed = 2.5f;

    /// <summary>XP multiplier floor: Dim Orbs (Cursed, unlimited) never takes XP below this share.</summary>
    public const float MinXpMultiplier = 0.4f;

    /// <summary>Wall step interval floor, as a share of the run's start interval (Rushing Wall, Cursed, unlimited).</summary>
    public const float MinWallStepFactor = 0.5f;

    /// <summary>Width factor of the area in which the paddle catches bonus items (Catch Zone).</summary>
    public static float CatchZoneScale { get; set; } = 1f;

    /// <summary>Extra damage fraction on bricks at or under <see cref="ExecutionerThreshold"/> of their HP (Executioner).</summary>
    public static float ExecutionerBonus { get; set; }
    public const float ExecutionerThreshold = 0.25f;

    /// <summary>Extra bounces a new shield absorbs before it breaks (Shield Plus).</summary>
    public static int ShieldExtraBounces { get; set; }

    /// <summary>Laser fire-rate factor (Laser Focus, Laser Frenzy; picks add up linearly). 1.2 = a shot every 1/1.2 of the base interval.</summary>
    public static float LaserFireRate { get; set; } = 1f;

    /// <summary>Extra balls every Multi-Ball pickup adds (Twin Fission).</summary>
    public static int ExtraMultiBalls { get; set; }

    /// <summary>Duration factor of timed bonuses (Bonus Extender): Laser and Multi-Ball.</summary>
    public static float BonusDuration { get; set; } = 1f;

    /// <summary>Damage added per brick hit since the ball last touched the paddle (Rampage), as a fraction.</summary>
    public static float RampagePercent { get; set; }

    /// <summary>Damage added per wall bounce since the ball's last brick hit (Ricochet), as a fraction.</summary>
    public static float RicochetPercent { get; set; }

    /// <summary>Echo picks: every <see cref="EchoInterval"/> ball hits repeat once for free.</summary>
    public static int EchoPicks { get; set; }

    /// <summary>Share of the damage beyond a brick's HP that passes to the brick behind (Overkill Spill).</summary>
    public static float OverkillPercent { get; set; }

    /// <summary>Orbs circling each ball (Satellite).</summary>
    public static int SatelliteOrbs { get; set; }

    /// <summary>Paddle Cannon picks: lanes, then extra bolt damage (see <see cref="PaddleCannon"/>).</summary>
    public static int CannonPicks { get; set; }

    /// <summary>Added to the bonus drop chance (Greed), as a fraction. The total is capped by <see cref="MaxDropChance"/>.</summary>
    public static float BonusDropBonus { get; set; }
    public const float MaxDropChance = 0.30f;

    /// <summary>Falling bonus items drift toward the paddle (Bonus Magnet).</summary>
    public static bool BonusMagnet { get; set; }

    /// <summary>A lost last ball is replaced once by a new one on the paddle (Phoenix Ball).</summary>
    public static bool PhoenixReady { get; set; }

    /// <summary>Brake Pads picks: the wall waits longer between steps (see <see cref="WallStepFactor"/>).</summary>
    public static int BrakePicks { get; set; }
    private const float BrakeMaxGain = 0.5f;  // the wall never waits more than +50% longer
    private const float BrakeRate = 0.96f;    // each pick adds 4% less than the one before (the first pick is +2%)

    /// <summary>Multiplier of the wall step interval: +2% for the first pick, diminishing toward +50%.</summary>
    public static float WallStepFactor => WallStepFactorFor(BrakePicks);
    public static float WallStepFactorFor(int picks) => 1f + BrakeMaxGain * (1f - Mathf.Pow(BrakeRate, picks));

    /// <summary>Chance, 0-1, that an XP orb pays double (Lucky Orbs). Capped by <see cref="MaxLuckyOrbChance"/>.</summary>
    public static float LuckyOrbChance { get; set; }
    public const float MaxLuckyOrbChance = 0.30f;

    /// <summary>Duration factor of wall buffs, 2x damage and Phase (Buff Lasting). Additive per pick.</summary>
    public static float WallBuffDuration { get; set; } = 1f;

    /// <summary>Zone Beacon picks: wall buff zones come sooner and are longer.</summary>
    public static int ZoneBeaconPicks { get; set; }
    private const float ZoneIntervalFloor = 0.45f;  // the wait between zones never drops under 45% (9 s of 20 s)
    private const float ZoneIntervalRate = 0.9455f; // the first pick is 3% sooner, each next one gains less
    private const float ZoneLengthPerPick = 0.04f;
    private const float ZoneLengthMax = 2f;

    /// <summary>Multiplier of the wait before a wall buff zone appears.</summary>
    public static float ZoneIntervalFactor => ZoneIntervalFactorFor(ZoneBeaconPicks);
    public static float ZoneIntervalFactorFor(int picks) => ZoneIntervalFloor + (1f - ZoneIntervalFloor) * Mathf.Pow(ZoneIntervalRate, picks);

    /// <summary>Multiplier of the length of a wall buff zone along the wall (+4% per pick, at most x2).</summary>
    public static float ZoneLengthFactor => ZoneLengthFactorFor(ZoneBeaconPicks);
    public static float ZoneLengthFactorFor(int picks) => Mathf.Min(ZoneLengthMax, 1f + ZoneLengthPerPick * picks);

    /// <summary>Size of every ball as a share of its start size (Marble): 1 = normal, 0.6 = the smallest.</summary>
    public static float BallSize { get; set; } = 1f;
    public const float MinBallSize = 0.6f;

    /// <summary>Corruption charges left (Glitch Core). Each one lets the player corrupt a capped card on a level-up.</summary>
    public static int CorruptionCharges { get; set; }

    /// <summary>Toll (Cursed--): rows the wall is pushed toward the paddle on every level-up. 0 = no Toll.</summary>
    public static int TollRows { get; set; }
    /// <summary>Hungry Wall (Cursed--): multiplier on the HP window of new bricks.</summary>
    public static float WallHpFactor { get; set; } = 1f;
    /// <summary>Brittle Pact (Cursed--): no more Shield drops, Shield Plus or Phoenix Ball this run.</summary>
    public static bool BrittlePact { get; set; }

    private static int echoCounter;
    private const int EchoEveryHits = 4; // fixed: every 4th ball hit echoes, once per pick
    public static readonly Color EchoColor = new(0.75f, 0.9f, 1f);
    private const int EchoChargeHits = 3; // the balls show the charge only over the last hits before an echo

    // Echo procs in one frame that get the full feedback (ring, sound, ball tick); later ones only pulse their brick.
    private const int MaxEchoFxPerFrame = 2;
    private static int echoFxFrame = -1;
    private static int echoFxCount;

    /// <summary>Ball hits between two Echo repeats after this many picks (0 = no Echo).</summary>
    public static int EchoInterval(int picks) => picks <= 0 ? 0 : EchoEveryHits;

    /// <summary>
    /// How close the next Echo is, 0-1, shown on every ball (the counter is shared, any ball can fire it): 0 until
    /// the last <see cref="EchoChargeHits"/> hits, then 1/3, 2/3, and 1 when the next ball hit echoes. Short intervals
    /// use fewer steps, so the hit right after an echo always reads 0.
    /// </summary>
    public static float EchoCharge
    {
        get
        {
            int every = EchoInterval(EchoPicks);
            if (every <= 1) return 0f;
            int steps = Mathf.Min(EchoChargeHits, every - 1);
            int hitsToGo = every - echoCounter; // 1 = the next hit echoes
            return Mathf.Clamp01((steps + 1 - hitsToGo) / (float)steps);
        }
    }

    public static void Reset()
    {
        SatelliteOrbs = 0;
        CannonPicks = 0;
        BonusDropBonus = 0f;
        BonusMagnet = false;
        PhoenixReady = false;
        RampagePercent = 0f;
        RicochetPercent = 0f;
        EchoPicks = 0;
        OverkillPercent = 0f;
        echoCounter = 0;
        BonusFallSpeed = 1f;
        CatchZoneScale = 1f;
        ExecutionerBonus = 0f;
        ShieldExtraBounces = 0;
        LaserFireRate = 1f;
        ExtraMultiBalls = 0;
        BonusDuration = 1f;
        BrakePicks = 0;
        LuckyOrbChance = 0f;
        WallBuffDuration = 1f;
        ZoneBeaconPicks = 0;
        BallSize = 1f;
        CorruptionCharges = 0;
        TollRows = 0;
        WallHpFactor = 1f;
        BrittlePact = false;
    }

    /// <summary>
    /// Damage of this ball's next hit on <paramref name="brick"/>: the ball's damage with Rampage, Ricochet and
    /// Executioner counted (rounded up).
    /// </summary>
    public static int BallHitDamage(BallController ball, Brick brick)
    {
        float factor = 1f + RampagePercent * ball.HitsSincePaddle + RicochetPercent * ball.WallBounces;
        int damage = factor > 1f ? Mathf.CeilToInt(ball.Damage * factor - 0.001f) : ball.Damage;
        return WithExecutioner(brick, damage);
    }

    /// <summary>
    /// Bookkeeping after a ball hit landed: streak counters, then the extra hits that ride on it (Overkill Spill, Echo).
    /// </summary>
    public static void AfterBallHit(BallController ball, Brick brick, int damage, int hpBefore)
    {
        ball.RegisterBrickHit();

        if (OverkillPercent > 0f && damage > hpBefore)
        {
            BrickGrid grid = brick.GetComponentInParent<BrickGrid>();
            if (grid != null && grid.TryGetComponent(out AreaDamage area))
                area.OverkillSpill(brick, (damage - hpBefore) * OverkillPercent);
        }

        int every = EchoInterval(EchoPicks);
        if (every > 0 && ++echoCounter >= every)
        {
            echoCounter = 0;

            // The hit rings again; it still triggers area effects like any ball hit. If the ball's hit already
            // killed the brick, the echo jumps to the nearest living brick and carries the overkill with it.
            int carry = brick.IsAlive ? 0 : Mathf.Max(0, damage - hpBefore);
            Brick first = brick.IsAlive ? brick : NearestLiveBrick(brick);
            if (first == null) return;
            first.Pulse(EchoColor * 1.6f, 0.22f, 0.08f);
            if (Time.frameCount != echoFxFrame)
            {
                echoFxFrame = Time.frameCount;
                echoFxCount = 0;
            }
            if (++echoFxCount <= MaxEchoFxPerFrame)
            {
                NeonFx.Ring(first.transform.position, EchoColor, 16, 2.5f, 0.08f, 0.25f);
                // A short bright tick on the ball, so the player sees which ball echoed.
                NeonFx.Flash(ball.Position, NeonGlow.Hdr(EchoColor, 2.2f), 0.45f, 0.06f);
                if (echoFxCount == 1) SoundManager.Play(Sfx.ChainZap, 0.8f);
            }
            // One repeat per pick, each a full copy of the hit. When a repeat kills its target, the next one jumps
            // to the nearest living brick and adds the damage beyond what the dead brick had left.
            using var tag = DamageTag.Begin(DamageSource.Echo);
            Brick target = first;
            for (int i = 0; i < EchoPicks; i++)
            {
                if (target == null || !target.IsAlive)
                {
                    Brick from = target != null ? target : brick;
                    target = NearestLiveBrick(from);
                    if (target == null) break;
                    ChainBolt.Spawn(from.transform.position, target.transform.position, EchoColor * 1.6f, 0.015f, 0.12f, 0.14f);
                    target.Pulse(EchoColor * 1.6f, 0.22f, 0.04f);
                }
                int hp = target.HitPoints;
                int dealt = damage + carry;
                carry = 0;
                target.TakeHit(dealt);
                if (!target.IsAlive) carry = Mathf.Max(0, dealt - hp);
            }
        }
    }

    private static readonly System.Collections.Generic.List<Brick> liveScratch = new();

    /// <summary>The living brick closest to <paramref name="from"/> (null when the wall is empty).</summary>
    private static Brick NearestLiveBrick(Brick from)
    {
        BrickGrid grid = from != null ? from.GetComponentInParent<BrickGrid>() : null;
        if (grid == null) return null;
        grid.GetLiveBricks(liveScratch);
        Vector3 origin = from.transform.position;
        Brick best = null;
        float bestDist = float.MaxValue;
        foreach (Brick candidate in liveScratch)
        {
            if (candidate == from || !candidate.IsAlive) continue;
            float d = (candidate.transform.position - origin).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = candidate; }
        }
        return best;
    }

    /// <summary>Damage a ball hit deals to <paramref name="brick"/> once Executioner is counted (rounded up).</summary>
    public static int WithExecutioner(Brick brick, int damage)
    {
        if (ExecutionerBonus <= 0f || brick == null) return damage;
        if (brick.HitPoints > brick.MaxHitPoints * ExecutionerThreshold) return damage;
        return Mathf.CeilToInt(damage * (1f + ExecutionerBonus) - 0.001f);
    }
}
