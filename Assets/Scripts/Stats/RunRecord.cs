using System;
using System.Collections.Generic;

/// <summary>
/// Everything recorded about one run, saved as JSON by the <see cref="RunProfiler"/> (plain public fields so
/// JsonUtility can write it). Times are seconds of actual play. Arrays named "dmg" follow the order of
/// <see cref="sources"/> (the <see cref="DamageSource"/> enum).
/// </summary>
[Serializable]
public class RunRecord
{
    public int schema = 3;
    public string runId;
    public string startedUtc;
    public string appVersion;
    public string platform;
    public string device;

    /// <summary>Damage source names, the index of every "dmg" / "hits" / "kills" / "xp" array.</summary>
    public string[] sources;

    public Settings settings = new();
    public Summary summary = new();
    public List<SourceTotal> bySource = new();
    public List<LevelEntry> levels = new();
    public List<OfferEntry> offers = new();
    public List<EventEntry> events = new();
    public List<Sample> samples = new();

    [Serializable]
    public class Settings
    {
        public int baseXp;
        public float xpPower;
        public int damagePerLevel;
        public int rerolls;

        // Added in schema 2: the tuning values of the run (0 in older files).
        public float baseBricksPerLevel;
        public float bricksPerLevelGrowth;
        public float targetHitsPerMinute;
        public float targetKillsPerSecond;
        public float maxPaceFactor;
        public float labelDamageWindow;
        public float labelDamageMaxRatio;
        public float wallStepIntervalStart;
        public float speedStepInterval;
        public float speedStepAmount;
        public float minSpeedFactor;
    }

    [Serializable]
    public class Summary
    {
        public string endReason;
        public float runTime;
        public int level;
        public int totalXp;
        public int bricksDestroyed;
        public int rowsSpawned;
        public float targetHits;
        public float smoothedDamage;
        public float paceFactor;
        public float killRate;
        public int hpWindowMin;
        public int hpWindowMax;
        public int ballDamage;
        public int bonusesDropped;
        public int bonusesCollected;
        public int zonesSpawned;
        public int zonesTaken;
        public int rerollsUsed;
        public int wallClears;
        public int totalDamage;

        // Echo (0 without the card, and in older files): procs fired, repeats dealt, procs where the ball's own hit
        // had already killed the brick, jumps to another brick, repeats lost with no living brick left, and the
        // overkill damage carried onto the next brick.
        public int echoProcs;
        public int echoRepeats;
        public int echoBallKills;
        public int echoJumps;
        public int echoNoTarget;
        public int echoCarriedDamage;

        // Wall bounces made between two brick hits of the same ball (0 in older files). "Hist" arrays count brick
        // hits by how many side / back wall bounces came before them: index 0 to 5, index 6 = 6 or more.
        // The totals are the raw bounce counts (not capped).
        public int brickHitsCounted;
        public int[] sideBounceHist = new int[7];
        public int[] backBounceHist = new int[7];
        public int sideBounceTotal;
        public int backBounceTotal;
        public int ballsLost;
    }

    /// <summary>Damage dealt by one source over the whole run.</summary>
    [Serializable]
    public class SourceTotal
    {
        public string source;
        /// <summary>Number of hits that removed at least 1 HP.</summary>
        public int hits;
        /// <summary>HP actually removed (overkill not counted).</summary>
        public int damage;
        /// <summary>Bricks this source finished off.</summary>
        public int kills;
        /// <summary>XP earned from that damage (before XP boosts, estimated from the HP and the brick's XP per HP).</summary>
        public float xp;
    }

    /// <summary>One level-up: the state of the run at the moment it was gained. Cumulative values.</summary>
    [Serializable]
    public class LevelEntry
    {
        public int level;
        public float t;
        public int totalXp;
        public int xpToNext;
        public int ballDamage;
        public int bricksDestroyed;
        public int wallBricks;
        public int wallHp;
        public int[] dmg;
    }

    /// <summary>A set of cards shown on a level-up (or a reroll of it) and what was picked.</summary>
    [Serializable]
    public class OfferEntry
    {
        public float t;
        public int level;
        public bool reroll;
        public List<string> offered = new();
        public List<string> rarities = new();
        /// <summary>Name and level of the picked card, e.g. "Shockwave LV2". Empty if this roll was rerolled away.</summary>
        public string chosen = "";
        public string chosenRarity = "";
    }

    /// <summary>A moment worth remembering: bonus, zone, scale-up, clear, lost ball...</summary>
    [Serializable]
    public class EventEntry
    {
        public float t;
        public string type;
        public string name;
        public float value;
        public string detail;
    }

    /// <summary>Regular snapshot of the run (every few seconds).</summary>
    [Serializable]
    public class Sample
    {
        public float t;
        public int level;
        public int totalXp;
        public int ballDamage;
        public int wallBricks;
        public int wallHp;
        public int hpMax;
        public int totalDamage;
        public int flyingBalls;

        // Added in schema 2 (0 in older files). ballDamage above is the live value (wall buffs and the Phase malus
        // included); baseDamage is what the HP window follows.
        public int baseDamage;
        public float cardMultiplier;
        public float smoothedDamage;
        public float targetHits;
        public float paceFactor;
        public float killRate;
        public int labelDamage;
        public float speedFactor;
        public float wallStepInterval;
        public float ballSize;
        public int corruptionCharges;
        // Share of the XP a brick pays under the kill-rate brake (1 = no brake). Added with the brake; 0 in older files.
        public float xpBrake;

        // Added with the 2026-10-31 rebalance (0 in older files). stepsLeft = wall steps before the front row would
        // cross the lose line (same measure as the danger meter), frontGap = that distance in world units,
        // dangerLevel = DangerMeter.Level (0 to 1). killRatePerBall = killRate / balls flying (at least 1) and
        // rampHits = the part of targetHits that comes from the steady per-minute ramp.
        public float dangerLevel;
        public float stepsLeft;
        public float frontGap;
        public float killRatePerBall;
        public float rampHits;
    }
}
