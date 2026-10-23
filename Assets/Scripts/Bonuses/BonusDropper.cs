using System;
using UnityEngine;

/// <summary>
/// Listens to <see cref="BrickGrid.BrickDestroyed"/>, rolls for a drop and spawns a falling <see cref="BonusItem"/>.
/// When the paddle catches it, applies the bonus effect and raises <see cref="BonusCollected"/>.
/// </summary>
public class BonusDropper : MonoBehaviour
{
    [SerializeField] private BrickGrid grid;
    [SerializeField] private BonusItem itemPrefab;
    [SerializeField] private BonusDefinition[] bonuses;

    [Header("Drop Chance")]
    [Tooltip("Base chance that a destroyed brick drops a bonus.")]
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.15f;
    [Tooltip("Extra chance per hit point above 1, so tougher bricks drop more often.")]
    [SerializeField, Range(0f, 0.3f)] private float extraChancePerHitPoint = 0.04f;

    /// <summary>Raised after a bonus was caught and its effect applied.</summary>
    public event Action<BonusDefinition, PaddleController> BonusCollected;

    private void OnEnable()
    {
        if (grid != null) grid.BrickDestroyed += HandleBrickDestroyed;
    }

    private void OnDisable()
    {
        if (grid != null) grid.BrickDestroyed -= HandleBrickDestroyed;
    }

    private void HandleBrickDestroyed(Brick brick)
    {
        float chance = dropChance + (brick.MaxHitPoints - 1) * extraChancePerHitPoint;
        if (UnityEngine.Random.value > chance) return;

        BonusDefinition definition = PickBonus();
        if (definition == null || itemPrefab == null) return;

        Vector3 position = brick.transform.position;
        position.y = itemPrefab.transform.position.y;
        BonusItem item = Instantiate(itemPrefab, position, Quaternion.identity);
        item.Initialize(definition);
        item.Collected += HandleItemCollected;
    }

    private void HandleItemCollected(BonusItem item, PaddleController paddle)
    {
        BonusDefinition definition = item.Definition;
        if (definition.Effect != null)
        {
            BallController[] balls = FindObjectsByType<BallController>();
            definition.Effect.Apply(new BonusContext(paddle, balls));
        }
        BonusCollected?.Invoke(definition, paddle);
    }

    private BonusDefinition PickBonus()
    {
        if (bonuses == null || bonuses.Length == 0) return null;

        float total = 0f;
        foreach (BonusDefinition bonus in bonuses)
            if (bonus != null) total += bonus.DropWeight;
        if (total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;
        foreach (BonusDefinition bonus in bonuses)
        {
            if (bonus == null) continue;
            roll -= bonus.DropWeight;
            if (roll <= 0f) return bonus;
        }
        return bonuses[bonuses.Length - 1];
    }
}
