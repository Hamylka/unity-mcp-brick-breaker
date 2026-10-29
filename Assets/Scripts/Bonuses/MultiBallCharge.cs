using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Multi-Ball telegraph: catching the bonus does not split the ball at once. A countdown runs on the main ball
/// (a ring closing in on it with a rising tick, glowing previews of the extras orbiting it and spreading out),
/// then the split waits until the ball heads toward the top of the screen, so the new balls fan out into the wall
/// instead of falling at the paddle. Catching more Multi-Balls during the countdown adds to the same split.
/// Builds itself (one object while a split is pending), no scene setup.
/// </summary>
public class MultiBallCharge : MonoBehaviour
{
    private const int RingPoints = 40;
    private const float RingWidth = 0.035f;
    private const float RingStartRadius = 4.5f;  // x ball radius, at the start of each tick
    private const float RingEndRadius = 1.7f;    // x ball radius, when the tick lands
    private const float GhostSize = 2.4f;        // x ball radius
    private const float GhostOrbitStart = 1.2f;  // x ball radius
    private const float GhostOrbitEnd = 2.6f;    // x ball radius

    private static MultiBallCharge current;

    private readonly List<string> procIds = new();
    private readonly List<MeshRenderer> ghosts = new();
    private MultiBallEffect effect;
    private LineRenderer ring;
    private Camera viewCamera;
    private float elapsed;
    private int ticksPlayed;
    private float spin;

    /// <summary>True while a split is counting down or waiting for the ball to head upfield.</summary>
    public static bool Pending => current != null;

    /// <summary>Queues one Multi-Ball pickup. It joins the split already counting down, if any.</summary>
    public static void Queue(MultiBallEffect effect, string procId)
    {
        if (current == null)
        {
            current = new GameObject("MultiBallCharge").AddComponent<MultiBallCharge>();
            current.Build(effect);
        }
        current.procIds.Add(procId);
    }

    private void Build(MultiBallEffect source)
    {
        effect = source;
        viewCamera = Camera.main;

        var ringObject = new GameObject("Ring");
        ringObject.transform.SetParent(transform, false);
        ring = ringObject.AddComponent<LineRenderer>();
        ring.positionCount = RingPoints;
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.sharedMaterial = NeonGlow.LineMaterial;
        ring.widthMultiplier = RingWidth;
        ring.numCornerVertices = 0;
        ring.numCapVertices = 0;
        ring.alignment = LineAlignment.View;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.enabled = false;
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }

    private void LateUpdate()
    {
        // Pending pickups keep a full ring on the HUD; the extras take over the same line when they spawn.
        foreach (string id in procIds) ActiveBonusTracker.Report(id, id, "MULTI-BALL", TemporaryBall.HudColor, 1f, 1f);

        BallController source = FindMainBall();
        if (source == null)
        {
            // No main ball flying (between a loss and a promotion, or the run is over): hold, and hide the look.
            SetVisible(false);
            return;
        }

        float interval = effect.TickInterval;
        float countdown = (effect.CountdownTicks + 0.5f) * interval; // a half beat after the last tick, then the split
        elapsed += Time.deltaTime; // scaled time: the countdown pauses with the game

        // The ring closes in over each interval and the tick lands with it, each one a step higher.
        while (ticksPlayed < effect.CountdownTicks && elapsed >= (ticksPlayed + 1) * interval)
        {
            ticksPlayed++;
            SoundManager.Play(Sfx.MultiBallTick, 1f + 0.12f * (ticksPlayed - 1));
            NeonFx.Ring(source.Position, TemporaryBall.HudColor, 10 + 4 * ticksPlayed, 2f, 0.06f, 0.2f);
        }

        if (elapsed >= countdown && source.Direction.z >= effect.MinUpward)
        {
            effect.Split(source, procIds);
            Destroy(gameObject);
            return;
        }

        bool waiting = ticksPlayed >= effect.CountdownTicks;
        UpdateLook(source, Mathf.Clamp01(elapsed / countdown), waiting, interval);
    }

    /// <summary>
    /// Ring: closes in on the ball once per tick, brighter on each one; once the count is done it stays tight and
    /// throbs fast while waiting for the ball to head up. Ghosts: one glowing preview per extra to come, orbiting
    /// faster and spreading out as the split nears.
    /// </summary>
    private void UpdateLook(BallController source, float progress, bool waiting, float interval)
    {
        SetVisible(true);
        Vector3 center = source.Position;
        float ballRadius = BallRadius(source);
        Color color = TemporaryBall.HudColor;

        float radius;
        float alpha;
        if (waiting)
        {
            float throb = 0.5f + 0.5f * Mathf.Sin(Time.time * 28f);
            radius = ballRadius * Mathf.Lerp(RingEndRadius, RingEndRadius * 1.2f, throb);
            alpha = Mathf.Lerp(0.6f, 1f, throb);
        }
        else
        {
            float tick = Mathf.Repeat(elapsed, interval) / interval;
            float eased = tick * tick * (3f - 2f * tick);
            radius = ballRadius * Mathf.Lerp(RingStartRadius, RingEndRadius, eased);
            alpha = Mathf.Lerp(0.15f, 0.55f + 0.45f * progress, eased);
        }
        for (int i = 0; i < RingPoints; i++)
        {
            float angle = i * Mathf.PI * 2f / RingPoints;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
        Color ringColor = Color.Lerp(color, Color.white, 0.35f);
        ringColor.a = alpha;
        ring.startColor = ringColor;
        ring.endColor = ringColor;

        int count = effect.ExtrasFor(procIds.Count);
        while (ghosts.Count < count)
        {
            MeshRenderer ghost = NeonGlow.CreateQuad(transform, "Ghost", 1f);
            ghosts.Add(ghost);
        }

        spin += Time.deltaTime * Mathf.Lerp(4f, 14f, progress);
        float orbit = ballRadius * Mathf.Lerp(GhostOrbitStart, GhostOrbitEnd, progress * progress);
        float pulse = waiting ? 0.75f + 0.25f * Mathf.Sin(Time.time * 28f) : 1f;
        if (viewCamera == null) viewCamera = Camera.main;
        for (int i = 0; i < ghosts.Count; i++)
        {
            MeshRenderer ghost = ghosts[i];
            ghost.enabled = i < count;
            if (i >= count) continue;
            float angle = spin + i * Mathf.PI * 2f / count;
            ghost.transform.position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbit;
            ghost.transform.localScale = Vector3.one * (ballRadius * GhostSize * Mathf.Lerp(0.5f, 1f, progress) * pulse);
            if (viewCamera != null) ghost.transform.rotation = viewCamera.transform.rotation;
            NeonGlow.SetColor(ghost, NeonGlow.Hdr(color, Mathf.Lerp(0.8f, 2.4f, progress) * pulse));
        }
    }

    private void SetVisible(bool visible)
    {
        ring.enabled = visible;
        if (!visible) foreach (MeshRenderer ghost in ghosts) ghost.enabled = false;
    }

    private static BallController FindMainBall()
    {
        foreach (BallController ball in BallController.Active)
            if (ball != null && !ball.IsExtra && ball.IsFlying) return ball;
        return null;
    }

    private static float BallRadius(BallController ball)
    {
        var sphere = ball.GetComponent<SphereCollider>();
        return sphere != null ? sphere.radius * ball.transform.lossyScale.x : 0.15f;
    }
}
