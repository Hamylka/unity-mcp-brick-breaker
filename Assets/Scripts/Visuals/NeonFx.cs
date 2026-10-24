using UnityEngine;

/// <summary>
/// One-shot neon particle effects (bursts, rings, sparks, embers) for any script to call, e.g.
/// <c>NeonFx.Burst(position, color)</c>. Everything is emitted from a single shared world-space particle system
/// built in code, so there is no prefab or scene setup. Particles are plain additive squares: after the
/// retro pixelation they read as glowing pixels, and bright colors feed the bloom.
/// The material is loaded from Resources/FX/M_NeonFx. Particles run on scaled time, so they freeze with the game.
/// </summary>
public static class NeonFx
{
    private const string MaterialPath = "FX/M_NeonFx";
    private const int MaxParticles = 2000;

    private static ParticleSystem system;

    /// <summary>A round burst of sparks flying out in every direction (mostly flat, a little upward).</summary>
    public static void Burst(Vector3 position, Color color, int count = 24, float speed = 4f, float size = 0.12f,
        float lifetime = 0.45f)
    {
        ParticleSystem ps = Get();
        if (ps == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector2 flat = Random.insideUnitCircle.normalized * Random.Range(0.35f, 1f);
            var velocity = new Vector3(flat.x, Random.Range(0.1f, 0.8f), flat.y) * speed;
            Emit(ps, position, velocity, color, size * Random.Range(0.7f, 1.3f), lifetime * Random.Range(0.7f, 1.2f));
        }
    }

    /// <summary>An expanding flat ring of sparks: a shockwave.</summary>
    public static void Ring(Vector3 position, Color color, int count = 28, float speed = 5f, float size = 0.1f,
        float lifetime = 0.35f)
    {
        ParticleSystem ps = Get();
        if (ps == null) return;
        float offset = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float angle = offset + i * Mathf.PI * 2f / count;
            var velocity = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * speed;
            Emit(ps, position, velocity, color, size, lifetime);
        }
    }

    /// <summary>A short cone of sparks thrown along <paramref name="direction"/> (impacts, muzzle flashes).</summary>
    public static void Sparks(Vector3 position, Vector3 direction, Color color, int count = 10, float speed = 5f,
        float spreadDegrees = 50f, float size = 0.09f, float lifetime = 0.25f)
    {
        ParticleSystem ps = Get();
        if (ps == null) return;
        direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.up;
        for (int i = 0; i < count; i++)
        {
            Quaternion spread = Quaternion.Euler(Random.Range(-spreadDegrees, spreadDegrees) * 0.5f,
                Random.Range(-spreadDegrees, spreadDegrees), 0f);
            Vector3 velocity = spread * direction * (speed * Random.Range(0.5f, 1.1f));
            Emit(ps, position, velocity, color, size * Random.Range(0.7f, 1.3f), lifetime * Random.Range(0.7f, 1.2f));
        }
    }

    /// <summary>One big square that shrinks away quickly: a flash at a point.</summary>
    public static void Flash(Vector3 position, Color color, float size = 0.6f, float lifetime = 0.12f)
    {
        ParticleSystem ps = Get();
        if (ps == null) return;
        Emit(ps, position, Vector3.zero, color, size, lifetime);
    }

    /// <summary>A few slow sparks drifting upward from a spot (soft, ambient).</summary>
    public static void Embers(Vector3 position, Color color, int count = 3, float size = 0.07f, float lifetime = 0.7f)
    {
        ParticleSystem ps = Get();
        if (ps == null) return;
        for (int i = 0; i < count; i++)
        {
            var velocity = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(1f, 2.2f), Random.Range(-0.4f, 0.4f));
            Vector3 jitter = new(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.1f, 0.1f));
            Emit(ps, position + jitter, velocity, color, size * Random.Range(0.7f, 1.3f), lifetime * Random.Range(0.8f, 1.3f));
        }
    }

    /// <summary>Sparks spread evenly along a segment, flying along <paramref name="direction"/> (shattering bars).</summary>
    public static void Line(Vector3 from, Vector3 to, Vector3 direction, Color color, int count = 40, float speed = 4f,
        float size = 0.1f, float lifetime = 0.5f)
    {
        ParticleSystem ps = Get();
        if (ps == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = Vector3.Lerp(from, to, (i + Random.value) / count);
            Vector3 velocity = (direction + Random.insideUnitSphere * 0.6f) * (speed * Random.Range(0.4f, 1.1f));
            velocity.y = Mathf.Abs(velocity.y) + Random.Range(0.2f, 1.5f);
            Emit(ps, position, velocity, color, size * Random.Range(0.6f, 1.4f), lifetime * Random.Range(0.6f, 1.2f));
        }
    }

    private static void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, Color color, float size, float lifetime)
    {
        var emit = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startColor = color,
            startSize = size,
            startLifetime = lifetime,
            applyShapeToPosition = false
        };
        ps.Emit(emit, 1);
    }

    // The shared system, rebuilt if the scene was reloaded (Restart destroys it).
    private static ParticleSystem Get()
    {
        if (system != null) return system;

        var material = Resources.Load<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"NeonFx: material Resources/{MaterialPath} not found, effects are disabled.");
            return null;
        }

        var go = new GameObject("NeonFx");
        system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = true; // stays "playing" forever, so emitted particles always simulate
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = MaxParticles;
        main.gravityModifier = 0f;
        main.startSpeed = 0f;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false; // only explicit Emit calls

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        // Sparks slow down quickly: a punchy start, then they hang and fade.
        ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0f;
        limit.dampen = 0.08f;
        limit.drag = 0f;

        // Shrink to nothing: reads as the spark burning out.
        ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.6f, 0.75f), new Keyframe(1f, 0f)));

        // Full brightness for most of the life, then fade out (the color itself comes from each Emit call).
        ParticleSystem.ColorOverLifetimeModule colorOverLife = system.colorOverLifetime;
        colorOverLife.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 1f;

        system.Play();
        return system;
    }
}
