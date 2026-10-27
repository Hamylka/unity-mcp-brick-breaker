using System;
using UnityEngine;

/// <summary>
/// One brick (one layer of a <see cref="BrickStack"/>): hit points, color, and the animations
/// (hit punch, destruction shrink, falling one layer with a soft landing).
/// Raises <see cref="Damaged"/> and <see cref="Destroyed"/> so other systems (bonus drops, XP, score, FX)
/// can react without the brick knowing about them. The ball never touches a brick directly:
/// the stack's collider receives the hit and forwards it to the bottom brick.
/// </summary>
public class Brick : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private BrickPalette palette;

    [Header("Neon")]
    [Tooltip("Brightness of the brick body compared to its HP color (the glow does the rest).")]
    [SerializeField, Range(0f, 1f)] private float bodyBrightness = 0.15f;
    [Tooltip("Glow strength of the neon outline (the material's emission map). Above 1 the bloom picks it up.")]
    [SerializeField, Min(0f)] private float glowIntensity = 2.4f;

    [Header("Shape")]
    [Tooltip("Rounded corners with a built-in neon outline (visual only, the stack's box collider is unchanged).")]
    [SerializeField] private bool roundedShape = true;
    [SerializeField, Min(0f)] private float cornerRadius = 0.12f;
    [Tooltip("Width of the glowing outline band, in world units.")]
    [SerializeField, Min(0.005f)] private float outlineWidth = 0.05f;

    [Header("Hit Juice")]
    [SerializeField, Min(0f)] private float colorFadeTime = 0.12f;
    [SerializeField, Min(0f)] private float hitPunchScale = 0.15f;
    [SerializeField, Min(0.01f)] private float punchTime = 0.12f;
    [SerializeField, Min(0.01f)] private float destroyShrinkTime = 0.1f;

    [Header("Landing Juice")]
    [Tooltip("How much the brick squashes vertically when it lands after a fall.")]
    [SerializeField, Range(0f, 0.4f)] private float landSquash = 0.12f;
    [SerializeField, Min(0.01f)] private float landTime = 0.16f;

    [Header("Spawn Juice")]
    [Tooltip("Seconds for the pop-in animation of a freshly spawned brick.")]
    [SerializeField, Min(0.01f)] private float spawnTime = 0.3f;

    /// <summary>Brick, hit points actually removed (never more than it had left). Raised on every hit.</summary>
    public event Action<Brick, int> Hit;
    /// <summary>Brick, remaining hit points. Raised on every hit that does not destroy the brick.</summary>
    public event Action<Brick, int> Damaged;
    /// <summary>Raised once, the moment the brick's hit points reach zero.</summary>
    public event Action<Brick> Destroyed;

    /// <summary>The toughest brick currently spawning: the end of the palette gradient. Set by the grid.</summary>
    public static int ColorScaleMaxHp { get; private set; } = 5;

    private static event Action ColorScaleChanged;

    /// <summary>Changes the gradient scale. Every living brick recolors with a small pulse.</summary>
    public static void SetColorScale(int maxHitPoints)
    {
        int clamped = Mathf.Max(1, maxHitPoints);
        if (clamped == ColorScaleMaxHp) return;
        ColorScaleMaxHp = clamped;
        ColorScaleChanged?.Invoke();
    }

    /// <summary>Sets the scale without recoloring anyone (new run).</summary>
    public static void ResetColorScale(int maxHitPoints) => ColorScaleMaxHp = Mathf.Max(1, maxHitPoints);

    public int MaxHitPoints { get; private set; } = 1;
    public int HitPoints { get; private set; } = 1;
    public bool IsAlive => HitPoints > 0;
    /// <summary>Fractional damage waiting to become a whole HP (splash power-ups at low damage).</summary>
    public float DamageCarry { get; set; }

    private MaterialPropertyBlock block;
    private Vector3 baseScale = Vector3.one;
    private Color shownColor = Color.white;
    private Color fromColor = Color.white;
    private Color targetColor = Color.white;
    private float colorT = 1f;
    private float punchT = 1f;
    private float landT = 1f;
    private float shrinkT;
    private bool shrinking;
    private bool spawning;
    private float spawnDelay;
    private float spawnT;
    private float spawnK = 1f;

    private bool moving;
    private float moveDelay;
    private float moveT;
    private float moveDuration = 0.2f;
    private float fromY;
    private float toY;

    private void Awake()
    {
        baseScale = transform.localScale;
        enabled = false; // Update only runs while an animation is playing.
    }

    /// <summary>
    /// Sets the brick's size. With the rounded shape the mesh is built at that size and the scale stays 1,
    /// so the corners stay round instead of being stretched. Call before <see cref="Initialize"/>.
    /// </summary>
    public void ApplySize(Vector3 size)
    {
        var filter = GetComponent<MeshFilter>();
        if (roundedShape && filter != null)
        {
            filter.sharedMesh = RoundedBoxMesh.GetShared(size, cornerRadius, outlineWidth);
            transform.localScale = Vector3.one;
        }
        else
        {
            transform.localScale = size;
        }
    }

    /// <summary>Called by the grid right after spawning and sizing the brick.</summary>
    public void Initialize(int hitPoints, BrickPalette brickPalette)
    {
        palette = brickPalette;
        MaxHitPoints = HitPoints = Mathf.Max(1, hitPoints);
        baseScale = transform.localScale;
        shownColor = targetColor = CurrentColor();
        ApplyColor(shownColor);
        enabled = false;

        ColorScaleChanged -= OnColorScaleChanged;
        ColorScaleChanged += OnColorScaleChanged;
    }

    private void OnDestroy() => ColorScaleChanged -= OnColorScaleChanged;

    private Color CurrentColor() => palette != null ? palette.GetColor(HitPoints, ColorScaleMaxHp) : Color.white;

    // The scale moved (the wall got tougher): fade to the new color and pulse so the change is noticeable,
    // with a couple of embers rising off the brick in its new color.
    private void OnColorScaleChanged()
    {
        if (!IsAlive || shrinking) return;
        fromColor = shownColor;
        targetColor = CurrentColor();
        colorT = 0f;
        punchT = 0f;
        enabled = true;
        NeonFx.Embers(transform.position, targetColor, 2, 0.06f, 0.6f);
    }

    /// <summary>Starts hidden (scale 0) and pops in after a delay, with a small overshoot.</summary>
    public void PlaySpawn(float delay = 0f)
    {
        spawning = true;
        spawnDelay = delay;
        spawnT = 0f;
        spawnK = 0f;
        transform.localScale = Vector3.zero;
        enabled = true;
    }

    public void TakeHit(int damage = 1)
    {
        if (!IsAlive) return;

        int dealt = Mathf.Min(damage, HitPoints);
        HitPoints = Mathf.Max(0, HitPoints - damage);
        Hit?.Invoke(this, dealt);

        if (HitPoints > 0)
        {
            fromColor = shownColor;
            targetColor = CurrentColor();
            colorT = 0f;
            punchT = 0f;
            enabled = true;
            SoundManager.Play(Sfx.BrickHit, 1f + (MaxHitPoints - HitPoints) * 0.08f); // rises as the brick weakens
            Damaged?.Invoke(this, HitPoints);
            return;
        }

        SoundManager.Play(Sfx.BrickBreak);
        Destroyed?.Invoke(this);
        shrinking = true;
        shrinkT = 0f;
        enabled = true;
    }

    /// <summary>Smoothly moves the brick to a new local height (used when the layer below it is destroyed).</summary>
    public void FallTo(float localY, float duration, float delay = 0f)
    {
        if (!IsAlive) return;
        fromY = transform.localPosition.y;
        toY = localY;
        moveDuration = Mathf.Max(duration, 0.01f);
        moveDelay = delay;
        moveT = 0f;
        moving = true;
        enabled = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (shrinking)
        {
            shrinkT += dt / destroyShrinkTime;
            transform.localScale = Vector3.Lerp(baseScale, Vector3.zero, shrinkT);
            if (shrinkT >= 1f) Destroy(gameObject);
            return;
        }

        if (spawning)
        {
            if (spawnDelay > 0f)
            {
                spawnDelay -= dt;
            }
            else
            {
                spawnT = Mathf.Min(1f, spawnT + dt / spawnTime);
                // ease-out-back: grows past full size, then settles
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                float u = spawnT - 1f;
                spawnK = 1f + c3 * u * u * u + c1 * u * u;
                if (spawnT >= 1f)
                {
                    spawning = false;
                    spawnK = 1f;
                }
            }
        }

        if (moving)
        {
            if (moveDelay > 0f)
            {
                moveDelay -= dt;
            }
            else
            {
                moveT = Mathf.Min(1f, moveT + dt / moveDuration);
                float k = moveT * moveT * (3f - 2f * moveT); // smoothstep: soft start, soft landing
                Vector3 p = transform.localPosition;
                p.y = Mathf.Lerp(fromY, toY, k);
                transform.localPosition = p;
                if (moveT >= 1f)
                {
                    moving = false;
                    landT = 0f;
                }
            }
        }

        if (colorT < 1f)
        {
            colorT = Mathf.Min(1f, colorT + dt / Mathf.Max(colorFadeTime, 0.0001f));
            ApplyColor(Color.Lerp(fromColor, targetColor, colorT));
        }

        float punch = 0f;
        if (punchT < 1f)
        {
            punchT = Mathf.Min(1f, punchT + dt / punchTime);
            punch = hitPunchScale * Mathf.Sin(punchT * Mathf.PI);
        }

        float land = 0f;
        if (landT < 1f)
        {
            landT = Mathf.Min(1f, landT + dt / landTime);
            land = Mathf.Sin(landT * Mathf.PI);
        }

        Vector3 scale = baseScale * ((1f + punch) * spawnK);
        scale.y *= 1f - landSquash * land;
        scale.x *= 1f + landSquash * 0.5f * land;
        scale.z *= 1f + landSquash * 0.5f * land;
        transform.localScale = scale;

        if (!moving && !spawning && colorT >= 1f && punchT >= 1f && landT >= 1f)
        {
            transform.localScale = baseScale;
            enabled = false;
        }
    }

    private void ApplyColor(Color color)
    {
        shownColor = color;
        if (targetRenderer == null) return;
        block ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(block);
        Color body = color * bodyBrightness;
        body.a = 1f;
        block.SetColor(BaseColorId, body);
        block.SetColor(EmissionColorId, color * glowIntensity); // HDR glow in the HP color
        targetRenderer.SetPropertyBlock(block);
    }
}
