using System;
using UnityEngine;

/// <summary>
/// One live buff zone: a lit stretch of a side wall that gives its <see cref="WallBuff"/> to the ball that bounces
/// inside it. It fades out and expires after its lifetime. Created by <see cref="WallBuffZoneSpawner"/>.
/// The camera sees the inner face of a side wall almost edge-on, so the look leans on what it sees well: the wall
/// top (a crisp neon line and a glow over the trim), the floor (a light band at the wall base and chevrons that
/// flow toward the wall, "bounce here"), and two light posts rising at the zone ends like a gate.
/// Spawn: the posts shoot up and the line draws out from the middle. Taken: everything flashes white and the light
/// arcs into the ball. Missed: the zone shrinks to its middle and fizzles.
/// </summary>
public class WallBuffZone : MonoBehaviour
{
    private const float GrowTime = 0.4f;
    private const float WarningTime = 3f;
    private const float TakenTime = 0.35f;
    private const float ExpireTime = 0.5f;

    private const float PostHeight = 1.3f;   // above the wall top
    private const float LineWidth = 0.09f;
    private const float PostWidth = 0.12f;
    private const int ChevronRows = 3;       // along the zone
    private const int ChevronWaves = 2;      // chevrons in flight per row
    private const float ChevronSize = 0.22f;
    private const float ChevronFar = 1.7f;   // distance from the wall where a chevron appears
    private const float ChevronNear = 0.25f; // ... and where it fades into the wall
    private const float ChevronSpeed = 0.7f; // trips per second

    /// <summary>Raised once when the zone ends; the argument is true if a ball took the buff.</summary>
    public event Action<WallBuffZone, bool> Ended;

    private Wall wall;
    private WallZone zone;
    private WallBuff buff;
    private Vector3 inward;
    private Vector3 center;   // middle of the inner face
    private float innerX;
    private float topY;
    private float length;
    private float lifetime;
    private float age;
    private float flow;       // chevron travel, speeds up near the end
    private bool consumed;
    private bool ending;
    private bool taken;
    private float endTimer;
    private float nextEmber;

    private MeshRenderer strip;
    private MeshRenderer core;
    private MeshRenderer topGlow;
    private MeshRenderer floorPatch;
    private LineRenderer topLine;
    private LineRenderer floorLine;
    private LineRenderer[] posts;
    private LineRenderer[] chevrons;

    public static WallBuffZone Create(Wall wall, WallBuff buff, float t, float length, float lifetime)
    {
        var go = new GameObject("WallBuffZone");
        var zone = go.AddComponent<WallBuffZone>();
        zone.Build(wall, buff, t, length, lifetime);
        return zone;
    }

    private void Build(Wall targetWall, WallBuff targetBuff, float t, float zoneLength, float zoneLifetime)
    {
        wall = targetWall;
        buff = targetBuff;
        length = zoneLength;
        lifetime = zoneLifetime;

        // Inner face of the wall, facing the middle of the playfield.
        Bounds bounds = wall.WorldBounds;
        float side = Mathf.Sign(bounds.center.x);
        inward = new Vector3(-side, 0f, 0f);
        innerX = bounds.center.x + inward.x * bounds.extents.x;
        topY = bounds.max.y;
        center = wall.GetWorldPoint(t);
        center.x = innerX;
        center.y = bounds.center.y;

        transform.SetPositionAndRotation(center, Quaternion.LookRotation(-inward));

        float half = zoneLength * 0.5f / Mathf.Max(0.01f, wall.WorldLength);
        zone = new WallZone { name = "Buff", start = t - half, end = t + half, gizmoColor = buff.color };
        zone.onBallHit.AddListener(HandleBallHit);
        wall.AddZone(zone);

        // Inner face (seen at a grazing angle, kept as a bonus). A thin offset keeps it from z-fighting the wall.
        strip = NeonGlow.CreateQuad(transform, "Strip", 1f);
        strip.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        core = NeonGlow.CreateQuad(transform, "Core", 1f);
        core.transform.localPosition = new Vector3(0f, 0f, 0.01f);

        // Wall top: a glow over the trim, facing up.
        topGlow = NeonGlow.CreateQuad(transform, "TopGlow", 1f);
        topGlow.transform.SetPositionAndRotation(new Vector3(bounds.center.x, topY + 0.015f, center.z), Quaternion.Euler(90f, 0f, 0f));

        floorPatch = NeonGlow.CreateQuad(transform, "FloorPatch", 1f);
        floorPatch.transform.SetPositionAndRotation(
            new Vector3(innerX + inward.x * 0.7f, 0.03f, center.z), Quaternion.Euler(90f, 0f, 0f));

        topLine = CreateLine("TopLine", 2, LineWidth);
        floorLine = CreateLine("FloorLine", 2, LineWidth);
        posts = new[] { CreateLine("PostA", 2, PostWidth), CreateLine("PostB", 2, PostWidth) };
        foreach (LineRenderer post in posts)
            post.colorGradient = Fade(new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f));
        chevrons = new LineRenderer[ChevronRows * ChevronWaves];
        for (int i = 0; i < chevrons.Length; i++) chevrons[i] = CreateLine("Chevron" + i, 3, LineWidth * 0.85f);

