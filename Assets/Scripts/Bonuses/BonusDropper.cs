using System;
using System.Collections.Generic;
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

    /// <summary>Raised when a destroyed brick drops a bonus item (run profiler).</summary>
    public event Action<BonusDefinition> BonusDropped;

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
        if (PowerUpStats.BonusDropBonus > 0f) // Greed
            chance = Mathf.Min(chance + PowerUpStats.BonusDropBonus, Mathf.Max(chance, PowerUpStats.MaxDropChance));
        if (UnityEngine.Random.value > chance) return;

        BonusDefinition definition = PickBonus();
        if (definition == null || itemPrefab == null) return;

        Vector3 position = brick.transform.position;
        position.y = itemPrefab.transform.position.y;
        BonusItem item = Instantiate(itemPrefab, position, Quaternion.identity);
        item.Initialize(definition);
        item.Collected += HandleItemCollected;
        BonusDropped?.Invoke(definition);
    }

    /// <summary>Spawns one item of every bonus in a row near the back of the field, to test them without waiting for drops.</summary>
    [ContextMenu("Debug: Drop Every Bonus")]
    private void DebugDropEveryBonus()
    {
        if (itemPrefab == null || bonuses == null) return;
        for (int i = 0; i < bonuses.Length; i++)
        {
            if (bonuses[i] == null) continue;
            var position = new Vector3((i - (bonuses.Length - 1) * 0.5f) * 1.5f, itemPrefab.transform.position.y, 5f);
            BonusItem item = Instantiate(itemPrefab, position, Quaternion.identity);
            item.Initialize(bonuses[i]);
            item.Collected += HandleItemCollected;
        }
    }

    private void HandleItemCollected(BonusItem item, PaddleController paddle)
    {
        BonusDefinition definition = item.Definition;

        // The bonus's own pickup cue; each effect then adds its own sound on top (shield hum, laser charge...).
        Vector3 at = item.transform.position;
        SoundManager.Play(definition.PickupSound);
        NeonFx.Flash(at, definition.Color, 0.8f, 0.14f);
        NeonFx.Ring(at, definition.Color, 32, 6f);
        NeonFx.Burst(at, Color.Lerp(definition.Color, Color.white, 0.4f), 16, 3f, 0.08f, 0.4f);

        if (definition.Effect != null)
        {
            // Copy the list: effects such as multi-ball add balls while they run.
            definition.Effect.Apply(new BonusContext(paddle, new List<BallController>(BallController.Active)));
        }
        BonusCollected?.Invoke(definition, paddle);
    }

    private BonusDefinition PickBonus()
    {
        if (bonuses == null || bonuses.Length == 0) return null;

        float total = 0f;
        foreach (BonusDefinition bonus in bonuses)
            if (IsDroppable(bonus)) total += bonus.DropWeight;
        if (total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;
        BonusDefinition last = null;
        foreach (BonusDefinition bonus in bonuses)
        {
            if (!IsDroppable(bonus)) continue;
            last = bonus;
            roll -= bonus.DropWeight;
            if (roll <= 0f) return bonus;
        }
        return last;
    }

    // A bonus whose effect says it cannot drop right now (e.g. a shield is already up) is left out of the roll.
    private static bool IsDroppable(BonusDefinition bonus) =>
        bonus != null && (bonus.Effect == null || bonus.Effect.CanDrop);
}
