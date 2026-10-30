using System;
using UnityEngine;

/// <summary>
/// Endless XP progression. A brick is worth 1/Bricks Per Level of the current level's cost, paid per hit point removed.
/// If an <see cref="IXpVisuals"/> is registered (the HUD), the XP arrives later, when its orb reaches the bar.
/// XP needed to go from level L to L + 1 is:  Required XP = Base XP x (L ^ Power)  (rounded to a whole number).
/// Excess XP carries over, and a big gain can raise several levels at once.
/// Other systems (perk choice UI, FX, sound) subscribe to <see cref="LevelUp"/> and <see cref="XpChanged"/>.
/// </summary>
public class ExperienceSystem : MonoBehaviour
{
    [SerializeField] private BrickGrid grid;

    [Header("Level Curve (editable at runtime for balancing)")]
    [Tooltip("XP needed to leave level 1. Also scales the whole curve.")]
    [SerializeField, Min(1)] private int baseXp = 12;
    [Tooltip("Exponent of the curve. 1 = linear, higher = steeper.")]
    [SerializeField, Min(0.1f)] private float power = 1.2f;

    [Header("XP per Brick")]
    [Tooltip("A typical brick is worth (XP needed for the current level) / bricks per level, split over its HP. Higher = slower levels. This is the value at level 1.")]
    [SerializeField, Min(0.5f)] private float bricksPerLevel = 8f;
    [Tooltip("Bricks per level grow by this much for every level gained, so level-ups stay spaced out when the kill rate is high (8 + 0.4 x 49 = 27.6 at level 50).")]
    [SerializeField, Min(0f)] private float bricksPerLevelGrowth = 0.4f;
    [Tooltip("A brick's worth is scaled by its max HP / the middle of the HP window now spawning, clamped to this. Weak or outdated bricks pay less, tough ones more (1 = ignore HP).")]
    [SerializeField, Min(1f)] private float maxHpWorth = 2f;

    [Header("Wall Clear Bonus")]
    [Tooltip("Clearing every brick grants this share of the XP needed for the current level (0.5 = 50%).")]
    [SerializeField, Range(0f, 2f)] private float clearBonus = 0.25f;
    [Tooltip("Seconds of play since the previous clear for the bonus to be paid in full; a faster clear pays proportionally less (min 10%). Stops clear spamming from paying a level each.")]
    [SerializeField, Min(0f)] private float clearBonusFullAfter = 20f;

    [Header("XP Orbs")]
    [Tooltip("XP orbs leave the ball if one is within this distance of the brick that was hit, otherwise they leave the brick.")]
    [SerializeField, Min(0f)] private float orbOriginRange = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool logLevelUps = true;
    [SerializeField] private bool showDebugOverlay = true;

    /// <summary>Raised whenever XP changes: current XP, XP required for the next level, current level.</summary>
    public event Action<int, int, int> XpChanged;
    /// <summary>Raised once per level gained, with the new level.</summary>
    public event Action<int> LevelUp;
    /// <summary>Raised when the whole wall is cleared, with the bonus XP that is on its way.</summary>
    public event Action<int> ClearBonusGranted;

    private float xpMultiplier = 1f;
    private float xpRemainder;
    private float lastClearTime = -999f;
    private Vector3 lastHitPosition;
    private int pendingXp;         // XP earned this frame, waiting to be sent as merged orbs
    private Vector3 pendingBrick;  // first brick hit this frame

    /// <summary>Optional: carries XP visually from the brick to the bar before it is added.</summary>
    public IXpVisuals XpVisuals { get; set; }

    /// <summary>Raises the XP gained from bricks (upgrades). 0.2 = +20%.</summary>
    public void AddXpMultiplier(float amount) => xpMultiplier += amount;

    /// <summary>
    /// XP (before the Quick Learner multiplier) for removing <paramref name="damageDealt"/> HP from a brick:
    /// the share of its HP times the brick's worth, <c>XpToNextLevel / Bricks Per Level</c>.
    /// </summary>
    public float XpForHit(Brick brick, int damageDealt)
    {
        int maxHp = Mathf.Max(1, brick.MaxHitPoints);
        // HP worth: a brick of the middle of the current HP window pays 1x, tougher ones more, weaker or outdated ones less.
        float worth = 1f;
        if (grid != null)
        {
            float mid = Mathf.Max(1f, (grid.CurrentMinHp + grid.CurrentMaxHp) * 0.5f);
            worth = Mathf.Clamp(maxHp / mid, 1f / maxHpWorth, maxHpWorth);
        }
        return damageDealt / (float)maxHp * worth * XpToNextLevel / BricksPerLevel;
    }

    public int BaseXp => baseXp;
    /// <summary>Bricks a level is worth now: the base value plus the growth per level gained.</summary>
    public float BricksPerLevel => bricksPerLevel + bricksPerLevelGrowth * (Level - 1);
    public float Power => power;
    /// <summary>Settings read by the run profiler.</summary>
    public float BaseBricksPerLevel => bricksPerLevel;
    public float BricksPerLevelGrowth => bricksPerLevelGrowth;
    public int Level { get; private set; } = 1;
    public int CurrentXp { get; private set; }
    public int TotalXp { get; private set; }
    public int XpToNextLevel => GetRequiredXp(Level);
    public float Progress01 => Mathf.Clamp01(CurrentXp / (float)XpToNextLevel);

