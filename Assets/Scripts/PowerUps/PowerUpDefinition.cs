using UnityEngine;

/// <summary>Data for one power-up offered on level-up: texts, rarity, icon and what it does.</summary>
[CreateAssetMenu(fileName = "PowerUp", menuName = "BrickBreaker/Power-Up")]
public class PowerUpDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "Power-Up";
    [SerializeField, TextArea] private string description = "Does something nice.";
    [SerializeField] private PowerUpRarity rarity = PowerUpRarity.Common;
    [Tooltip("7 lines of 7 characters: # = pixel, . = empty.")]
    [SerializeField, TextArea(7, 7)] private string icon = "...#...\n..###..\n.#####.\n#######\n.#####.\n..###..\n...#...";
    [Tooltip("How many times it can be picked in one run. 0 = unlimited.")]
    [SerializeField, Min(0)] private int maxStacks;
    [SerializeField] private PowerUpEffect effect;

    public string DisplayName => displayName;
    public string Description => description;
    public PowerUpRarity Rarity => rarity;
    public string Icon => icon;
    public int MaxStacks => maxStacks;
    public PowerUpEffect Effect => effect;
}
