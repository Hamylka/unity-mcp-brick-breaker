using UnityEngine;

/// <summary>Data for one kind of droppable bonus: look, drop weight and ability.</summary>
[CreateAssetMenu(fileName = "Bonus", menuName = "BrickBreaker/Bonus Definition")]
public class BonusDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "Bonus";
    [SerializeField] private Color color = Color.cyan;
    [Tooltip("Relative chance of being picked when a drop happens.")]
    [SerializeField, Min(0f)] private float dropWeight = 1f;
    [SerializeField] private BonusEffect effect;
    [Tooltip("Played when the paddle catches it. The effect may add its own sound on top (laser charge, shield hum...).")]
    [SerializeField] private Sfx pickupSound = Sfx.BonusPickup;

    public string DisplayName => displayName;
    public Color Color => color;
    public float DropWeight => dropWeight;
    public BonusEffect Effect => effect;
    public Sfx PickupSound => pickupSound;
}
