using UnityEngine;

/// <summary>
/// Maps remaining hit points to a color. Index 0 = 1 hit point (white), last = the toughest brick.
/// A damaged brick simply shows the color of its remaining hit points, so it fades toward white as it loses life.
/// Placeholder colors until the final visuals exist.
/// </summary>
[CreateAssetMenu(fileName = "BrickPalette", menuName = "BrickBreaker/Brick Palette")]
public class BrickPalette : ScriptableObject
{
    [Tooltip("Color per hit point. Element 0 = 1 HP, element 4 = 5 HP.")]
    [SerializeField] private Color[] hitPointColors =
    {
        new Color(1f, 1f, 1f),        // 1 HP - white
        new Color(0.72f, 0.90f, 1f),  // 2 HP
        new Color(0.40f, 0.70f, 1f),  // 3 HP
        new Color(0.20f, 0.40f, 0.90f), // 4 HP
        new Color(0.08f, 0.12f, 0.55f), // 5 HP - dark blue
    };

    public int MaxHitPoints => hitPointColors.Length;

    public Color GetColor(int hitPoints)
    {
        if (hitPointColors == null || hitPointColors.Length == 0) return Color.white;
        return hitPointColors[Mathf.Clamp(hitPoints - 1, 0, hitPointColors.Length - 1)];
    }
}
