using System;
using UnityEngine;

/// <summary>
/// Endless XP progression. Every ball hit grants XP equal to the hit points it removed from the brick.
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
    [SerializeField, Min(1)] private int baseXp = 10;
    [Tooltip("Exponent of the curve. 1 = linear, higher = steeper.")]
    [SerializeField, Min(0.1f)] private float power = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool logLevelUps = true;
    [SerializeField] private bool showDebugOverlay = true;

    /// <summary>Raised whenever XP changes: current XP, XP required for the next level, current level.</summary>
    public event Action<int, int, int> XpChanged;
    /// <summary>Raised once per level gained, with the new level.</summary>
    public event Action<int> LevelUp;

    private float xpMultiplier = 1f;
    private float xpRemainder;

    /// <summary>Optional: carries XP visually from the brick to the bar before it is added.</summary>
    public IXpVisuals XpVisuals { get; set; }

    /// <summary>Raises the XP gained from bricks (upgrades). 0.2 = +20%.</summary>
    public void AddXpMultiplier(float amount) => xpMultiplier += amount;

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
        if (grid != null) grid.BrickHit += HandleBrickHit;
    }

    private void OnDisable()
    {
        if (grid != null) grid.BrickHit -= HandleBrickHit;
    }

    private void Start()
    {
        XpChanged?.Invoke(CurrentXp, XpToNextLevel, Level);
    }

    private void HandleBrickHit(Brick brick, int damageDealt)
    {
        // 1 XP per hit point removed. Fractions of the multiplier carry over, so small bonuses still add up.
        xpRemainder += damageDealt * xpMultiplier;
        int amount = Mathf.FloorToInt(xpRemainder);
        if (amount <= 0) return;
        xpRemainder -= amount;

        if (XpVisuals != null) XpVisuals.Deliver(brick.transform.position, amount, AddXp); // each orb adds its own share
        else AddXp(amount);
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
