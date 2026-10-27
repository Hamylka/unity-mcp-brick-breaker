using System;
using UnityEngine;

/// <summary>
/// One live buff zone: a glowing strip on a side wall (plus a light patch on the floor) that gives its
/// <see cref="WallBuff"/> to the ball that bounces inside it. It fades out and expires after its lifetime.
/// Created by <see cref="WallBuffZoneSpawner"/>. Visuals are placeholders (glow quads and embers).
/// </summary>
public class WallBuffZone : MonoBehaviour
{
    private const float GrowTime = 0.4f;
    private const float WarningTime = 3f;

    /// <summary>Raised once when the zone ends; the argument is true if a ball took the buff.</summary>
    public event Action<WallBuffZone, bool> Ended;

    private Wall wall;
    private WallZone zone;
    private WallBuff buff;
    private Vector3 inward;
    private float length;
    private float lifetime;
    private float age;
    private bool consumed;
    private bool ending;
    private float endTimer;
    private float nextEmber;

    private MeshRenderer strip;
    private MeshRenderer floorPatch;
    private MeshRenderer core;

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
        Vector3 center = wall.GetWorldPoint(t);
        center.x = bounds.center.x + inward.x * bounds.extents.x;
        center.y = bounds.center.y;

        transform.SetPositionAndRotation(center, Quaternion.LookRotation(-inward));

        float half = zoneLength * 0.5f / Mathf.Max(0.01f, wall.WorldLength);
        zone = new WallZone { name = "Buff", start = t - half, end = t + half, gizmoColor = buff.color };
        zone.onBallHit.AddListener(HandleBallHit);
        wall.AddZone(zone);

        // A thin offset keeps the quads from z-fighting with the wall mesh.
        strip = NeonGlow.CreateQuad(transform, "Strip", 1f);
        strip.transform.localPosition = new Vector3(0f, 0f, 0.02f);

        core = NeonGlow.CreateQuad(transform, "Core", 1f);
        core.transform.localPosition = new Vector3(0f, 0f, 0.01f);

        floorPatch = NeonGlow.CreateQuad(transform, "FloorPatch", 1f);
        floorPatch.transform.SetPositionAndRotation(
            new Vector3(center.x + inward.x * 0.7f, 0.03f, center.z), Quaternion.Euler(90f, 0f, 0f));

        NeonFx.Ring(center + inward * 0.3f, buff.color, 24, 3f, 0.08f, 0.3f);
        NeonFx.Flash(center + inward * 0.3f, buff.color, 0.9f, 0.2f);
        SoundManager.Play(Sfx.ShieldUp, 1.4f, 0.5f);
        ApplyVisuals(0f);
    }

    private void HandleBallHit(BallController ball)
    {
        if (consumed || ending) return;
        consumed = true;

        BallBuffAura.Give(ball, buff);
        Vector3 at = transform.position + inward * 0.3f;
        NeonFx.Burst(at, buff.color, 30, 4.5f, 0.12f, 0.5f);
        NeonFx.Ring(at, Color.Lerp(buff.color, Color.white, 0.4f), 30, 5f, 0.1f, 0.35f);
        NeonFx.Flash(at, buff.color, 1.4f, 0.25f);
        SoundManager.Play(Sfx.BonusPickup, 0.9f, 0.9f);
        Finish(true);
    }

    private void Finish(bool tookBuff)
    {
        if (ending) return;
        ending = true;
        // Do not remove the zone from the wall while it is iterating its zones (we are in a hit callback).
        Ended?.Invoke(this, tookBuff);
    }

    private void Update()
    {
        if (ending)
        {
            endTimer += Time.deltaTime;
            ApplyVisuals(Mathf.Max(0f, 1f - endTimer / 0.25f));
            if (endTimer >= 0.25f) Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        if (age >= lifetime)
        {
            NeonFx.Embers(transform.position + inward * 0.2f, buff.color, 6, 0.08f, 0.6f);
            SoundManager.Play(Sfx.BonusExpire, 0.8f, 0.5f);
            Finish(false);
            return;
        }

        float remaining = lifetime - age;
        float grow = Mathf.Clamp01(age / GrowTime);
        float blink = 1f;
        if (remaining < WarningTime)
        {
            // Blinks faster as the end gets closer.
            float speed = Mathf.Lerp(6f, 18f, 1f - remaining / WarningTime);
            blink = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(age * speed));
        }
        float pulse = 0.85f + 0.15f * Mathf.Sin(age * 5f);
        ApplyVisuals(grow * blink * pulse);

        if (Time.time >= nextEmber && Time.timeScale > 0f)
        {
            nextEmber = Time.time + 0.12f;
            Vector3 p = transform.position + Vector3.forward * UnityEngine.Random.Range(-length, length) * 0.5f
                        + inward * 0.15f + Vector3.down * 0.4f;
            NeonFx.Embers(p, buff.color, 1, 0.07f, 0.8f);
        }
    }

    private void ApplyVisuals(float strength)
    {
        float grow = Mathf.Clamp01(age / GrowTime);
        float len = Mathf.Max(0.01f, length * Mathf.SmoothStep(0f, 1f, grow));

        strip.transform.localScale = new Vector3(len, 1.05f, 1f);
        core.transform.localScale = new Vector3(len * 0.8f, 0.35f, 1f);
        floorPatch.transform.localScale = new Vector3(1.8f, len * 1.15f, 1f);

        NeonGlow.SetColor(strip, With(NeonGlow.Hdr(buff.color, 1.4f), strength * 0.55f));
        NeonGlow.SetColor(core, With(NeonGlow.Hdr(buff.color, 3f), strength));
        NeonGlow.SetColor(floorPatch, With(NeonGlow.Hdr(buff.color, 1.6f), strength * 0.7f));
    }

    private static Color With(Color c, float alpha) => new(c.r, c.g, c.b, Mathf.Clamp01(alpha));

    private void OnDestroy()
    {
        if (wall != null && zone != null) wall.RemoveZone(zone);
    }
}