    /// <summary>XP needed to go from <paramref name="level"/> to the next one.</summary>
    public int GetRequiredXp(int level)
    {
        return Mathf.Max(1, Mathf.RoundToInt(baseXp * Mathf.Pow(Mathf.Max(1, level), power)));
    }

    private void OnEnable()
    {
        if (grid == null) return;
        grid.BrickHit += HandleBrickHit;
        grid.BricksCleared += HandleWallCleared;
    }

    private void OnDisable()
    {
        if (grid == null) return;
        grid.BrickHit -= HandleBrickHit;
        grid.BricksCleared -= HandleWallCleared;
    }

    private void Start()
    {
        XpChanged?.Invoke(CurrentXp, XpToNextLevel, Level);
    }

    // Every brick cleared: a bonus worth part of the current level, flying out of the last brick that was hit.
    private void HandleWallCleared()
    {
        // Clears in quick succession pay less (a wall that dies in a second is a sign of too much power, not skill).
        float now = grid != null ? grid.RunTime : 0f;
        float share = clearBonusFullAfter <= 0f ? 1f : Mathf.Clamp((now - lastClearTime) / clearBonusFullAfter, 0.1f, 1f);
        lastClearTime = now;
        int amount = Mathf.CeilToInt(XpToNextLevel * clearBonus * share);
        if (amount <= 0) return;

        ClearBonusGranted?.Invoke(amount);

        if (XpVisuals != null) XpVisuals.Deliver(lastHitPosition, amount, AddXp);
        else AddXp(amount);
    }

    private void HandleBrickHit(Brick brick, int damageDealt)
    {
        lastHitPosition = brick.transform.position;

        // A brick is worth a share of the level cost, split per HP removed (see XpForHit). Fractions carry over, so small hits still add up.
        xpRemainder += XpForHit(brick, damageDealt) * Mathf.Max(PowerUpStats.MinXpMultiplier, xpMultiplier); // Dim Orbs floor
        int amount = Mathf.FloorToInt(xpRemainder);
        if (amount <= 0) return;
        xpRemainder -= amount;

        if (XpVisuals == null)
        {
            AddXp(amount);
            return;
        }

        // Everything earned by one ball hit (the hit itself plus splash, chain and cleave damage in the same frame)
        // is merged into a single delivery, sent in LateUpdate. It starts from the ball that made the hit.
        if (pendingXp == 0) pendingBrick = brick.transform.position;
        pendingXp += amount;
    }

    private void LateUpdate()
    {
        if (pendingXp <= 0) return;

        int amount = pendingXp;
        pendingXp = 0;
        if (XpVisuals != null) XpVisuals.Deliver(FindOrbOrigin(pendingBrick), amount, AddXp); // each orb adds its own share
        else AddXp(amount);
    }

    // The orbs leave the ball closest to the first brick hit this frame (it is touching it), or the brick if no ball is near.
    private Vector3 FindOrbOrigin(Vector3 brickPosition)
    {
        Vector3 best = brickPosition;
        float bestSqr = orbOriginRange * orbOriginRange;
        var balls = BallController.Active;
        for (int i = 0; i < balls.Count; i++)
        {
            float sqr = (balls[i].transform.position - brickPosition).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = balls[i].transform.position;
        }
        return best;
    }

    /// <summary>Adds XP and applies any level-ups. Also usable by bonuses or perks.</summary>
    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        CurrentXp += amount;
        TotalXp += amount;

        while (CurrentXp >= XpToNextLevel)
        {
            CurrentXp -= XpToNextLevel;
            Level++;
            if (logLevelUps) Debug.Log($"Level up! Now level {Level} (next level needs {XpToNextLevel} XP).", this);
            LevelUp?.Invoke(Level);
        }

        XpChanged?.Invoke(CurrentXp, XpToNextLevel, Level);
    }

    /// <summary>Back to level 1 with no XP (new run).</summary>
    public void ResetProgress()
    {
        Level = 1;
        CurrentXp = 0;
        TotalXp = 0;
        XpChanged?.Invoke(CurrentXp, XpToNextLevel, Level);
    }

    [ContextMenu("Debug: Add 10 XP")]
    private void DebugAddXp() => AddXp(10);

    [ContextMenu("Debug: Print Level Table")]
    private void DebugPrintTable()
    {
        int cumulative = 0;
        for (int level = 1; level <= 15; level++)
        {
            int required = GetRequiredXp(level);
            cumulative += required;
            Debug.Log($"Level {level} -> {level + 1}: {required} XP (total {cumulative})");
        }
    }

    // Temporary readout for testing the loop; replace with real UI later.
    private void OnGUI()
    {
        if (!showDebugOverlay) return;

        float scale = Screen.height / 1920f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        var label = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold };
        label.normal.textColor = Color.white;
        GUI.Label(new Rect(30, 20, 700, 60), $"Lv {Level}   {CurrentXp} / {XpToNextLevel} XP", label);

        var bar = new Rect(30, 90, 520, 22);
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Progress01, bar.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
