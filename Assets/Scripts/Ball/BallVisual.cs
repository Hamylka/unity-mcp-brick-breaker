using UnityEngine;

/// <summary>
/// The ball's look, kept apart from its physics: a white-hot core in a glowing orange shell (Neon Fresnel shader),
/// a soft halo, a pool of light on the floor under it, a two-layer trail (hot thin core inside a wide
/// orange-to-magenta tail), squash and stretch along its path, and sparks on every bounce.
/// The mesh is drawn by a child so it can stretch without touching the collider. Works on extra balls too
/// (they are copies, so the children already exist and are reused).
/// </summary>
[RequireComponent(typeof(BallController))]
[DisallowMultipleComponent]
public class BallVisual : MonoBehaviour
{
    private const string BodyName = "BallBody";
    private const string HaloName = "BallHalo";
    private const string FloorName = "BallFloorGlow";
    private const string CoreTrailName = "BallCoreTrail";

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

    private BallController ball;
    private Transform body;
    private MeshRenderer halo;
    private MeshRenderer floorGlow;
    private Camera viewCamera;
    private float referenceSpeed;
    private float squashT = 1f;
    private Vector3 squashNormal = Vector3.forward;
    private float time;

    private void Awake()
    {
        ball = GetComponent<BallController>();
        BuildBody();
        halo = FindOrCreateQuad(HaloName, haloSize);
        floorGlow = FindOrCreateQuad(FloorName, floorGlowSize);
        StyleTrails();
    }

    private void OnEnable() => ball.Bounced += HandleBounce;
    private void OnDisable() => ball.Bounced -= HandleBounce;

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

        outer.time = trailTime;
        outer.colorGradient = MakeGradient(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.12f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.15f, 0.45f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.3f), new GradientAlphaKey(0f, 1f) });
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
        core.colorGradient = MakeGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.85f, 0.5f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        NeonGlow.SetColor(core, NeonGlow.Hdr(Color.white, trailBrightness * 1.3f));
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
        NeonFx.Sparks(point, normal, glowColor, sparkCount, sparkSpeed, 70f, 0.07f, 0.22f);
        NeonFx.Flash(point, new Color(1f, 0.85f, 0.6f), 0.35f, 0.07f);
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
        body.rotation = axis;
        body.localScale = new Vector3(across, across, along);

        // Faster than at the start (boosts, speed upgrades) = brighter glow. Waiting on the paddle = gentle breathing.
        float speedGlow = flying ? Mathf.Clamp(ball.CurrentSpeed / referenceSpeed, 1f, 2f) : 1f;
        float breathe = flying ? 1f : 1f + readyPulse * Mathf.Sin(time * 4f);
        float intensity = speedGlow * breathe;

        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null) halo.transform.rotation = viewCamera.transform.rotation;
        NeonGlow.SetColor(halo, NeonGlow.Hdr(glowColor, haloIntensity * intensity));

        floorGlow.transform.SetPositionAndRotation(new Vector3(position.x, floorY, position.z), Quaternion.Euler(90f, 0f, 0f));
        NeonGlow.SetColor(floorGlow, NeonGlow.Hdr(glowColor, floorGlowIntensity * intensity));
    }

    private static Gradient MakeGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        var gradient = new Gradient();
        gradient.SetKeys(colors, alphas);
        return gradient;
    }
}
