using System;
using UnityEngine;

/// <summary>
/// Player setting: how brick HP is shown on top of the color ramp. Saved between runs (PlayerPrefs),
/// changed from the pause screen. Every brick listens to <see cref="Changed"/> and updates its label.
/// </summary>
public static class BrickHpDisplay
{
    public enum Mode
    {
        /// <summary>Color, fill and glow only (no label).</summary>
        Color,
        /// <summary>The HP left as a pixel number over the brick.</summary>
        Numbers,
        /// <summary>One pip per HP left (rows of 5); falls back to the number above <see cref="Brick"/>'s pip limit.</summary>
        Pips,
    }

    private const string PrefKey = "BrickHpDisplay";

    private static bool loaded;
    private static Mode mode;

    public static event Action Changed;

    public static Mode Current
    {
        get
        {
            if (!loaded)
            {
                mode = (Mode)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, (int)Mode.Color), 0, 2);
                loaded = true;
            }
            return mode;
        }
        set
        {
            if (loaded && value == mode) return;
            mode = value;
            loaded = true;
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>Short pixel-font label for the pause screen selector.</summary>
    public static string Label(Mode m) => m switch
    {
        Mode.Numbers => "NUMBERS",
        Mode.Pips => "PIPS",
        _ => "COLOR",
    };
}
