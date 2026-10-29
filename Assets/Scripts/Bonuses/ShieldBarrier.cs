using UnityEngine;

/// <summary>
/// The barrier raised by <see cref="ShieldEffect"/>. The ball reflects off it like off a wall; the first hit
/// breaks it. Built in code so it needs no prefab.
/// Juice: it unrolls from the middle with a bright flash, hums (glow pulse and drifting sparks) while up,
/// and shatters into shards when the ball hits it.
/// </summary>
public class ShieldBarrier : BallSurface
{
    /// <summary>Shield cyan, shared by the barrier, its particles and the HUD row.</summary>
    public static readonly Color ShieldColor = new(0.3f, 0.9f, 1f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private const float BreakTime = 0.25f;
    private const float RaiseTime = 0.35f;
    private const float Glow = 2.2f;           // emission strength at rest (bloom picks it up above 1)
    private const float RaiseGlowBoost = 3f;   // extra glow at the start of the raise, fading to 0
    private const float PulseSpeed = 5f;       // radians per second of the idle hum
    private const float PulseDepth = 0.25f;
    private const float SparkInterval = 0.12f; // seconds between idle sparks drifting off the barrier

    /// <summary>The shield currently up, or null.</summary>
    public static ShieldBarrier Current { get; private set; }

    private readonly System.Collections.Generic.HashSet<Collider> ignoredBalls = new();
    private Collider barrierCollider;
    private Renderer barrierRenderer;
    private MaterialPropertyBlock block;
    private Vector3 startScale;
    private float raiseT;
    private float breakT = -1f;
    private float sparkTimer;
    private float age;
    private int bouncesLeft = 1;
    private int bouncesTotal = 1; // segments of the HUD ring

    /// <summary>Lets the shield absorb more bounces before it breaks (Shield Plus picked while it is up).</summary>
    public void AddBounces(int count)
    {
        bouncesLeft += count;
        bouncesTotal += count;
    }

    public static ShieldBarrier Create(Material material, float z, float width, float thickness)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); // comes with a BoxCollider
        cube.name = "Shield";
        cube.transform.position = new Vector3(0f, 0f, z);
        // Tall enough to catch the ball, which rolls close to the floor.
        cube.transform.localScale = new Vector3(width, 1f, thickness);
        if (material != null) cube.GetComponent<Renderer>().sharedMaterial = material;

        var barrier = cube.AddComponent<ShieldBarrier>();
        barrier.barrierCollider = cube.GetComponent<Collider>();
        barrier.barrierRenderer = cube.GetComponent<Renderer>();
        barrier.startScale = cube.transform.localScale;
        barrier.ApplyGlow(Glow + RaiseGlowBoost);
        cube.transform.localScale = new Vector3(0f, barrier.startScale.y, barrier.startScale.z);
        barrier.bouncesLeft = barrier.bouncesTotal = 1 + PowerUpStats.ShieldExtraBounces;
        Current = barrier;

