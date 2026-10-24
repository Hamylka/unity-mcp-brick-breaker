using UnityEngine;

/// <summary>
/// The look of a falling <see cref="BonusItem"/>: a faceted neon crystal (dark glassy center, bright edges) that
/// bobs and spins inside a tilted orbiting ring, wrapped in a soft halo, casting a pool of light on the floor and
/// leaving a short trail. Everything is tinted with the bonus color and built in code at <see cref="Build"/>,
/// so the prefab only needs this component and the crystal material.
/// </summary>
public class BonusItemVisual : MonoBehaviour
{
    private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");

    [Tooltip("Material using the BrickBreaker/Neon Fresnel shader.")]
    [SerializeField] private Material crystalMaterial;

    [Header("Crystal (world units)")]
    [SerializeField, Min(0.05f)] private float crystalRadius = 0.27f;
    [SerializeField, Min(0.05f)] private float crystalHeight = 0.8f;
    [Tooltip("Brightness of the crystal's center, as a fraction of the bonus color.")]
    [SerializeField, Range(0f, 2f)] private float coreBrightness = 0.6f;
    [Tooltip("HDR brightness of the crystal's edges (above 1 blooms).")]
    [SerializeField, Min(0f)] private float rimIntensity = 5f;
    [SerializeField, Min(0f)] private float bobHeight = 0.06f;
    [SerializeField, Min(0f)] private float bobSpeed = 5f;

    [Header("Ring")]
    [SerializeField, Min(0.05f)] private float ringRadius = 0.46f;
    [SerializeField, Min(0.005f)] private float ringWidth = 0.045f;
    [SerializeField, Range(0f, 80f)] private float ringTilt = 16f;
    [SerializeField] private float ringSpin = -140f;
    [SerializeField, Min(0f)] private float ringIntensity = 3f;

    [Header("Glow")]
    [SerializeField, Min(0f)] private float haloSize = 1.7f;
    [SerializeField, Min(0f)] private float haloIntensity = 2f;
    [SerializeField, Min(0f)] private float floorGlowSize = 2.3f;
    [SerializeField, Min(0f)] private float floorGlowIntensity = 1.6f;
    [Tooltip("World height of the floor the light pool lies on.")]
    [SerializeField] private float floorY = 0.02f;
    [SerializeField, Range(0f, 1f)] private float pulseDepth = 0.25f;
    [SerializeField, Min(0f)] private float pulseSpeed = 6f;

    [Header("Trail")]
    [SerializeField, Min(0f)] private float trailTime = 0.25f;
    [SerializeField, Min(0f)] private float trailWidth = 0.22f;

    private Transform crystal;
    private Transform ring;
    private MeshRenderer halo;
    private MeshRenderer floorGlow;
    private Color color;
    private float time;
    private float ringAngle;
    private Camera viewCamera;

    /// <summary>Builds the visual in the bonus color. Hides the item's own placeholder renderer.</summary>
    public void Build(Color bonusColor, Renderer placeholder)
    {
        color = bonusColor;
        if (placeholder != null) placeholder.enabled = false;
        time = Random.value * 10f; // items dropped together do not pulse in sync

        BuildCrystal();
        BuildRing();

        halo = NeonGlow.CreateQuad(transform, "Halo", 1f);
        floorGlow = NeonGlow.CreateQuad(transform, "FloorGlow", 1f);
        SetWorldSize(halo.transform, haloSize);
        SetWorldSize(floorGlow.transform, floorGlowSize);

        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = NeonGlow.LineMaterial;
        trail.time = trailTime;
        trail.widthMultiplier = trailWidth;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        trail.colorGradient = Fade();
        NeonGlow.SetColor(trail, NeonGlow.Hdr(color, 1.5f));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.minVertexDistance = 0.05f;

        viewCamera = Camera.main;
        LateUpdate();
    }

    private void BuildCrystal()
    {
        var go = new GameObject("Crystal", typeof(MeshFilter), typeof(MeshRenderer));
        crystal = go.transform;
        crystal.SetParent(transform, false);
        SetWorldSize(crystal, 1f);
        go.GetComponent<MeshFilter>().sharedMesh = CrystalMesh.Get(crystalRadius, crystalHeight);

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = crystalMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor(CoreColorId, NeonGlow.Hdr(color, coreBrightness));
        block.SetColor(RimColorId, NeonGlow.Hdr(Color.Lerp(color, Color.white, 0.25f), rimIntensity));
        renderer.SetPropertyBlock(block);
    }

    private void BuildRing()
    {
        var go = new GameObject("Ring");
        ring = go.transform;
        ring.SetParent(transform, false);

        const int segments = 32;
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.widthMultiplier = ringWidth;
        line.sharedMaterial = NeonGlow.LineMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        // Vertex colors are clamped to 1, so the HDR tint goes through the material color instead.
        line.startColor = line.endColor = Color.white;
        NeonGlow.SetColor(line, NeonGlow.Hdr(color, ringIntensity));

        // Positions are local to the ring, which is scaled so they come out in world units.
        SetWorldSize(ring, 1f);
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ringRadius);
        }
    }

    private void LateUpdate()
    {
        if (crystal == null) return;
        float dt = Time.deltaTime;
        time += dt;
        float pulse = 1f + pulseDepth * Mathf.Sin(time * pulseSpeed);

        // The crystal bobs; it spins with the item (the item's rigidbody turns around Y).
        crystal.localPosition = Vector3.up * (Mathf.Sin(time * bobSpeed) * bobHeight / Scale());

        // The ring keeps its own (wobbling) tilt and turns the other way, whatever the item's spin.
        // Near-flat, it reads as an orbit around the crystal from the high camera instead of an edge-on line.
        ringAngle += ringSpin * dt;
        float tilt = ringTilt + Mathf.Sin(time * 2.3f) * 10f;
        ring.SetPositionAndRotation(crystal.position,
            Quaternion.AngleAxis(tilt, Vector3.right) * Quaternion.AngleAxis(ringAngle, Vector3.up));

        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null) halo.transform.SetPositionAndRotation(crystal.position, viewCamera.transform.rotation);
        NeonGlow.SetColor(halo, NeonGlow.Hdr(color, haloIntensity * pulse));

        Vector3 p = transform.position;
        floorGlow.transform.SetPositionAndRotation(new Vector3(p.x, floorY, p.z), Quaternion.Euler(90f, 0f, 0f));
        NeonGlow.SetColor(floorGlow, NeonGlow.Hdr(color, floorGlowIntensity * pulse));
    }

    // Children are sized in world units even though the item root is scaled.
    private float Scale() => Mathf.Max(0.0001f, transform.lossyScale.x);
    private void SetWorldSize(Transform child, float worldSize) => child.localScale = Vector3.one * (worldSize / Scale());

    // White, fading out: the hue and HDR brightness come from the material color.
    private static Gradient Fade()
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
