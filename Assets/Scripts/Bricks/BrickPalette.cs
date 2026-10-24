using UnityEngine;

/// <summary>
/// Maps a brick's remaining hit points to a color. The color is a ratio, not a fixed tier: the weakest possible
/// brick (1 HP) is the start of the gradient (white) and the toughest brick currently spawning is the end (dark blue),
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
    public Color GetColor(int hitPoints, int scaleMaxHitPoints)
    {
        if (gradient == null) return Color.white;
        float t = scaleMaxHitPoints <= 1 ? 0f : Mathf.Clamp01((hitPoints - 1f) / (scaleMaxHitPoints - 1f));
        return gradient.Evaluate(t);
    }

    private static Gradient CreateDefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.72f, 0.90f, 1f), 0.25f),
                new GradientColorKey(new Color(0.40f, 0.70f, 1f), 0.5f),
                new GradientColorKey(new Color(0.20f, 0.40f, 0.90f), 0.75f),
                new GradientColorKey(new Color(0.08f, 0.12f, 0.55f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
