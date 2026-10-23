using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A segment of a wall, defined by a normalized range along its length (0 = start, 1 = end).
/// Hook up <see cref="onBallHit"/> in the Inspector, or subclass <see cref="Wall"/> for custom logic.
/// </summary>
[Serializable]
public class WallZone
{
    public string name = "Zone";
    [Range(0f, 1f)] public float start = 0f;
    [Range(0f, 1f)] public float end = 0.5f;
    public Color gizmoColor = Color.cyan;
    [Tooltip("Invoked when the ball bounces inside this zone.")]
    public UnityEvent<BallController> onBallHit;

    public bool Contains(float t) => t >= start && t <= end;
}
