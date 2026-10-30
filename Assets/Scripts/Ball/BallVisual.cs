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
/// While it deals extra damage (<see cref="BallController.DamageMultiplier"/> above 1, the 2x damage wall buff) it is
/// powered up: bigger, white-hot in a gold shell, two energy arcs spinning around it, a throbbing gold halo, a wide
/// gold trail, crackling sparks, and every brick hit lands with a heavy flash, ring and crunch.
/// In the last seconds of a wall buff (<see cref="BuffTimeLeft"/>) both looks strobe, faster and faster.
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
    private const string ArcNameA = "BallPowerArcA";
    private const string ArcNameB = "BallPowerArcB";
    private const int ArcSegments = 12;

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
    [Header("Speed Look (run ramp)")]
    [Tooltip("Extra trail time per +100% of run speed (the trail is already longer in space since the ball is faster).")]
    [SerializeField, Min(0f)] private float speedTrailTimeGain = 0.2f;
    [Tooltip("Extra trail brightness per +100% of run speed.")]
    [SerializeField, Min(0f)] private float speedTrailBrightnessGain = 0.15f;
    [Tooltip("Run speed bonus (1 = +100%) after which the look stops growing.")]
    [SerializeField, Min(0.1f)] private float speedLookMax = 1.5f;
    [Tooltip("Seconds to ease the look to a new speed step.")]
    [SerializeField, Min(0.05f)] private float speedLookEase = 1f;

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

    [Header("Power Look (extra damage)")]
    [Tooltip("Power color; the 2x damage wall buff overrides it with its own color when applied.")]
    [SerializeField] private Color powerColor = new(1f, 0.75f, 0.1f, 1f);
    [SerializeField, Min(0.01f)] private float powerFadeIn = 0.15f;
    [SerializeField, Min(0.01f)] private float powerFadeOut = 0.4f;
    [Tooltip("Ball size while powered (1 = normal).")]
    [SerializeField, Range(1f, 2f)] private float powerBodyScale = 1.25f;
    [Tooltip("Heartbeat of the powered ball (size, halo and arcs throb), in beats per second.")]
    [SerializeField, Min(0f)] private float throbRate = 2.5f;
    [Tooltip("Radius of the two spinning energy arcs, in world units.")]
    [SerializeField, Min(0f)] private float arcRadius = 0.62f;
    [SerializeField, Min(0f)] private float arcWidth = 0.09f;
    [Tooltip("Arc length of each of the two arcs, in degrees.")]
    [SerializeField, Range(20f, 170f)] private float arcSpan = 120f;
    [SerializeField] private float arcSpin = 600f;
    [SerializeField, Min(0f)] private float arcIntensity = 4f;
    [SerializeField, Min(1f)] private float powerHaloScale = 1.5f;
    [SerializeField, Min(1f)] private float powerTrailWidth = 1.6f;
    [SerializeField, Min(0f)] private float powerTrailBrightness = 1.2f;
    [Tooltip("Seconds between two crackling sparks around the powered ball.")]
    [SerializeField, Min(0.01f)] private float crackleInterval = 0.06f;
    [Tooltip("Seconds between two tiny lightning arcs jumping off the powered ball.")]
    [SerializeField, Min(0.05f)] private float boltInterval = 0.3f;

    [Header("Echo Charge")]
    [Tooltip("How far the halo and floor light turn echo blue when the next hit echoes (0 = off).")]
    [SerializeField, Range(0f, 1f)] private float echoTint = 0.5f;
    [Tooltip("Extra halo brightness from the shimmer at full charge.")]
    [SerializeField, Min(0f)] private float echoShimmer = 0.35f;
    [Tooltip("Shimmer flicker speed at full charge, in cycles per second (slower at lower charge).")]
    [SerializeField, Min(0f)] private float echoShimmerRate = 14f;
    [SerializeField, Min(0.01f)] private float echoChargeEase = 0.12f;

    [Header("Buff Ending")]
    [Tooltip("The buffed look strobes for this many seconds before the wall buff runs out.")]
    [SerializeField, Min(0f)] private float warnTime = 2f;

    /// <summary>Ghost color (set by the Phase wall buff so the ball, zone and HUD ring match).</summary>
    public Color PhaseColor { get => phaseColor; set => phaseColor = value; }

    /// <summary>Power color (set by the damage wall buff so the ball, zone and HUD ring match).</summary>
    public Color PowerColor { get => powerColor; set { powerColor = value; BuildPowerGradients(); } }

    // Halo and floor light of the powered ball: the power color pulled toward amber, so the bloom does not wash it
    // out to lemon yellow.
    private Color PowerGlow => Color.Lerp(powerColor, new Color(1f, 0.45f, 0.05f), 0.35f);

    /// <summary>Seconds left on the wall buff this ball carries (infinity = none). Set by <see cref="BallBuffAura"/>.</summary>
    public float BuffTimeLeft { get; set; } = float.PositiveInfinity;

    /// <summary>Trail width factor 0-1 (a fading multi-ball extra). Applied on top of the buff looks, so they never fight.</summary>
    public float LifeFade { get; set; } = 1f;

    // Trail width of the plain ball. Serialized (hidden) so a clone made while the source is buffed (its trail is wider
    // then) still starts from the plain width instead of baking the buffed one in.
    [SerializeField, HideInInspector] private float baseTrailWidth;

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
    private Gradient outerPower, corePower;
    private LineRenderer arcA, arcB;
    private float powerBlend;
    private float trailPower = -1f;
    private float speedLook;          // eased run-speed bonus (0 = start speed), drives the trail length and brightness
    private float trailSpeedLook = -1f;
    private bool wasPowered;
    private float arcAngle;
    private float arcKick;     // 1 right after a powered brick hit, eases back to 0
    private float nextCrackle;
    private float nextBolt;
    private float warn = 1f;   // brightness factor of the buffed look (strobes before the buff ends)
    private float echoBlend;   // eased PowerUpStats.EchoCharge (0 = far from an echo, 1 = the next hit echoes)

    private void Awake()
    {
        ball = GetComponent<BallController>();
        BuildBody();
        halo = FindOrCreateQuad(HaloName, haloSize);
        floorGlow = FindOrCreateQuad(FloorName, floorGlowSize);
        BuildShell();
        arcA = FindOrCreateArc(ArcNameA);
        arcB = FindOrCreateArc(ArcNameB);
        StyleTrails();
    }

    /// <summary>One of the two energy arcs spinning around the powered ball (world-space line, hidden until needed).</summary>
    private LineRenderer FindOrCreateArc(string childName)
    {
        Transform existing = transform.Find(childName);
        LineRenderer line = existing != null ? existing.GetComponent<LineRenderer>() : null;
        if (line == null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = NeonGlow.LineMaterial;
            line.useWorldSpace = true;
            line.positionCount = ArcSegments + 1;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            // Bright head, fading tail: reads as spinning.
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 1f) });
            line.colorGradient = fade;
        }
        line.enabled = false;
        return line;
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
        if (baseTrailWidth <= 0f) baseTrailWidth = outer.widthMultiplier;
        outerWidth = baseTrailWidth;
        outer.widthMultiplier = outerWidth;

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
        BuildPowerGradients();
    }

    /// <summary>
    /// Powered versions of the two trails, same key layout as the normal ones: white-hot head, the power color,
    /// then a deep red-orange fade, fully opaque (heavy, solid).
    /// </summary>
    private void BuildPowerGradients()
    {
        Color deep = Color.Lerp(powerColor, new Color(1f, 0.1f, 0.05f), 0.6f);
        outerPower = MakeGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(PowerGlow, 0.25f), new GradientColorKey(deep, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.95f, 0.3f), new GradientAlphaKey(0f, 1f) });
        corePower = MakeGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.Lerp(Color.white, powerColor, 0.5f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trailPower = -1f; // force a refresh
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

        // Powered ball hitting a brick: a heavy impact so the extra damage is felt.
        if (powerBlend > 0.5f && collision.collider.GetComponentInParent<BrickStack>() != null)
        {
            Color hot = NeonGlow.Hdr(powerColor, 2f);
            NeonFx.Flash(point, NeonGlow.Hdr(Color.Lerp(Color.white, powerColor, 0.3f), 2f), 0.9f, 0.1f);
            NeonFx.Ring(point, hot, 18, 3.5f, 0.08f, 0.25f);
            NeonFx.Sparks(point, normal, hot, sparkCount + 6, sparkSpeed * 1.6f, 55f, 0.08f, 0.3f);
            squashT = 0f;
            arcKick = 1f;
            SoundManager.Play(Sfx.PowerHit, Random.Range(0.95f, 1.05f));
        }
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
        UpdateWarning();
        float power = UpdatePowerBlend(dt);
        // Heartbeat: a short sharp beat, then rest.
        float throb = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(time * throbRate * 2f * Mathf.PI), 6f) * power;
        float powerScale = Mathf.Lerp(1f, powerBodyScale, power) * (1f + 0.07f * throb);
        float coreScale = Mathf.Lerp(1f, phaseCoreScale, UpdatePhaseBlend(dt)) * powerScale;
        body.rotation = axis;
        body.localScale = new Vector3(across, across, along) * coreScale;
        UpdateGhost(dt, axis, stretch, flying);
        UpdatePower(dt, throb);

        // Faster than at the start (boosts, speed upgrades) = brighter glow. Waiting on the paddle = gentle breathing.
        float speedGlow = flying ? Mathf.Clamp(ball.CurrentSpeed / referenceSpeed, 1f, 2f) : 1f;
        float breathe = flying ? 1f : 1f + readyPulse * Mathf.Sin(time * 4f);
        float intensity = speedGlow * breathe;

        // While phasing the halo turns violet and swells and breathes a little (a soft aura around the ghost).
        // While powered it turns gold, grows and throbs with the heartbeat.
        Color glow = Color.Lerp(Color.Lerp(glowColor, PowerGlow, power), ghostHue, phaseBlend);
        intensity *= Mathf.Lerp(1f, (1.15f + 0.6f * throb) * warn, power);
        // Echo charging: every flying ball (any of them can fire the shared counter) shimmers pale blue over the last
        // hits before an echo, faster and brighter as it nears. Drops at once when the echo fires.
        float echoTarget = flying ? PowerUpStats.EchoCharge : 0f;
        echoBlend = echoTarget < echoBlend ? echoTarget : Mathf.MoveTowards(echoBlend, echoTarget, dt / echoChargeEase);
        if (echoBlend > 0.001f)
        {
            float shimmer = 0.5f + 0.5f * Mathf.Sin(time * echoShimmerRate * Mathf.Lerp(0.4f, 1f, echoBlend) * 2f * Mathf.PI);
            glow = Color.Lerp(glow, PowerUpStats.EchoColor, echoTint * echoBlend);
            intensity *= 1f + echoShimmer * echoBlend * shimmer;
        }
        float haloScale = haloSize * Mathf.Lerp(1f, 1.3f + 0.15f * Mathf.Sin(phaseTime * 9f), phaseBlend)
                          * Mathf.Lerp(1f, powerHaloScale * (1f + 0.15f * throb), power);
        float inverseScale = 1f / Mathf.Max(0.0001f, transform.lossyScale.x);
        halo.transform.localScale = Vector3.one * (haloScale * inverseScale);
        floorGlow.transform.localScale = Vector3.one * (floorGlowSize * Mathf.Lerp(1f, 1.4f, power) * inverseScale);

        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null) halo.transform.rotation = viewCamera.transform.rotation;
        NeonGlow.SetColor(halo, NeonGlow.Hdr(glow, haloIntensity * intensity));

        floorGlow.transform.SetPositionAndRotation(new Vector3(position.x, floorY, position.z), Quaternion.Euler(90f, 0f, 0f));
        NeonGlow.SetColor(floorGlow, NeonGlow.Hdr(glow, floorGlowIntensity * intensity));
    }

    /// <summary>Strobes the buffed look in the last seconds of a wall buff, faster and faster.</summary>
    private void UpdateWarning()
    {
        float left = BuffTimeLeft;
        if (left >= warnTime || warnTime <= 0f) { warn = 1f; return; }
        float urgency = 1f - Mathf.Clamp01(left / warnTime);
        float rate = Mathf.Lerp(4f, 11f, urgency); // blinks per second
        warn = Mathf.Repeat(time * rate, 1f) < 0.6f ? 1f : 0.3f;
    }

    /// <summary>Eases the power look in and out; a burst when it starts.</summary>
    private float UpdatePowerBlend(float dt)
    {
        bool powered = ball.DamageMultiplier > 1.01f;
        if (powered && !wasPowered)
        {
            Vector3 position = ball.Position;
            NeonFx.Ring(position, NeonGlow.Hdr(powerColor, 1.8f), 22, 3.5f, 0.08f, 0.3f);
            NeonFx.Flash(position, NeonGlow.Hdr(Color.Lerp(Color.white, powerColor, 0.3f), 1.8f), 0.9f, 0.12f);
            NeonFx.Sparks(position, Vector3.up, NeonGlow.Hdr(powerColor, 1.5f), 10, 4f, 50f, 0.07f, 0.3f);
            arcKick = 1f;
        }
        wasPowered = powered;

        float rate = dt / (powered ? powerFadeIn : powerFadeOut);
        powerBlend = Mathf.MoveTowards(powerBlend, powered ? 1f : 0f, rate);
        return Mathf.SmoothStep(0f, 1f, powerBlend);
    }

    /// <summary>The two spinning energy arcs, crackling sparks and tiny lightning arcs of the powered ball.</summary>
    private void UpdatePower(float dt, float throb)
    {
        float power = Mathf.SmoothStep(0f, 1f, powerBlend);
        bool show = power > 0.001f && arcA != null && arcB != null;
        if (arcA != null) arcA.enabled = show;
        if (arcB != null) arcB.enabled = show;
        if (!show) return;

        arcKick = Mathf.MoveTowards(arcKick, 0f, dt / 0.25f);
        arcAngle += arcSpin * dt * (1f + 1.5f * arcKick);
        float radius = arcRadius * Mathf.Lerp(0.5f, 1f, power) * (1f + 0.12f * throb + 0.35f * arcKick * arcKick);
        Color color = NeonGlow.Hdr(Color.Lerp(powerColor, Color.white, 0.25f * throb + 0.5f * arcKick), arcIntensity * power * warn);
        DrawArc(arcA, arcAngle, radius, color);
        DrawArc(arcB, arcAngle + 180f, radius, color);

        if (!wasPowered || !ball.IsFlying || Time.timeScale <= 0f) return;
        Vector3 position = ball.Position;
        if (Time.time >= nextCrackle)
        {
            nextCrackle = Time.time + crackleInterval;
            Vector3 direction = RandomFlatDirection();
            NeonFx.Sparks(position + direction * radius * 0.8f, direction, NeonGlow.Hdr(powerColor, 1.6f), 2, 2.5f, 30f, 0.05f, 0.15f);
        }
        if (Time.time >= nextBolt)
        {
            nextBolt = Time.time + boltInterval * Random.Range(0.6f, 1.4f);
            ChainBolt.Spawn(position, position + RandomFlatDirection() * radius * 1.7f,
                NeonGlow.Hdr(Color.Lerp(powerColor, Color.white, 0.4f), 2f), 0.08f, 0.1f, 0.05f);
        }
    }

    private void DrawArc(LineRenderer line, float startDegrees, float radius, Color color)
    {
        Vector3 center = transform.position;
        for (int i = 0; i <= ArcSegments; i++)
        {
            float angle = (startDegrees + arcSpan * i / ArcSegments) * Mathf.Deg2Rad;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
        line.widthMultiplier = arcWidth;
        NeonGlow.SetColor(line, color);
    }

    private static Vector3 RandomFlatDirection()
    {
        float angle = Random.value * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
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

        // Core: white-hot in a gold rim while powered, pale violet while phasing.
        // Cleared at 0 so the ball keeps its plain material (and batching).
        float power = Mathf.SmoothStep(0f, 1f, powerBlend);
        if (blend > 0f || power > 0f)
        {
            Color pale = Color.Lerp(Color.white, ghostHue, 0.5f);
            Color coreColor = Color.Lerp(baseCoreColor, NeonGlow.Hdr(Color.Lerp(Color.white, powerColor, 0.5f), 1.5f * (0.6f + 0.4f * warn)), power);
            Color rimColor = Color.Lerp(baseRimColor, NeonGlow.Hdr(powerColor, 3.5f * (0.5f + 0.5f * warn)), power);
            bodyBlock.SetColor(CoreColorId, Color.Lerp(coreColor, NeonGlow.Hdr(pale, 1.4f), blend));
            bodyBlock.SetColor(RimColorId, Color.Lerp(rimColor, NeonGlow.Hdr(ghostHue, 2f), blend));
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
                float brightness = shellIntensity * blend * (1f + shellFlicker * noise) * (1f + kick * 2f) * warn;
                shellBlock.SetColor(GhostColorId, NeonGlow.Hdr(ghostHue, Mathf.Max(0f, brightness)));
                shell.SetPropertyBlock(shellBlock);
            }
        }

        // Trails: blend toward the power, then the ghost gradients (rebuilt only while a blend moves).
        bool settled = (blend == 0f || blend == 1f) && (power == 0f || power == 1f);
        speedLook = Mathf.MoveTowards(speedLook, Mathf.Min(speedLookMax, BallController.RunSpeedFactor - 1f), dt * speedLookMax / speedLookEase);
        bool speedMoved = Mathf.Abs(speedLook - trailSpeedLook) > 0.01f;
        bool moved = Mathf.Abs(blend - trailBlend) > 0.02f || Mathf.Abs(power - trailPower) > 0.02f;
        if (outerTrail != null && (moved || speedMoved || (settled && (blend != trailBlend || power != trailPower))))
        {
            trailBlend = blend;
            trailPower = power;
            trailSpeedLook = speedLook;
            float brightness = Mathf.Lerp(Mathf.Lerp(trailBrightness, powerTrailBrightness, power), phaseTrailBrightness, blend) * (1f + speedTrailBrightnessGain * speedLook);
            outerTrail.colorGradient = LerpGradient(LerpGradient(outerNormal, outerPower, power), outerPhase, blend);
            outerTrail.time = Mathf.Lerp(Mathf.Lerp(trailTime, trailTime * 1.15f, power), phaseTrailTime, blend) * (1f + speedTrailTimeGain * speedLook);
            NeonGlow.SetColor(outerTrail, NeonGlow.Hdr(Color.white, brightness));
            if (coreTrail != null)
            {
                coreTrail.colorGradient = LerpGradient(LerpGradient(coreNormal, corePower, power), corePhase, blend);
                NeonGlow.SetColor(coreTrail, NeonGlow.Hdr(Color.white, brightness * 1.3f));
            }
        }

        // Widths follow the buff blends and the life fade every frame, so they ease back to plain when a buff ends.
        if (outerTrail != null)
        {
            float powerWidth = Mathf.Lerp(1f, powerTrailWidth, power);
            outerTrail.widthMultiplier = outerWidth * powerWidth * Mathf.Lerp(1f, 1.2f, blend) * LifeFade;
            if (coreTrail != null) coreTrail.widthMultiplier = outerWidth * coreTrailWidth * powerWidth * LifeFade;
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
