using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Look of the corruption screen's background (Inspector choice on <see cref="PowerUpScreen"/>).</summary>
public enum CorruptionBackdrop
{
    /// <summary>Digital: rolling band, scanlines, edge noise, tears.</summary>
    Glitch,
    /// <summary>Organic: dark red veins creeping in from the edges, pulsing with a slow heartbeat, embers drifting up.</summary>
    Veins
}

/// <summary>
/// The Veins backdrop of the corruption screen, and the switch between backdrops. Both share the blood-red field;
/// Glitch lives in <c>PowerUpScreen.Corruption.cs</c>. Everything here is calm in the inventory and only speeds up
/// with the virus of the sequence (level near 1).
/// </summary>
public partial class PowerUpScreen
{
    [Header("Corruption screen")]
    [Tooltip("Background of the corruption screen. Glitch: digital noise and tears. Veins: an organic infection pulsing with a heartbeat.")]
    [SerializeField] private CorruptionBackdrop backdrop = CorruptionBackdrop.Veins;
    [Tooltip("Veins only: a soft heartbeat sound on each beat while the inventory is open.")]
    [SerializeField] private bool backdropHeartbeat = true;

    private const int EmberCount = 22;
    private const float CalmBeatPeriod = 1.2f;  // seconds per heartbeat in the inventory
    private const float RacingBeatPeriod = 0.5f; // at the peak of the virus

    private sealed class Ember
    {
        public RectTransform rect;
        public Image image;
        public Vector2 pos;
        public float speed, sway, phase, life, maxLife, size;
        public Color color;
    }

    private RectTransform glitchLayer;
    private RectTransform veinsLayer;
    private RawImage veinsFar;   // a blurred, wider copy behind: the glow of the veins
    private RawImage veinsNear;  // the crisp veins
    private readonly List<Ember> embers = new();
    private float beatPhase;

    private void BuildVeinsLayer(RectTransform background)
    {
        veinsLayer = NewRect("Veins", background);
        Stretch(veinsLayer);

        Texture2D veins = BuildVeinTexture(7);
        veinsFar = NewRawIn("VeinGlow", veinsLayer, veins);
        veinsFar.enabled = true;
        veinsFar.rectTransform.localScale = new Vector3(1.04f, 1.04f, 1f);
        veinsNear = NewRawIn("Veins", veinsLayer, veins);
        veinsNear.enabled = true;

        for (int i = 0; i < EmberCount; i++)
        {
            Image image = NewImage("Ember", veinsLayer, Color.clear, false);
            image.sprite = null;
            image.type = Image.Type.Simple;
            var ember = new Ember { rect = image.rectTransform, image = image };
            Place(ember.rect, Vector2.zero, Vector2.zero, Vector2.one * 8f);
            RespawnEmber(ember, true);
            embers.Add(ember);
        }
    }

    // Shows the chosen backdrop's layer and hides the other.
    private void ApplyBackdrop()
    {
        if (glitchLayer != null) glitchLayer.gameObject.SetActive(backdrop == CorruptionBackdrop.Glitch);
        if (veinsLayer != null) veinsLayer.gameObject.SetActive(backdrop == CorruptionBackdrop.Veins);
    }

    private void AnimateVeins(float dt, float time, float level)
    {
        // Heartbeat: lub, then a softer dub. The phase accumulates so a change of pace never jumps.
        float period = Mathf.Lerp(CalmBeatPeriod, RacingBeatPeriod, level * level);
        float before = beatPhase;
        beatPhase = Mathf.Repeat(beatPhase + dt / period, 1f);
        if (beatPhase < before && backdropHeartbeat && phase == Phase.Inventory)
            SoundManager.Play(Sfx.DangerHeartbeat, 0.85f, 0.16f);
        float beat = Pulse(beatPhase, 0.02f, 0.07f) + 0.55f * Pulse(beatPhase, 0.22f, 0.07f);

        // The field swells a little on each beat, otherwise breathes slowly.
        float glow = 0.78f + 0.06f * Mathf.Sin(time * 0.9f) + 0.14f * beat + 0.15f * level;
        field.color = new Color(glow, glow, glow, 0.96f);

        // Veins: dim at rest, a wave of blood on each beat, brighter as the virus takes over.
        float pump = 0.3f + 0.35f * beat + 0.35f * level;
        veinsNear.color = WithAlpha(new Color(0.85f, 0.1f, 0.18f), Mathf.Clamp01(pump));
        veinsFar.color = WithAlpha(new Color(1f, 0.15f, 0.25f), Mathf.Clamp01(0.08f + 0.18f * beat + 0.2f * level));
        float swell = 1f + 0.008f * beat;
        veinsNear.rectTransform.localScale = new Vector3(swell, swell, 1f);
        veinsFar.rectTransform.localScale = new Vector3(1.04f * swell, 1.04f * swell, 1f);

        // Embers drift up and sway, brighter on the beat.
        float halfHeight = root.rect.height * 0.5f;
        foreach (Ember e in embers)
        {
            e.life -= dt;
            e.pos.y += e.speed * (1f + 1.5f * level) * dt;
            if (e.life <= 0f || e.pos.y > halfHeight + 20f) RespawnEmber(e, false);
            float k = Mathf.Clamp01(e.life / e.maxLife);
            float fade = Mathf.Sin(Mathf.PI * k); // fades in and out over its life
            Vector2 p = e.pos + new Vector2(Mathf.Sin(time * 0.8f + e.phase) * e.sway, 0f);
            e.rect.anchoredPosition = new Vector2(Mathf.Round(p.x / 4f) * 4f, Mathf.Round(p.y / 4f) * 4f);
            e.rect.sizeDelta = Vector2.one * e.size;
            e.image.color = WithAlpha(e.color, fade * (0.35f + 0.25f * beat + 0.3f * level));
        }
    }

