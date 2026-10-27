using System;
using UnityEngine;

/// <summary>
/// Player setting: how brick HP is shown on top of the color ramp. Saved between runs (PlayerPrefs),
/// changed from the pause screen. Every brick listens to <see cref="Changed"/> and updates its label.
/// </summary>
public static class BrickHpDisplay
{
    // The int values are what PlayerPrefs stores: never renumber them, or saved choices would change meaning.
    // The order shown to the player is Order, not the enum order.
    public enum Mode
    {
        /// <summary>Color, fill and glow only (no label).</summary>
        Color = 0,
        /// <summary>The hits left as a pixel number over the brick.</summary>
        Numbers = 1,
        /// <summary>One pip per hit left (2 to 5), a skull above that.</summary>
        Pips = 2,
    }

    /// <summary>The modes in the order the pause screen lists them.</summary>
    public static readonly Mode[] Order = { Mode.Pips, Mode.Color, Mode.Numbers };

    /// <summary>Used until the player picks something.</summary>
    public const Mode Default = Mode.Pips;

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
                int saved = PlayerPrefs.GetInt(PrefKey, (int)Default);
                mode = System.Enum.IsDefined(typeof(Mode), saved) ? (Mode)saved : Default;
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
