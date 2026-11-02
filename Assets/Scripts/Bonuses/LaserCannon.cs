using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime part of <see cref="LaserEffect"/>, added to the paddle. Fires instant bolts along +Z every interval,
/// only while a ball is in play, and removes itself when the time is up. Laser Focus adds parallel lanes
/// (<see cref="PowerUpStats.LaserLanes"/>), spread across the paddle width; every lane deals the full damage.
/// </summary>
[DisallowMultipleComponent]
public class LaserCannon : MonoBehaviour
{
    private const float BoltFlashTime = 0.15f;
    private const float MaxRange = 40f;
    private const int FullFxLanes = 3; // beyond this many lanes the per-lane sparks are thinned out

    private float remaining;
    private float totalTime;
    private float interval;
    private float cooldown;

    // Laser Frenzy's fire rate is read live, so a pick made while the cannon is up speeds it at once.
    private float Interval => interval / PowerUpStats.LaserFireRate;
    private Color color;
    private Material material;
    private Collider paddleCollider;
    private readonly List<LineRenderer> bolts = new();
    private readonly List<LaserSight> sights = new();
    private float boltWidth;
    private float flash;
    private readonly RaycastHit[] hits = new RaycastHit[32];

    /// <summary>Starts (or refreshes) the cannon.</summary>
    public void Activate(float duration, float fireInterval, Material beamMaterial, Color boltColor, float width)
    {
        if (bolts.Count == 0) // a fresh cannon (a refresh only gets the shared pickup cue)
        {
            SoundManager.Play(Sfx.LaserCharge);
            NeonFx.Sparks(transform.position, Vector3.up, boltColor, 14, 3f, 70f, 0.08f, 0.4f);
        }

        remaining = duration;
        totalTime = duration;
        interval = fireInterval;
        color = boltColor;
        material = beamMaterial;
        boltWidth = width;
        if (paddleCollider == null) paddleCollider = GetComponentInChildren<Collider>();
        if (cooldown <= 0f || cooldown > Interval) cooldown = Mathf.Min(0.3f, Interval); // first shot comes quickly

        EnsureLanes(PowerUpStats.LaserLanes);
        foreach (LineRenderer bolt in bolts) bolt.sharedMaterial = material;
        foreach (LaserSight sight in sights) sight.SetColor(boltColor);
    }

    /// <summary>Adds time to the running cannon (Bonus Extender); the HUD ring never reads above full.</summary>
    public void Extend(float seconds)
    {
        remaining += seconds;
        totalTime = Mathf.Max(totalTime, remaining);
    }

    /// <summary>Full length of this cannon's run, used to size an extension.</summary>
    public float TotalTime => totalTime;

    // Grows the bolt and sight lists to one pair per lane (they are never shrunk: extra lanes just stay hidden).
    private void EnsureLanes(int lanes)
    {
        while (bolts.Count < lanes)
        {
            var holder = new GameObject("LaserBolt");
            holder.transform.SetParent(transform, false);
            var bolt = holder.AddComponent<LineRenderer>();
            bolt.positionCount = 2;
            bolt.useWorldSpace = true;
            bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bolt.receiveShadows = false;
            bolt.sharedMaterial = material;
            bolt.widthMultiplier = boltWidth;
            bolt.enabled = false;
            bolts.Add(bolt);
            sights.Add(new LaserSight(transform, material, color));
        }
    }

    /// <summary>Where lane <paramref name="lane"/> of <paramref name="lanes"/> starts: spread across the paddle width, the middle for one lane.</summary>
    private Vector3 LaneOrigin(int lane, int lanes)
    {
        Vector3 origin = transform.position;
        if (lanes <= 1 || paddleCollider == null) return origin;
        Bounds paddle = paddleCollider.bounds;
        origin.x = paddle.min.x + paddle.size.x * (lane + 0.5f) / lanes;
        return origin;
    }

    private void Update()
    {
        float dt = Time.deltaTime; // scaled: the cannon pauses with the game
        remaining -= dt;
        if (remaining <= 0f)
        {
            // Power-down: the cannon winds down and sputters, a last puff of sparks and embers drifting off it.
            SoundManager.Play(Sfx.LaserPowerDown);
            NeonFx.Burst(transform.position, color, 12, 2f, 0.07f, 0.35f);
            NeonFx.Sparks(transform.position, Vector3.forward, color, 6, 1.5f, 30f, 0.06f, 0.25f);
            NeonFx.Embers(transform.position, Color.Lerp(color, Color.white, 0.3f), 4, 0.06f, 0.8f);
            foreach (LineRenderer bolt in bolts)
                if (bolt != null) Destroy(bolt.gameObject);
            Destroy(this);
            return;
        }

        ActiveBonusTracker.Report("laser", "laser", "LASER", color, remaining, totalTime);
        EnsureLanes(PowerUpStats.LaserLanes); // a Laser Focus pick while the cannon is up adds a lane at once
        UpdateBoltFlash(dt);

        bool armed = AnyBallFlying();
        cooldown -= dt;
        if (cooldown <= 0f && armed)
        {
            cooldown = Interval;
            Fire();
        }
        UpdateSight(armed, dt);
    }

