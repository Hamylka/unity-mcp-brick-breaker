using UnityEngine;

/// <summary>
/// The ball's look, kept apart from its physics: a white-hot core in a glowing orange shell (Neon Fresnel shader),
/// a soft halo, a pool of light on the floor under it, a two-layer trail (hot thin core inside a wide
/// orange-to-magenta tail), squash and stretch along its path, and sparks on every bounce.
/// The mesh is drawn by a child so it can stretch without touching the collider. Works on extra balls too
/// (they are copies, so the children already exist and are reused).
/// While the ball phases through bricks (<see cref="BallController.Phasing"/>) it turns into a ghost: the core shrinks
/// and goes pale violet inside a big see-through shell that flickers, the trail, halo and floor light turn violet,
/// afterimages trail behind it, and every brick it slips through gets a ripple, a flash and a hollow whoosh.
/// </summary>
[RequireComponent(typeof(BallController))]
[DisallowMultipleComponent]
public class BallVisual : MonoBehaviour
{
    private const string BodyName = "BallBody";
    private const string HaloName = "BallHalo";
    private const string FloorName = "BallFloorGlow";
    private const string CoreTrailName = "BallCoreTrail";
    private const string ShellName = "BallGhostShell";
    private const string GhostMaterialPath = "FX/M_BallGhost";

    private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int GhostColorId = Shader.PropertyToID("_Color");

    [Tooltip("Material using the BrickBreaker/Neon Fresnel shader (core and rim colors are set on it).")]
    [SerializeField] private Material bodyMaterial;
    [Tooltip("Main glow color: halo, floor light, sparks.")]
    [SerializeField] private Color glowColor = new(1f, 0.5f, 0.12f, 1f);

    [Header("Glow (world units)")]
    [SerializeField, Min(0f)] private float haloSize = 1.15f;
    [SerializeField, Min(0f)] private float haloIntensity = 1.1f;
    [SerializeField, Min(0f)] private float floorGlowSize = 1.5f;
    [SerializeField, Min(0f)] private float floorGlowIntensity = 0.9f;
    [SerializeField] private float floorY = 0.02f;
    [Tooltip("While the ball waits on the paddle, the halo breathes to say \"tap to launch\".")]
    [SerializeField, Range(0f, 1f)] private float readyPulse = 0.35f;

    [Header("Squash & Stretch")]
    [Tooltip("Stretch along the path per unit of speed (10 speed x 0.012 = 12% longer).")]
    [SerializeField, Range(0f, 0.05f)] private float stretchPerSpeed = 0.012f;
    [SerializeField, Range(0f, 1f)] private float maxStretch = 0.35f;
    [SerializeField, Range(0f, 0.6f)] private float bounceSquash = 0.3f;
    [SerializeField, Min(0.01f)] private float squashTime = 0.1f;

    [Header("Trail")]
    [SerializeField, Min(0f)] private float trailTime = 0.24f;
    [SerializeField, Min(0f)] private float trailBrightness = 2.6f;
    [SerializeField, Range(0f, 1f)] private float coreTrailWidth = 0.38f;
    [SerializeField, Min(0f)] private float coreTrailTime = 0.1f;

    [Header("Bounce Sparks")]
    [SerializeField, Min(0)] private int sparkCount = 7;
    [SerializeField, Min(0f)] private float sparkSpeed = 4f;

