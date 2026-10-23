using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Basic wall: lets the default arcade bounce happen untouched (speed never changes).
/// Optionally defines zones along its length; when the ball hits a zone its event fires and
/// <see cref="OnZoneHit"/> is called, so special effects can be added later without touching the ball.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class Wall : BallSurface
{
    public enum LengthAxis { X, Z }

    [Tooltip("Local axis running along the wall's length (Z for side walls, X for the back wall).")]
    [SerializeField] private LengthAxis lengthAxis = LengthAxis.Z;
    [SerializeField] private List<WallZone> zones = new();

    private BoxCollider box;

    private BoxCollider Box
    {
        get
        {
            if (box == null) box = GetComponent<BoxCollider>();
            return box;
        }
    }

    public override void OnBallHit(BallController ball, Collision collision)
    {
        SoundManager.Play(Sfx.WallBounce);
        if (zones.Count == 0) return;

        float t = GetNormalizedPosition(collision.GetContact(0).point);
        foreach (WallZone zone in zones)
        {
            if (!zone.Contains(t)) continue;
            zone.onBallHit?.Invoke(ball);
            OnZoneHit(zone, ball, collision, t);
        }
    }

    /// <summary>Override in a subclass for custom zone effects (change direction, boost, spawn FX...).</summary>
    protected virtual void OnZoneHit(WallZone zone, BallController ball, Collision collision, float t) { }

    /// <summary>0..1 position of a world point along the wall's length axis.</summary>
    public float GetNormalizedPosition(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        int axis = lengthAxis == LengthAxis.X ? 0 : 2;
        float size = Box.size[axis];
        return size > 0f ? Mathf.Clamp01((local[axis] - Box.center[axis]) / size + 0.5f) : 0.5f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        BoxCollider b = Box;
        int axis = lengthAxis == LengthAxis.X ? 0 : 2;
        Gizmos.matrix = transform.localToWorldMatrix;
        foreach (WallZone zone in zones)
        {
            Vector3 size = b.size * 1.02f;
            Vector3 center = b.center;
            float length = b.size[axis];
            size[axis] = (zone.end - zone.start) * length;
            center[axis] = b.center[axis] - length * 0.5f + (zone.start + zone.end) * 0.5f * length;
            Gizmos.color = zone.gizmoColor;
            Gizmos.DrawWireCube(center, size);
        }
    }
#endif
}
