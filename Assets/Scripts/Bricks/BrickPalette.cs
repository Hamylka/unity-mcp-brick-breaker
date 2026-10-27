using UnityEngine;

/// <summary>
/// Maps a brick's remaining hit points to a color. The color is a ratio, not a fixed tier: the weakest possible
/// brick (1 HP) is the start of the gradient (white) and the toughest brick currently spawning is the end (hot red),
/// so the color always reads as "how tough compared to the toughest brick right now".
/// A damaged brick shows the color of its remaining hit points, so it fades toward white as it loses life.
/// </summary>
[CreateAssetMenu(fileName = "BrickPalette", menuName = "BrickBreaker/Brick Palette")]
public class BrickPalette : ScriptableObject
{
    [Tooltip("Left = 1 HP, right = the toughest brick currently spawning.")]
    [SerializeField] private Gradient gradient = CreateDefaultGradient();

    /// <param name="hitPoints">Remaining hit points of the brick.</param>
    /// <param name="scaleMaxHitPoints">The toughest brick currently spawning (the end of the gradient).</param>
    public Color GetColor(int hitPoints, int scaleMaxHitPoints) =>
        gradient != null ? gradient.Evaluate(GetToughness(hitPoints, scaleMaxHitPoints)) : Color.white;

    /// <summary>0 = 1 HP, 1 = the toughest brick currently spawning. Drives the color and the brick's fill and glow.</summary>
    public static float GetToughness(int hitPoints, int scaleMaxHitPoints) =>
        scaleMaxHitPoints <= 1 ? 0f : Mathf.Clamp01((hitPoints - 1f) / (scaleMaxHitPoints - 1f));

    // A "heat" ramp across the hue wheel, so every HP step of the starting wall (1 to 5) is its own color:
    // white, cyan, lime, amber, hot red. Pale and cool reads as "almost gone", hot and saturated as "heavy".
    private static Gradient CreateDefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.15f, 0.95f, 1f), 0.25f),
                new GradientColorKey(new Color(0.35f, 1f, 0.25f), 0.5f),
                new GradientColorKey(new Color(1f, 0.6f, 0.08f), 0.75f),
                new GradientColorKey(new Color(1f, 0.12f, 0.3f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