        // Arrival: a ring and flash, sparks shooting up where the posts rise, and the beacon sound.
        Vector3 mid = new(innerX + inward.x * 0.3f, topY, center.z);
        NeonFx.Ring(mid, buff.color, 24, 3f, 0.08f, 0.3f);
        NeonFx.Flash(mid, buff.color, 1.1f, 0.22f);
        for (int i = -1; i <= 1; i += 2)
            NeonFx.Sparks(new Vector3(innerX, topY, center.z + i * length * 0.5f), Vector3.up, NeonGlow.Hdr(buff.color, 1.5f), 8, 5f, 20f, 0.07f, 0.35f);
        SoundManager.Play(Sfx.ZoneSpawn);
        ApplyVisuals(0f, 0f, 0f, 0f);
    }

    private LineRenderer CreateLine(string lineName, int points, float width)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = NeonGlow.LineMaterial;
        line.useWorldSpace = true;
        line.positionCount = points;
        line.widthMultiplier = width;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;
        return line;
    }

    private static Gradient Fade(params GradientAlphaKey[] alphas)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas);
        return gradient;
    }

    private void HandleBallHit(BallController ball)
    {
        if (consumed || ending) return;
        consumed = true;

        BallBuffAura.Give(ball, buff);

        // The light leaves the wall and arcs into the ball: two bolts from the top line, sparks along the zone.
        Vector3 at = new(innerX + inward.x * 0.3f, ball.Position.y, ball.Position.z);
        Color hot = NeonGlow.Hdr(Color.Lerp(buff.color, Color.white, 0.35f), 2f);
        float halfLength = length * 0.5f;
        ChainBolt.Spawn(new Vector3(innerX, topY, center.z - halfLength * 0.6f), ball.Position, hot, 0.18f, 0.22f, 0.14f);
        ChainBolt.Spawn(new Vector3(innerX, topY, center.z + halfLength * 0.6f), ball.Position, hot, 0.18f, 0.22f, 0.14f);
        NeonFx.Line(new Vector3(innerX, topY, center.z - halfLength), new Vector3(innerX, topY, center.z + halfLength),
            inward, NeonGlow.Hdr(buff.color, 1.5f), 30, 3f, 0.09f, 0.4f);
        NeonFx.Burst(at, buff.color, 30, 4.5f, 0.12f, 0.5f);
        NeonFx.Ring(ball.Position, Color.Lerp(buff.color, Color.white, 0.4f), 30, 5f, 0.1f, 0.35f);
        NeonFx.Flash(ball.Position, buff.color, 1.6f, 0.25f);
        SoundManager.Play(Sfx.ZoneTake);
        Finish(true);
    }

    private void Finish(bool tookBuff)
    {
        if (ending) return;
        ending = true;
        taken = tookBuff;
        // Do not remove the zone from the wall while it is iterating its zones (we are in a hit callback).
        Ended?.Invoke(this, tookBuff);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (ending)
        {
            endTimer += dt;
            float duration = taken ? TakenTime : ExpireTime;
            float k = Mathf.Clamp01(endTimer / duration);
            if (taken)
                // Flash white, then fade; the posts collapse.
                ApplyVisuals(1f - k, 1f - k, 1f - k, (1f - k) * (1f - k));
            else
                // Shrink to the middle while dimming, the posts drop.
                ApplyVisuals(1f - k * k, 1f - k, 1f - k, 0f);
            if (endTimer >= duration) Destroy(gameObject);
            return;
        }

        age += dt;
        if (age >= lifetime)
        {
            NeonFx.Line(new Vector3(innerX, topY, center.z - length * 0.5f), new Vector3(innerX, topY, center.z + length * 0.5f),
                Vector3.up, NeonGlow.Hdr(buff.color, 0.8f), 16, 1.2f, 0.07f, 0.5f);
            NeonFx.Embers(new Vector3(innerX, topY, center.z), buff.color, 6, 0.08f, 0.6f);
            SoundManager.Play(Sfx.ZoneExpire);
            Finish(false);
            return;
        }

        float remaining = lifetime - age;
        float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / GrowTime));
        float blink = 1f;
        float urgency = 0f;
        if (remaining < WarningTime)
        {
            // Blinks faster as the end gets closer, and the chevrons hurry.
            urgency = 1f - remaining / WarningTime;
            float speed = Mathf.Lerp(6f, 18f, urgency);
            blink = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(age * speed));
        }
        flow += dt * ChevronSpeed * (1f + urgency);
        float pulse = 0.85f + 0.15f * Mathf.Sin(age * 5f);
        ApplyVisuals(grow, grow * blink * pulse, EaseOutBack(Mathf.Clamp01(age / GrowTime)), 0f);

        if (Time.time >= nextEmber && Time.timeScale > 0f)
        {
            nextEmber = Time.time + 0.1f;
            var p = new Vector3(innerX, topY + 0.05f, center.z + UnityEngine.Random.Range(-0.5f, 0.5f) * length * grow);
            NeonFx.Embers(p, buff.color, 1, 0.07f, 0.8f);
        }
    }

    /// <param name="span">Share of the full length shown (grows in from the middle, shrinks back on expiry).</param>
    /// <param name="strength">Overall brightness.</param>
    /// <param name="postRise">Post height, 0 to 1 (may overshoot while rising).</param>
    /// <param name="white">Flash toward white (zone taken).</param>
    private void ApplyVisuals(float span, float strength, float postRise, float white)
    {
        float len = Mathf.Max(0.01f, length * span);
        float halfLength = len * 0.5f;
        Color tint = Color.Lerp(buff.color, Color.white, white * 0.8f);
        float flash = 1f + white * 1.5f;

        strip.transform.localScale = new Vector3(len, 1.05f, 1f);
        core.transform.localScale = new Vector3(len * 0.8f, 0.35f, 1f);
        topGlow.transform.localScale = new Vector3(0.9f, len * 1.1f, 1f);
        floorPatch.transform.localScale = new Vector3(2.4f, len * 1.15f, 1f);

        NeonGlow.SetColor(strip, With(NeonGlow.Hdr(tint, 1.4f * flash), strength * 0.55f));
        NeonGlow.SetColor(core, With(NeonGlow.Hdr(tint, 3f * flash), strength));
        NeonGlow.SetColor(topGlow, With(NeonGlow.Hdr(tint, 2f * flash), strength * 0.8f));
        NeonGlow.SetColor(floorPatch, With(NeonGlow.Hdr(tint, 1.6f * flash), strength * 0.7f));

        // Crisp neon lines: along the inner top edge and at the wall base on the floor.
        float z0 = center.z - halfLength, z1 = center.z + halfLength;
        topLine.SetPosition(0, new Vector3(innerX, topY + 0.02f, z0));
        topLine.SetPosition(1, new Vector3(innerX, topY + 0.02f, z1));
        float floorX = innerX + inward.x * 0.08f;
        floorLine.SetPosition(0, new Vector3(floorX, 0.04f, z0));
        floorLine.SetPosition(1, new Vector3(floorX, 0.04f, z1));
        SetLineColor(topLine, With(NeonGlow.Hdr(tint, 4f * flash), strength));
        SetLineColor(floorLine, With(NeonGlow.Hdr(tint, 2.2f * flash), strength * 0.8f));

        // Gate posts at both ends: rise with a small overshoot.
        float rise = Mathf.Max(0f, postRise);
        for (int i = 0; i < posts.Length; i++)
        {
            float z = i == 0 ? z0 : z1;
            posts[i].SetPosition(0, new Vector3(innerX, 0.02f, z));
            posts[i].SetPosition(1, new Vector3(innerX, topY + PostHeight * Mathf.Max(0.01f, rise), z));
            SetLineColor(posts[i], With(NeonGlow.Hdr(tint, 3.5f * flash), strength * Mathf.Clamp01(rise * 2f)));
        }

        // Chevrons on the floor, sliding toward the wall and fading in and out on the way.
        for (int i = 0; i < chevrons.Length; i++)
        {
            int row = i % ChevronRows;
            int wave = i / ChevronRows;
            float trip = Mathf.Repeat(flow + (float)wave / ChevronWaves + row * 0.17f, 1f);
            float distance = Mathf.Lerp(ChevronFar, ChevronNear, trip);
            float fade = Mathf.Sin(trip * Mathf.PI);
            float z = center.z + (row - (ChevronRows - 1) * 0.5f) * (len / ChevronRows);
            float tipX = innerX + inward.x * distance;
            float armX = tipX + inward.x * ChevronSize;
            float arm = ChevronSize * Mathf.Clamp01(span);
            LineRenderer chevron = chevrons[i];
            chevron.SetPosition(0, new Vector3(armX, 0.04f, z - arm));
            chevron.SetPosition(1, new Vector3(tipX, 0.04f, z));
            chevron.SetPosition(2, new Vector3(armX, 0.04f, z + arm));
            SetLineColor(chevron, With(NeonGlow.Hdr(tint, 2f * flash), strength * fade * 0.85f));
        }
    }

    private static void SetLineColor(LineRenderer line, Color color)
    {
        // The line material is additive: scale the color too, so a faded line really goes dark.
        NeonGlow.SetColor(line, new Color(color.r * color.a, color.g * color.a, color.b * color.a, color.a));
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private static Color With(Color c, float alpha) => new(c.r, c.g, c.b, Mathf.Clamp01(alpha));

    private void OnDestroy()
    {
        if (wall != null && zone != null) wall.RemoveZone(zone);
    }
}
