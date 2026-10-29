using UnityEngine;

/// <summary>
/// Aiming guide of the <see cref="LaserCannon"/>: a faint beam from the paddle to whatever the next bolt will hit,
/// and four corner brackets framing the top of the targeted brick. The brackets close in on the brick and brighten
/// as the next shot charges, so the player sees both where and when the bolt lands. Owned by the cannon,
/// which feeds it every frame.
/// </summary>
public class LaserSight
{
    // Look. World units; the 640 px render shows about 45 px per unit, so 0.05 is a ~2 px line.
    private const float BeamWidth = 0.05f;
    private const float BracketWidth = 0.045f;
    private const float BracketArm = 0.16f;
    private const float BracketLift = 0.03f;       // above the brick top, so the brackets are not hidden in it
    private const float OpenScale = 1.7f;          // bracket frame size right after a shot (x the brick footprint)
    private const float LockedScale = 1.08f;       // frame size when the shot is ready
    private const float FollowSharpness = 30f;     // how fast the frame slides to a new target (1/s)
    private const float ReadyBlinkRate = 14f;      // blink of the locked frame in the last moment before a shot
    private const float ReadyBlinkTime = 0.2f;     // seconds before the shot when the blink starts
    private const float IdleDim = 0.45f;           // brightness while no ball is flying (the cannon holds fire)

    private readonly LineRenderer beam;
    private readonly LineRenderer[] corners = new LineRenderer[4];
    private readonly GameObject root;
    private Color color;

    private Brick lastTarget;
    private Vector3 frameCenter;
    private Vector2 frameHalf;
    private float appear; // 0 -> 1 fade in when the cannon starts

    public LaserSight(Transform parent, Material material, Color sightColor)
    {
        root = new GameObject("LaserSight");
        root.transform.SetParent(parent, false);
        beam = CreateLine(root.transform, "Beam", material, 2, BeamWidth);
        for (int i = 0; i < corners.Length; i++)
            corners[i] = CreateLine(root.transform, "Bracket" + i, material, 3, BracketWidth);
        SetColor(sightColor);
    }

    public void SetColor(Color sightColor) => color = sightColor;

    /// <summary>
    /// Places the guide for this frame.
    /// </summary>
    /// <param name="origin">Where the bolt starts.</param>
    /// <param name="end">Where the bolt stops (the hit point, or its max range).</param>
    /// <param name="target">The brick the bolt will damage, or null (a wall or nothing in the lane).</param>
    /// <param name="charge">0 right after a shot, 1 when the next one is ready.</param>
    /// <param name="secondsToShot">Time left before the next shot; the locked frame blinks just before it.</param>
    /// <param name="armed">False while no ball is flying (the cannon holds its fire): the guide dims.</param>
    public void Update(Vector3 origin, Vector3 end, Brick target, float charge, float secondsToShot, bool armed, float dt)
    {
        appear = Mathf.MoveTowards(appear, 1f, dt * 4f);
        float dim = appear * (armed ? 1f : IdleDim);
        float eased = 1f - (1f - charge) * (1f - charge); // ease-out: the frame closes in fast, then settles

        // Beam: faint at the paddle, stronger at the target, and brighter as the shot charges.
        float beamAlpha = Mathf.Lerp(0.15f, 0.45f, eased) * dim;
        beam.enabled = true;
        beam.SetPosition(0, origin);
        beam.SetPosition(1, end);
        beam.startColor = WithAlpha(color, beamAlpha * 0.35f);
        beam.endColor = WithAlpha(color, beamAlpha);

        if (target == null || !target.ShowsHpLabel)
        {
            SetCornersVisible(false);
            lastTarget = null;
            return;
        }

        // The frame slides to a new target (the paddle moved), but jumps on the first target.
        Vector3 size = target.Size;
        Vector3 center = target.transform.position + Vector3.up * (size.y * 0.5f + BracketLift);
        var half = new Vector2(size.x * 0.5f, size.z * 0.5f);
        if (lastTarget == null)
        {
            frameCenter = center;
            frameHalf = half;
        }
        else
        {
            float follow = 1f - Mathf.Exp(-FollowSharpness * dt);
            frameCenter = Vector3.Lerp(frameCenter, center, follow);
            frameHalf = Vector2.Lerp(frameHalf, half, follow);
        }
        lastTarget = target;

        float scale = Mathf.Lerp(OpenScale, LockedScale, eased);
        float alpha = Mathf.Lerp(0.35f, 1f, eased) * dim;
        if (armed && secondsToShot < ReadyBlinkTime) alpha *= 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * ReadyBlinkRate));

        SetCornersVisible(true);
        Color tint = WithAlpha(color, alpha);
        float hx = frameHalf.x * scale;
        float hz = frameHalf.y * scale;
        float arm = Mathf.Min(BracketArm, hx, hz);
        for (int i = 0; i < corners.Length; i++)
        {
            float sx = (i & 1) == 0 ? -1f : 1f;
            float sz = (i & 2) == 0 ? -1f : 1f;
            Vector3 corner = frameCenter + new Vector3(sx * hx, 0f, sz * hz);
            LineRenderer line = corners[i];
            line.SetPosition(0, corner - new Vector3(sx * arm, 0f, 0f));
            line.SetPosition(1, corner);
            line.SetPosition(2, corner - new Vector3(0f, 0f, sz * arm));
            line.startColor = tint;
            line.endColor = tint;
        }
    }

    public void Destroy()
    {
        if (root != null) Object.Destroy(root);
    }

    private void SetCornersVisible(bool visible)
    {
        foreach (LineRenderer line in corners) line.enabled = visible;
    }

    private static Color WithAlpha(Color c, float alpha)
    {
        c.a *= alpha;
        return c;
    }

    private static LineRenderer CreateLine(Transform parent, string name, Material material, int points, float width)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        var line = holder.AddComponent<LineRenderer>();
        line.positionCount = points;
        line.useWorldSpace = true;
        line.sharedMaterial = material;
        line.widthMultiplier = width;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false; // until the first Update places it
        return line;
    }
}