    private void OnDestroy()
    {
        foreach (LaserSight sight in sights) sight?.Destroy();
    }

    // The aiming guides show the lane of every next bolt and charge up toward it.
    private void UpdateSight(bool armed, float dt)
    {
        int lanes = PowerUpStats.LaserLanes;
        float charge = 1f - Mathf.Clamp01(cooldown / Interval);
        for (int i = 0; i < sights.Count; i++)
        {
            if (i >= lanes)
            {
                sights[i].Hide();
                continue;
            }
            Vector3 origin = LaneOrigin(i, lanes);
            float nearest = FindTarget(origin, out BrickStack target);
            sights[i].Update(origin, origin + Vector3.forward * nearest, target != null ? target.Bottom : null,
                charge, Mathf.Max(0f, cooldown), armed, dt);
        }
    }

    /// <summary>Distance to the nearest solid thing in a bolt's lane (balls and the paddle ignored), and the brick stack there if any.</summary>
    private float FindTarget(Vector3 origin, out BrickStack target)
    {
        float nearest = MaxRange;
        target = null;
        int count = Physics.RaycastNonAlloc(origin, Vector3.forward, hits, MaxRange, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.distance >= nearest) continue;
            if (hit.collider.GetComponentInParent<BallController>() != null) continue;
            if (hit.collider.GetComponentInParent<PaddleController>() != null) continue;
            nearest = hit.distance;
            target = hit.collider.GetComponentInParent<BrickStack>();
        }
        return nearest;
    }

    private void Fire()
    {
        int lanes = PowerUpStats.LaserLanes;
        int damage = CurrentDamage();
        bool thin = lanes > FullFxLanes;
        Color hot = Color.Lerp(color, Color.white, 0.5f);

        for (int i = 0; i < lanes; i++)
        {
            Vector3 origin = LaneOrigin(i, lanes);
            float nearest = FindTarget(origin, out BrickStack target);
            Vector3 end = origin + Vector3.forward * nearest;
            if (target != null)
            {
                using var tag = DamageTag.Begin(DamageSource.Laser);
                target.TakeDamage(damage);
            }

            LineRenderer bolt = bolts[i];
            bolt.SetPosition(0, origin);
            bolt.SetPosition(1, end);
            bolt.enabled = true;

            // Muzzle flash at the paddle, and sparks splashing back off whatever the bolt hit (thinned out with many lanes).
            NeonFx.Flash(origin, hot, 0.45f, 0.08f);
            if (!thin) NeonFx.Sparks(origin, Vector3.forward, color, 5, 3f, 40f, 0.07f, 0.15f);
            if (nearest < MaxRange)
            {
                NeonFx.Flash(end, hot, 0.5f, 0.1f);
                NeonFx.Sparks(end, Vector3.back, color, thin ? 5 : 12, 5f, 120f, 0.09f, 0.3f);
            }
        }

        flash = BoltFlashTime;
        SetBoltFlash(1f);
        SoundManager.Play(Sfx.LaserShot);
    }

    private void UpdateBoltFlash(float dt)
    {
        if (flash <= 0f) return;
        flash -= dt;
        if (flash <= 0f)
        {
            foreach (LineRenderer bolt in bolts) bolt.enabled = false;
            return;
        }
        SetBoltFlash(flash / BoltFlashTime);
    }

    // k goes 1 -> 0: the bolt starts fat and bright, then thins and fades, which reads as a zap instead of a line.
    private void SetBoltFlash(float k)
    {
        Color tint = color;
        tint.a *= k;
        float width = boltWidth * Mathf.Lerp(0.2f, 1.8f, k * k);
        foreach (LineRenderer bolt in bolts)
        {
            bolt.widthMultiplier = width;
            bolt.startColor = tint;
            bolt.endColor = tint;
        }
    }

    // The strongest ball sets the damage, so upgrades from the cards also boost the laser.
    private static int CurrentDamage()
    {
        int damage = 1;
        foreach (BallController ball in BallController.Active)
            if (!ball.IsExtra) damage = Mathf.Max(damage, ball.AttackDamage);
        return Mathf.CeilToInt(damage * (1f + PowerUpStats.LaserDamageBonus) - 0.001f); // Hot Laser
    }

    private static bool AnyBallFlying()
    {
        foreach (BallController ball in BallController.Active)
            if (ball.IsFlying) return true;
        return false;
    }
}