    [Header("Phase Look (ghost ball)")]
    [Tooltip("Additive Neon Ghost material for the see-through shell. Empty = Resources/FX/M_BallGhost.")]
    [SerializeField] private Material ghostMaterial;
    [Tooltip("Ghost color; the Phase wall buff overrides it with its own color when applied.")]
    [SerializeField] private Color phaseColor = new(0.55f, 0.4f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float phaseFadeIn = 0.12f;
    [SerializeField, Min(0.01f)] private float phaseFadeOut = 0.35f;
    [Tooltip("Size of the solid core while phasing (1 = normal ball).")]
    [SerializeField, Range(0.2f, 1f)] private float phaseCoreScale = 0.55f;
    [Tooltip("Size of the see-through shell (1 = normal ball).")]
    [SerializeField, Range(1f, 3f)] private float shellScale = 1.55f;
    [SerializeField, Min(0f)] private float shellIntensity = 2.4f;
    [Tooltip("How much the shell brightness wavers (0 = steady).")]
    [SerializeField, Range(0f, 1f)] private float shellFlicker = 0.45f;
    [SerializeField, Min(0f)] private float shellFlickerSpeed = 14f;
    [Tooltip("Seconds between two afterimages left behind the ghost ball.")]
    [SerializeField, Min(0.01f)] private float echoInterval = 0.04f;
    [SerializeField, Min(0f)] private float echoSize = 0.32f;
    [SerializeField, Min(0.01f)] private float echoLifetime = 0.22f;
    [SerializeField, Min(0f)] private float echoIntensity = 0.9f;
    [SerializeField, Min(0f)] private float phaseTrailTime = 0.34f;
    [Tooltip("Trail brightness while phasing. Lower than the normal trail so the violet does not clip to white.")]
    [SerializeField, Min(0f)] private float phaseTrailBrightness = 1.3f;
    [Tooltip("Minimum saturation of the ghost hue. Pastel buff colors clip to white once boosted for the bloom.")]
    [SerializeField, Range(0f, 1f)] private float ghostSaturation = 0.8f;
    [Tooltip("Shell pop when the ball slips into a brick (0.35 = 35% bigger, then settles).")]
    [SerializeField, Range(0f, 1f)] private float passKick = 0.35f;
    [SerializeField, Min(0.01f)] private float passKickTime = 0.15f;

    /// <summary>Ghost color (set by the Phase wall buff so the ball, zone and HUD ring match).</summary>
    public Color PhaseColor { get => phaseColor; set => phaseColor = value; }

    private BallController ball;
    private Transform body;
    private MeshRenderer bodyRenderer;
    private MeshRenderer halo;
    private MeshRenderer floorGlow;
    private MeshRenderer shell;
    private MaterialPropertyBlock bodyBlock;
    private MaterialPropertyBlock shellBlock;
    private Color baseCoreColor;
    private Color baseRimColor;
    private TrailRenderer outerTrail;
    private TrailRenderer coreTrail;
    private Gradient outerNormal, outerPhase, coreNormal, corePhase;
    private float outerWidth;
    private Camera viewCamera;
    private float referenceSpeed;
    private float squashT = 1f;
    private Vector3 squashNormal = Vector3.forward;
    private float time;
    private float phaseBlend;
    private float trailBlend = -1f;
    private float phaseTime;
    private float nextEcho;
    private float kickT = 1f;
    private bool wasPhasing;
    private Color ghostHue; // phaseColor pushed to a strong saturation, used for everything that glows

    private void Awake()
    {
        ball = GetComponent<BallController>();
        BuildBody();
        halo = FindOrCreateQuad(HaloName, haloSize);
        floorGlow = FindOrCreateQuad(FloorName, floorGlowSize);
        BuildShell();
        StyleTrails();
    }

    private void OnEnable()
    {
        ball.Bounced += HandleBounce;
        ball.PhasedThrough += HandlePhasedThrough;
    }

    private void OnDisable()
    {
        ball.Bounced -= HandleBounce;
        ball.PhasedThrough -= HandlePhasedThrough;
    }

    private void Start()
    {
        viewCamera = Camera.main;
        referenceSpeed = Mathf.Max(0.1f, ball.CurrentSpeed);
    }

    private void BuildBody()
    {
        var rootRenderer = GetComponent<MeshRenderer>();
        Transform existing = transform.Find(BodyName);
        if (existing != null)
        {
            body = existing;
        }
        else
        {
            var go = new GameObject(BodyName, typeof(MeshFilter), typeof(MeshRenderer));
            body = go.transform;
            body.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = bodyMaterial != null ? bodyMaterial : rootRenderer.sharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (rootRenderer != null) rootRenderer.enabled = false; // the collider stays on the root, the look moves to the child

        bodyRenderer = body.GetComponent<MeshRenderer>();
        Material material = bodyRenderer.sharedMaterial;
        baseCoreColor = material != null && material.HasProperty(CoreColorId) ? material.GetColor(CoreColorId) : Color.white;
        baseRimColor = material != null && material.HasProperty(RimColorId) ? material.GetColor(RimColorId) : glowColor;
        bodyBlock = new MaterialPropertyBlock();
    }

    /// <summary>The see-through ghost shell around the core, hidden until the ball phases.</summary>
    private void BuildShell()
    {
        shellBlock = new MaterialPropertyBlock();
        if (ghostMaterial == null) ghostMaterial = Resources.Load<Material>(GhostMaterialPath);
        Transform existing = transform.Find(ShellName);
        if (existing != null)
        {
            shell = existing.GetComponent<MeshRenderer>();
        }
        else if (ghostMaterial != null)
        {
            var go = new GameObject(ShellName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
            shell = go.GetComponent<MeshRenderer>();
            shell.sharedMaterial = ghostMaterial;
            shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shell.receiveShadows = false;
        }
        if (shell != null) shell.enabled = false;
    }

    private MeshRenderer FindOrCreateQuad(string childName, float worldSize)
    {
        Transform existing = transform.Find(childName);
        MeshRenderer quad = existing != null ? existing.GetComponent<MeshRenderer>() : NeonGlow.CreateQuad(transform, childName, 1f);
        quad.transform.localScale = Vector3.one * (worldSize / Mathf.Max(0.0001f, transform.lossyScale.x));
        return quad;
    }

    private void StyleTrails()
    {
        // The existing trail becomes the wide outer tail: white-hot at the ball, orange, then a magenta fade.
        TrailRenderer outer = null;
        foreach (TrailRenderer trail in GetComponents<TrailRenderer>()) { outer = trail; break; }
        if (outer == null) return;
        outerTrail = outer;
        outerWidth = outer.widthMultiplier;

        outer.time = trailTime;
        outerNormal = MakeGradient(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.12f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.15f, 0.45f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.3f), new GradientAlphaKey(0f, 1f) });
        outer.colorGradient = outerNormal;
        // The trail material is orange; neutral white here so the gradient sets the hue (HDR for the bloom).
        NeonGlow.SetColor(outer, NeonGlow.Hdr(Color.white, trailBrightness));

        // A thin white core inside it, shorter, like the bright filament of a neon tube.
        Transform existing = transform.Find(CoreTrailName);
        TrailRenderer core = existing != null ? existing.GetComponent<TrailRenderer>() : null;
        if (core == null)
        {
            var go = new GameObject(CoreTrailName);
            go.transform.SetParent(transform, false);
            core = go.AddComponent<TrailRenderer>();
        }
        core.sharedMaterial = NeonGlow.LineMaterial;
        core.time = coreTrailTime;
        core.widthMultiplier = outer.widthMultiplier * coreTrailWidth;
        core.widthCurve = outer.widthCurve;
        core.minVertexDistance = outer.minVertexDistance;
        core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        core.receiveShadows = false;
        coreNormal = MakeGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.85f, 0.5f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        core.colorGradient = coreNormal;
        NeonGlow.SetColor(core, NeonGlow.Hdr(Color.white, trailBrightness * 1.3f));
        coreTrail = core;
        BuildPhaseGradients();
    }

    /// <summary>
    /// Ghost versions of the two trails, same key layout as the normal ones so they can be blended:
    /// a pale lavender head into the ghost color, then a cold blue fade, a little more transparent (wispy).
    /// </summary>
    private void BuildPhaseGradients()
    {
        Color.RGBToHSV(phaseColor, out float hue, out float saturation, out _);
        ghostHue = Color.HSVToRGB(hue, Mathf.Max(saturation, ghostSaturation), 1f);

        Color pale = Color.Lerp(Color.white, ghostHue, 0.5f);
        Color deep = Color.Lerp(ghostHue, new Color(0.1f, 0.2f, 1f), 0.5f);
        outerPhase = MakeGradient(
            new[] { new GradientColorKey(pale, 0f), new GradientColorKey(ghostHue, 0.25f), new GradientColorKey(deep, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.55f, 0.3f), new GradientAlphaKey(0f, 1f) });
        corePhase = MakeGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(pale, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trailBlend = -1f; // force a refresh
    }

    private void HandleBounce(BallController _, Collision collision)
    {
        if (collision.contactCount == 0) return;
        ContactPoint contact = collision.GetContact(0);
        Vector3 normal = contact.normal;
        normal.y = 0f;
        if (normal.sqrMagnitude < 1e-4f) return;
        normal.Normalize();
        if (Vector3.Dot(ball.Position - contact.point, normal) < 0f) normal = -normal; // face the ball

        squashNormal = normal;
        squashT = 0f;

        Vector3 point = contact.point;
        point.y = ball.Position.y;
        Color flash = Color.Lerp(new Color(1f, 0.85f, 0.6f), Color.Lerp(Color.white, ghostHue, 0.4f), phaseBlend);
        NeonFx.Sparks(point, normal, Color.Lerp(glowColor, ghostHue, phaseBlend), sparkCount, sparkSpeed, 70f, 0.07f, 0.22f);
        NeonFx.Flash(point, flash, 0.35f, 0.07f);
    }

    /// <summary>
    /// The ghost ball slipping into a brick: a flat ripple on the brick, a pale flash on the ball, a few sparks out
    /// the far side along its path, a pop of the shell and a hollow whoosh. Plays even when the halved damage only
    /// adds to the brick's carry, so every brick passed is marked.
    /// </summary>
    private void HandlePhasedThrough(BallController _, BrickStack stack)
    {
        Vector3 position = ball.Position;
        Vector3 center = stack.transform.position;
        center.y = position.y;
        Color pale = Color.Lerp(Color.white, ghostHue, 0.5f);

        NeonFx.Ring(center, NeonGlow.Hdr(ghostHue, 1.6f), 14, 2.6f, 0.07f, 0.22f);
        NeonFx.Flash(position, NeonGlow.Hdr(pale, 1.4f), 0.55f, 0.09f);
        NeonFx.Sparks(position, ball.Direction, NeonGlow.Hdr(ghostHue, 1.3f), 4, 3.5f, 35f, 0.06f, 0.2f);
        kickT = 0f;
        SoundManager.Play(Sfx.PhasePass, Random.Range(0.95f, 1.08f));
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        time += Time.unscaledDeltaTime;
        Vector3 position = transform.position;
        bool flying = ball.IsFlying;

        // Squash against the surface right after a bounce, otherwise stretch along the path with speed.
        Quaternion axis;
        float stretch;
        if (squashT < 1f)
        {
            squashT = Mathf.Min(1f, squashT + dt / squashTime);
            axis = Quaternion.LookRotation(squashNormal, Vector3.up);
            stretch = -bounceSquash * Mathf.Sin(squashT * Mathf.PI);
        }
        else
        {
            axis = Quaternion.LookRotation(ball.Direction, Vector3.up);
            stretch = flying ? Mathf.Min(maxStretch, ball.CurrentSpeed * stretchPerSpeed) : 0f;
        }
        float along = 1f + stretch;
        float across = 1f / Mathf.Sqrt(along); // keeps the volume, so it reads as squash rather than resize
        float coreScale = Mathf.Lerp(1f, phaseCoreScale, UpdatePhaseBlend(dt));
        body.rotation = axis;
        body.localScale = new Vector3(across, across, along) * coreScale;
        UpdateGhost(dt, axis, stretch, flying);

        // Faster than at the start (boosts, speed upgrades) = brighter glow. Waiting on the paddle = gentle breathing.
        float speedGlow = flying ? Mathf.Clamp(ball.CurrentSpeed / referenceSpeed, 1f, 2f) : 1f;
        float breathe = flying ? 1f : 1f + readyPulse * Mathf.Sin(time * 4f);
        float intensity = speedGlow * breathe;

        // While phasing the halo turns violet and swells and breathes a little (a soft aura around the ghost).
        Color glow = Color.Lerp(glowColor, ghostHue, phaseBlend);
        float haloScale = haloSize * Mathf.Lerp(1f, 1.3f + 0.15f * Mathf.Sin(phaseTime * 9f), phaseBlend);
        halo.transform.localScale = Vector3.one * (haloScale / Mathf.Max(0.0001f, transform.lossyScale.x));

        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null) halo.transform.rotation = viewCamera.transform.rotation;
        NeonGlow.SetColor(halo, NeonGlow.Hdr(glow, haloIntensity * intensity));

        floorGlow.transform.SetPositionAndRotation(new Vector3(position.x, floorY, position.z), Quaternion.Euler(90f, 0f, 0f));
        NeonGlow.SetColor(floorGlow, NeonGlow.Hdr(glow, floorGlowIntensity * intensity));
    }

    /// <summary>Eases the ghost look in and out (quick in, slower out so the end of the buff is felt).</summary>
    private float UpdatePhaseBlend(float dt)
    {
        bool phasing = ball.Phasing;
        if (phasing && !wasPhasing)
        {
            // Turning into a ghost: a ring and a pale flash on the ball.
            BuildPhaseGradients();
            NeonFx.Ring(ball.Position, NeonGlow.Hdr(ghostHue, 1.8f), 22, 3.5f, 0.08f, 0.3f);
            NeonFx.Flash(ball.Position, NeonGlow.Hdr(Color.Lerp(Color.white, ghostHue, 0.3f), 1.5f), 0.8f, 0.12f);
            kickT = 0f;
        }
        wasPhasing = phasing;

        float rate = dt / (phasing ? phaseFadeIn : phaseFadeOut);
        phaseBlend = Mathf.MoveTowards(phaseBlend, phasing ? 1f : 0f, rate);
        return phaseBlend;
    }

    /// <summary>Core colors, shell, trails and afterimages for the current phase blend.</summary>
    private void UpdateGhost(float dt, Quaternion axis, float stretch, bool flying)
    {
        phaseTime += dt;
        float blend = Mathf.SmoothStep(0f, 1f, phaseBlend);

        // Core: pale violet instead of orange. Cleared at 0 so the ball keeps its plain material (and batching).
        if (blend > 0f)
        {
            Color pale = Color.Lerp(Color.white, ghostHue, 0.5f);
            bodyBlock.SetColor(CoreColorId, Color.Lerp(baseCoreColor, NeonGlow.Hdr(pale, 1.4f), blend));
            bodyBlock.SetColor(RimColorId, Color.Lerp(baseRimColor, NeonGlow.Hdr(ghostHue, 2f), blend));
            bodyRenderer.SetPropertyBlock(bodyBlock);
        }
        else
        {
            bodyRenderer.SetPropertyBlock(null);
        }

        // Shell: bigger than the ball, stretched a bit more along the path (a comet of light), wavering brightness.
        if (shell != null)
        {
            shell.enabled = blend > 0.001f;
            if (shell.enabled)
            {
                kickT = Mathf.Min(1f, kickT + dt / passKickTime);
                float kick = passKick * (1f - kickT) * (1f - kickT);
                float wobble = 1f + 0.06f * Mathf.Sin(phaseTime * 11f);
                float along = 1f + stretch * 1.4f;
                float across = 1f / Mathf.Sqrt(Mathf.Max(0.1f, along));
                float size = shellScale * Mathf.Lerp(0.6f, 1f, blend) * wobble * (1f + kick);
                shell.transform.rotation = axis;
                shell.transform.localScale = new Vector3(across, across, along) * size;

                float noise = Mathf.PerlinNoise(phaseTime * shellFlickerSpeed, 0.37f) * 2f - 1f;
                float brightness = shellIntensity * blend * (1f + shellFlicker * noise) * (1f + kick * 2f);
                shellBlock.SetColor(GhostColorId, NeonGlow.Hdr(ghostHue, Mathf.Max(0f, brightness)));
                shell.SetPropertyBlock(shellBlock);
            }
        }

        // Trails: blend toward the ghost gradients (rebuilt only while the blend moves).
        bool settled = blend == 0f || blend == 1f;
        if (outerTrail != null && (Mathf.Abs(blend - trailBlend) > 0.02f || (settled && blend != trailBlend)))
        {
            trailBlend = blend;
            outerTrail.colorGradient = LerpGradient(outerNormal, outerPhase, blend);
            outerTrail.time = Mathf.Lerp(trailTime, phaseTrailTime, blend);
            outerTrail.widthMultiplier = outerWidth * Mathf.Lerp(1f, 1.2f, blend);
            NeonGlow.SetColor(outerTrail, NeonGlow.Hdr(Color.white, Mathf.Lerp(trailBrightness, phaseTrailBrightness, blend)));
            if (coreTrail != null)
            {
                coreTrail.colorGradient = LerpGradient(coreNormal, corePhase, blend);
                NeonGlow.SetColor(coreTrail, NeonGlow.Hdr(Color.white, Mathf.Lerp(trailBrightness * 1.3f, phaseTrailBrightness * 1.3f, blend)));
            }
        }

        // Afterimages: fading squares left along the path, so the ghost reads even at the pixelated resolution.
        if (ball.Phasing && flying && Time.timeScale > 0f && Time.time >= nextEcho)
        {
            nextEcho = Time.time + echoInterval;
            NeonFx.Flash(ball.Position, NeonGlow.Hdr(ghostHue, echoIntensity), echoSize, echoLifetime);
        }
    }

    /// <summary>Blends two gradients that share the same key layout (key times taken from <paramref name="a"/>).</summary>
    private static Gradient LerpGradient(Gradient a, Gradient b, float t)
    {
        if (a == null || b == null) return a ?? b;
        GradientColorKey[] colors = a.colorKeys;
        GradientAlphaKey[] alphas = a.alphaKeys;
        GradientColorKey[] colorsB = b.colorKeys;
        GradientAlphaKey[] alphasB = b.alphaKeys;
        for (int i = 0; i < colors.Length && i < colorsB.Length; i++)
            colors[i].color = Color.Lerp(colors[i].color, colorsB[i].color, t);
        for (int i = 0; i < alphas.Length && i < alphasB.Length; i++)
            alphas[i].alpha = Mathf.Lerp(alphas[i].alpha, alphasB[i].alpha, t);
        return MakeGradient(colors, alphas);
    }

    private static Gradient MakeGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        var gradient = new Gradient();
        gradient.SetKeys(colors, alphas);
        return gradient;
    }
}
