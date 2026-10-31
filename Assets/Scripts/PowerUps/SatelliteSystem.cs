using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime of the Satellite card. Keeps <see cref="PowerUpStats.SatelliteOrbs"/> glowing orbs circling every flying
/// ball; an orb that touches a brick damages it for a share of the ball's damage, with a short cooldown per brick.
/// Created on the first pick (<see cref="Ensure"/>), so a scene reload starts a run clean.
/// </summary>
public class SatelliteSystem : MonoBehaviour
{
    private const float OrbitRadius = 0.75f;
    private const float OrbitSpeed = 5f;       // radians per second
    private const float OrbRadius = 0.22f;     // contact radius against a brick footprint
    private const float DamageShare = 0.25f;   // of the ball's damage per touch
    private const float BrickCooldown = 0.5f;  // seconds before the same brick can be hit again by an orb
    private const float BoltShare = 0.5f;      // of the ball's damage per orb bolt
    private const float BoltInterval = 4f;     // seconds between two bolts of one orb with a single orb
    private const float BoltFasterPerOrb = 0.15f; // each extra orb makes every orb fire this much faster
    private const float BoltMinInterval = 1.5f;
    private static readonly Color BoltColor = new(1f, 0.9f, 0.5f);
    private static readonly Color OrbColor = new(1f, 0.85f, 0.35f);

    private readonly Dictionary<BallController, List<Transform>> orbs = new();
    private readonly Dictionary<Brick, float> lastHit = new();
    private readonly Dictionary<Transform, float> boltTimers = new(); // seconds until each orb fires its next bolt
    private readonly List<Brick> live = new();
    private readonly List<BallController> stale = new();
    private BrickGrid grid;
    private AreaDamage area;
    private float angle;

    public static SatelliteSystem Ensure()
    {
        var existing = FindAnyObjectByType<SatelliteSystem>();
        if (existing != null) return existing;
        return new GameObject("SatelliteSystem").AddComponent<SatelliteSystem>();
    }

    private void Awake()
    {
        grid = FindAnyObjectByType<BrickGrid>();
        area = AreaDamage.For(grid);
    }

    private void Update()
    {
        int wanted = PowerUpStats.SatelliteOrbs;
        angle += OrbitSpeed * Time.deltaTime;
        Camera cam = Camera.main;

        // Drop the orbs of balls that are gone.
        stale.Clear();
        foreach (KeyValuePair<BallController, List<Transform>> pair in orbs)
            if (pair.Key == null || !pair.Key.isActiveAndEnabled) stale.Add(pair.Key);
        foreach (BallController ball in stale) Release(ball);

        if (grid != null) grid.GetLiveBricks(live);

        foreach (BallController ball in BallController.Active)
        {
            if (ball == null) continue;
            if (!orbs.TryGetValue(ball, out List<Transform> list)) orbs[ball] = list = new List<Transform>();
            bool show = ball.IsFlying;

            while (list.Count < wanted) list.Add(CreateOrb());
            for (int i = 0; i < list.Count; i++)
            {
                Transform orb = list[i];
                orb.gameObject.SetActive(show);
                if (!show) continue;

                float a = angle + i * Mathf.PI * 2f / list.Count;
                Vector3 position = ball.Position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * OrbitRadius;
                orb.position = position;
                if (cam != null) orb.rotation = cam.transform.rotation;
                orb.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.time * 12f + i));

                TouchBricks(ball, position);
                TickBolt(ball, orb, position, wanted);
            }
        }
    }

    private void TouchBricks(BallController ball, Vector3 position)
    {
        if (grid == null || area == null) return;
        float halfW = grid.BrickWidth * 0.5f + OrbRadius;
        float halfD = grid.CellDepth * 0.5f + OrbRadius;
        foreach (Brick brick in live)
        {
            if (brick == null || !brick.IsAlive) continue;
            Vector3 d = brick.transform.position - position;
            if (Mathf.Abs(d.x) > halfW || Mathf.Abs(d.z) > halfD) continue;
            if (lastHit.TryGetValue(brick, out float t) && Time.time - t < BrickCooldown) continue;

            lastHit[brick] = Time.time;
            brick.Pulse(OrbColor * 1.5f, 0.18f, 0f);
            NeonFx.Sparks(position, d.normalized, OrbColor, 4, 3f, 60f, 0.06f, 0.2f);
            area.DealSecondary(brick, ball.Damage * DamageShare, DamageSource.Satellite);
        }
    }

    /// <summary>Seconds between the bolts of one orb: shorter with every extra orb (floored).</summary>
    public static float BoltIntervalFor(int orbs) =>
        Mathf.Max(BoltMinInterval, BoltInterval / (1f + BoltFasterPerOrb * Mathf.Max(0, orbs - 1)));

    // Every few seconds an orb also shoots a bolt at the brick nearest to it, for a share of the ball's damage.
    private void TickBolt(BallController ball, Transform orb, Vector3 position, int orbCount)
    {
        float interval = BoltIntervalFor(orbCount);
        if (!boltTimers.TryGetValue(orb, out float timer)) timer = Random.Range(0.4f, 1f) * interval; // staggered first shots
        timer -= Time.deltaTime; // scaled: pauses with the game
        if (timer > 0f)
        {
            boltTimers[orb] = timer;
            return;
        }
        boltTimers[orb] = interval;
        if (area == null) return;

        Brick nearest = null;
        float bestSqr = float.MaxValue;
        foreach (Brick brick in live)
        {
            if (brick == null || !brick.IsAlive) continue;
            float sqr = (brick.transform.position - position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = brick;
            }
        }
        if (nearest == null) return;

        Vector3 to = nearest.transform.position;
        ChainBolt.Spawn(position, to, BoltColor * 1.8f, 0.01f, 0.14f, 0.07f);
        NeonFx.Flash(position, OrbColor, 0.4f, 0.06f);
        NeonFx.Sparks(to, Vector3.up, BoltColor, 5, 3f, 80f, 0.07f, 0.2f);
        nearest.Pulse(OrbColor * 1.6f, 0.2f, 0.02f);
        SoundManager.Play(Sfx.ChainZap, 1.4f, 0.3f);
        area.DealSecondary(nearest, ball.Damage * BoltShare, DamageSource.Satellite);
    }

    // A soft golden glow around a white-hot core, turned toward the camera every frame.
    private static Transform CreateOrb()
    {
        var root = new GameObject("SatelliteOrb").transform;
        MeshRenderer glow = NeonGlow.CreateQuad(root, "Glow", 0.7f);
        NeonGlow.SetColor(glow, NeonGlow.Hdr(OrbColor, 1.6f));
        MeshRenderer core = NeonGlow.CreateQuad(root, "Core", 0.26f);
        NeonGlow.SetColor(core, NeonGlow.Hdr(Color.white, 3f));
        return root;
    }

    private void Release(BallController ball)
    {
        if (!orbs.TryGetValue(ball, out List<Transform> list)) return;
        foreach (Transform orb in list)
        {
            boltTimers.Remove(orb);
            if (orb != null) Destroy(orb.gameObject);
        }
        orbs.Remove(ball);
    }

    private void OnDestroy()
    {
        foreach (List<Transform> list in orbs.Values)
            foreach (Transform orb in list)
                if (orb != null) Destroy(orb.gameObject);
    }
}