    private void RespawnEmber(Ember e, bool anywhere)
    {
        float halfHeight = root != null && root.rect.height > 0f ? root.rect.height * 0.5f : 960f;
        float x = Random.Range(-540f, 540f);
        // Mostly along the sides, where the veins are, so the middle stays clean for the cards.
        if (Random.value < 0.7f) x = Mathf.Sign(x) * Random.Range(330f, 540f);
        e.pos = new Vector2(x, anywhere ? Random.Range(-halfHeight, halfHeight) : -halfHeight - Random.Range(0f, 200f));
        e.speed = Random.Range(18f, 55f);
        e.sway = Random.Range(6f, 22f);
        e.phase = Random.Range(0f, 6.3f);
        e.maxLife = e.life = Random.Range(5f, 11f);
        e.size = Random.value < 0.75f ? 6f : 10f;
        e.color = Color.Lerp(new Color(0.75f, 0.06f, 0.12f), new Color(1f, 0.45f, 0.2f), Random.value * 0.6f);
    }

    private static float Pulse(float x, float center, float width)
    {
        float d = (x - center) / width;
        return Mathf.Exp(-d * d);
    }

    // Veins that start at the screen edges and branch inward, thinner and fainter as they go, leaving the middle clear.
    // White with alpha = strength; tinted by the RawImage. Bilinear so they read smooth, not pixelated.
    private static Texture2D BuildVeinTexture(int seed)
    {
        const int w = 108, h = 192;
        var strength = new float[w * h];
        var rng = new System.Random(seed);
        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        var stack = new Stack<(Vector2 pos, Vector2 dir, float width, float power, int steps)>();
        const int roots = 18;
        for (int i = 0; i < roots; i++)
        {
            // Spread the roots around the border, more on the long sides.
            float t = (i + Rand(0.1f, 0.9f)) / roots;
            Vector2 pos;
            if (t < 0.38f) pos = new Vector2(0f, t / 0.38f * h);
            else if (t < 0.76f) pos = new Vector2(w - 1f, (t - 0.38f) / 0.38f * h);
            else if (t < 0.88f) pos = new Vector2((t - 0.76f) / 0.12f * w, 0f);
            else pos = new Vector2((t - 0.88f) / 0.12f * w, h - 1f);
            Vector2 toCenter = (new Vector2(w * 0.5f, h * 0.5f) - pos).normalized;
            stack.Push((pos, toCenter, Rand(1.6f, 2.4f), Rand(0.75f, 1f), (int)Rand(26f, 48f)));
        }

        while (stack.Count > 0)
        {
            var (pos, dir, width, power, steps) = stack.Pop();
            for (int s = 0; s < steps; s++)
            {
                float k = 1f - s / (float)steps;
                Stamp(strength, w, h, pos, Mathf.Max(0.6f, width * (0.4f + 0.6f * k)), power * (0.35f + 0.65f * k));
                float angle = Rand(-0.45f, 0.45f);
                dir = (Vector2)(Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg) * dir);
                pos += dir * 1.1f;
                if (pos.x < 0f || pos.y < 0f || pos.x >= w || pos.y >= h) break;
                if (width > 0.9f && rng.NextDouble() < 0.06)
                {
                    Vector2 branch = (Vector2)(Quaternion.Euler(0f, 0f, Rand(25f, 55f) * (rng.NextDouble() < 0.5 ? -1f : 1f)) * dir);
                    stack.Push((pos, branch, width * 0.6f, power * 0.75f, (int)((steps - s) * Rand(0.4f, 0.7f))));
                }
            }
        }

        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(strength[i]) * 255f));
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }

    private static void Stamp(float[] strength, int w, int h, Vector2 center, float radius, float power)
    {
        int r = Mathf.CeilToInt(radius);
        int cx = Mathf.RoundToInt(center.x), cy = Mathf.RoundToInt(center.y);
        for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) continue;
            float d = Vector2.Distance(new Vector2(x, y), center) / radius;
            if (d > 1f) continue;
            float v = power * (1f - d * d);
            int i = y * w + x;
            if (v > strength[i]) strength[i] = v;
        }
    }
}