        // A flash and a line of sparks along the barrier as it appears.
        Vector3 center = cube.transform.position;
        NeonFx.Flash(center, ShieldColor, 0.9f, 0.15f);
        NeonFx.Line(center + Vector3.left * width * 0.4f, center + Vector3.right * width * 0.4f, Vector3.forward,
            ShieldColor, 24, 1.5f, 0.07f, 0.4f);
        return barrier;
    }

    public override void OnBallHit(BallController ball, Collision collision)
    {
        if (breakT >= 0f) return;
        ball.PassThroughPaddle(); // the rebound goes back up through the paddle instead of bouncing off its back

        // Shield Plus: it survives until the last bounce, with a small flash for each one absorbed.
        if (--bouncesLeft > 0)
        {
            Vector3 spot = ball.Position;
            spot.z = transform.position.z;
            NeonFx.Flash(spot, ShieldColor, 0.8f, 0.1f);
            NeonFx.Burst(spot, ShieldColor, 10, 4f, 0.08f, 0.3f);
            SoundManager.Play(Sfx.ShieldUp);
            return;
        }

        barrierCollider.enabled = false; // the bounce was already computed, so this is safe
        breakT = 0f;
        SoundManager.Play(Sfx.ShieldBreak);

        // Shatter: a flash where the ball struck, then shards spraying forward along the whole barrier.
        Vector3 hit = ball.Position;
        hit.z = transform.position.z;
        float halfWidth = startScale.x * 0.45f;
        Vector3 center = transform.position;
        NeonFx.Flash(hit, Color.white, 1f, 0.12f);
        NeonFx.Burst(hit, ShieldColor, 18, 5f, 0.1f, 0.4f);
        NeonFx.Line(center + Vector3.left * halfWidth, center + Vector3.right * halfWidth, Vector3.forward,
            ShieldColor, 60, 4f, 0.11f, 0.55f);
    }

    private void Update()
    {
        float dt = Time.deltaTime; // scaled: freezes with the game
        age += dt;
        if (breakT < 0f) UpdateFadingPassThrough();

        if (breakT >= 0f)
        {
            breakT += dt;
            float k = Mathf.Clamp01(breakT / BreakTime);
            transform.localScale = new Vector3(startScale.x, startScale.y * (1f - k), startScale.z * (1f + k * 2f));
            ApplyGlow(Glow * (1f + 2f * (1f - k))); // flares as it collapses
            if (k >= 1f) Destroy(gameObject);
            return;
        }

        ActiveBonusTracker.Report("shield", "shield", "SHIELD", ShieldColor, -1f, 0f, bouncesTotal, bouncesLeft);

        if (raiseT < 1f)
        {
            // Unrolls from the middle with a small overshoot, glow settling from a flare to rest.
            raiseT = Mathf.Min(1f, raiseT + dt / RaiseTime);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = raiseT - 1f;
            float eased = 1f + c3 * u * u * u + c1 * u * u; // ease-out-back
            transform.localScale = new Vector3(startScale.x * eased, startScale.y, startScale.z);
            ApplyGlow(Glow + RaiseGlowBoost * (1f - raiseT));
            return;
        }

        // Idle hum: a slow glow pulse and the odd spark drifting off, so it feels alive but stays calm.
        ApplyGlow(Glow * (1f + PulseDepth * Mathf.Sin(age * PulseSpeed)));
        sparkTimer -= dt;
        if (sparkTimer <= 0f)
        {
            sparkTimer = SparkInterval;
            Vector3 spot = transform.position + Vector3.right * (Random.Range(-0.5f, 0.5f) * startScale.x * 0.9f);
            NeonFx.Embers(spot, ShieldColor, 1, 0.06f, 0.5f);
        }
    }

    /// <summary>
    /// Extras shrinking away at the end of their Multi-Ball life no longer count: they pass through the barrier
    /// without bouncing or breaking it. Collision is switched off for them and back on if they stop fading
    /// (e.g. promoted to main).
    /// </summary>
    private void UpdateFadingPassThrough()
    {
        for (int i = 0; i < BallController.Active.Count; i++)
        {
            BallController ball = BallController.Active[i];
            if (ball == null) continue;
            var sphere = ball.GetComponent<SphereCollider>();
            if (sphere == null) continue;

            bool fading = ball.TryGetComponent(out TemporaryBall temp) && temp.IsFading;
            if (fading) { if (ignoredBalls.Add(sphere)) Physics.IgnoreCollision(sphere, barrierCollider, true); }
            else if (ignoredBalls.Remove(sphere)) Physics.IgnoreCollision(sphere, barrierCollider, false);
        }
    }

    private void ApplyGlow(float intensity)
    {
        if (barrierRenderer == null) return;
        block ??= new MaterialPropertyBlock();
        barrierRenderer.GetPropertyBlock(block);
        Color body = ShieldColor * 0.3f;
        body.a = 1f;
        block.SetColor(BaseColorId, body);
        block.SetColor(EmissionColorId, ShieldColor * intensity);
        barrierRenderer.SetPropertyBlock(block);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
