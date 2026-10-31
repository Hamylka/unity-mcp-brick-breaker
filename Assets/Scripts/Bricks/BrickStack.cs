using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A column of bricks on one cell. It owns the only collider, which stays on the ground layer,
/// so the ball can only ever hit the bottom brick. When the bottom brick is destroyed,
/// the bricks above it fall one layer, in a quick top-to-bottom ripple.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class BrickStack : BallSurface
{
    [Header("Collapse Animation")]
    [Tooltip("Seconds for a brick to drop one layer.")]
    [SerializeField, Min(0.01f)] private float fallDuration = 0.22f;
    [Tooltip("Extra delay per layer above the destroyed one, for a ripple effect.")]
    [SerializeField, Min(0f)] private float staggerDelay = 0.04f;

    private readonly List<Brick> bricks = new();
    private BoxCollider box;
    private float layerHeight = 1f;
    private float depth = 0.5f;

    public int Count => bricks.Count;
    public Brick Bottom => bricks.Count > 0 ? bricks[0] : null;

    /// <summary>Z of the player-facing edge, relative to the grid (add the grid's advance offset for world Z).</summary>
    public float LocalFrontZ => transform.localPosition.z - depth * 0.5f;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
    }

    /// <summary>Sizes the collider to the footprint of the stack, at ground level.</summary>
    public void Setup(float width, float depth, float brickLayerHeight)
    {
        if (box == null) box = GetComponent<BoxCollider>();
        layerHeight = brickLayerHeight;
        this.depth = depth;

        // Tall enough to always catch the ball, which rolls close to the floor.
        float height = Mathf.Max(layerHeight, 1f);
        box.size = new Vector3(width, height, depth);
        box.center = new Vector3(0f, height * 0.5f, 0f);
    }

    /// <summary>Adds a brick on top of the stack.</summary>
    public void AddBrick(Brick brick)
    {
        brick.transform.localPosition = new Vector3(0f, LayerCenterY(bricks.Count), 0f);
        brick.transform.localRotation = Quaternion.identity;
        brick.Destroyed += HandleBrickDestroyed;
        bricks.Add(brick);
    }

    public override void OnBallHit(BallController ball, Collision collision)
    {
        Brick bottom = Bottom;
        if (ball == null || bottom == null)
        {
            TakeDamage(1);
            return;
        }

        int hpBefore = bottom.HitPoints;
        int damage = PowerUpStats.BallHitDamage(ball, bottom);
        TakeDamage(damage);
        // Overkill and Echo carry what the brick really took, Executioner included.
        PowerUpStats.AfterBallHit(ball, bottom, PowerUpStats.WithExecutioner(bottom, damage, hpBefore), hpBefore);
    }

    /// <summary>Damages the bottom brick of the stack (ball hits, lasers, other abilities).</summary>
    public void TakeDamage(int damage)
    {
        Brick bottom = Bottom;
        if (bottom != null) bottom.TakeHit(damage);
    }

    private void HandleBrickDestroyed(Brick destroyed)
    {
        destroyed.Destroyed -= HandleBrickDestroyed;

        int index = bricks.IndexOf(destroyed);
        if (index < 0) return;
        bricks.RemoveAt(index);

        // Everything above the removed brick drops one layer, lowest first.
        for (int i = index; i < bricks.Count; i++)
            bricks[i].FallTo(LayerCenterY(i), fallDuration, staggerDelay * (i - index));

        if (bricks.Count == 0)
        {
            box.enabled = false;
            Destroy(gameObject, 0.5f); // lets the last brick finish its shrink animation
        }
    }

    private float LayerCenterY(int layer) => (layer + 0.5f) * layerHeight;
}
