using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// On-screen experience display: a level badge, a flat rounded progress bar and the XP numbers,
/// plus big retro "LEVEL UP!" and "CLEAR!" banners near the middle of the screen. Built in code, not interactive.
/// The bar fills smoothly, wraps around when a level is gained, and pulses when XP comes in.
/// </summary>
[DisallowMultipleComponent]
public class ExperienceHud : MonoBehaviour, IXpVisuals
{
    [SerializeField] private ExperienceSystem experience;

    [Header("Colors")]
    [SerializeField] private Color panelColor = new(0.07f, 0.09f, 0.2f, 0.85f);
    [SerializeField] private Color trackColor = new(0.04f, 0.05f, 0.12f, 0.8f);
    [SerializeField] private Color fillColor = new(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color levelLabelColor = new(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color levelUpColor = new(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color levelUpOutlineColor = new(0.16f, 0.06f, 0.3f, 1f);

    [Header("Animation")]
    [Tooltip("Seconds the bar takes to catch up with new XP. Lower = snappier.")]
    [SerializeField, Min(0.01f)] private float fillSmoothTime = 0.2f;
    [Tooltip("How much the bar and level badge pop. 0 = no pulse.")]
    [SerializeField, Range(0f, 0.5f)] private float punchStrength = 0.18f;

    [Header("Level Up Banner")]
    [Tooltip("Vertical position of the banner: 0 = bottom of the screen, 1 = top.")]
    [SerializeField, Range(0.3f, 0.9f)] private float bannerHeight = 0.625f;
    [Tooltip("Seconds the banner stays fully visible.")]
    [SerializeField, Min(0.1f)] private float bannerHoldTime = 0.9f;

    [Header("Clear Banner")]
    [Tooltip("Vertical position of the CLEAR! banner (below LEVEL UP!, since a clear often causes a level-up).")]
    [SerializeField, Range(0.3f, 0.9f)] private float clearBannerHeight = 0.53f;
    [SerializeField] private Color clearColor = new(0.35f, 0.95f, 1f, 1f);
    [SerializeField] private Color clearOutlineColor = new(0.03f, 0.08f, 0.25f, 1f);

    [Header("XP Orbs")]
    [Tooltip("Seconds an XP orb takes to fly from the brick to the bar.")]
    [SerializeField, Min(0.1f)] private float orbTravelTime = 0.7f;
    [Tooltip("Seconds between the orbs of one hit, so they pop out as a small burst.")]
    [SerializeField, Min(0f)] private float orbStagger = 0.05f;
    [Tooltip("Scales the number of pixel sparkles. 0 = none.")]
    [SerializeField, Range(0f, 2f)] private float particleAmount = 1f;

    // XP orb denominations: the XP of a hit is merged into the fewest orbs. Index = tier.
    private static readonly int[] OrbValues = { 1, 5, 20, 50, 100 };
    private static readonly Color[] OrbColors =
    {
        new(0.82f, 0.85f, 0.92f), // 1   white / grey
        new(0.3f, 1f, 0.4f),      // 5   green
        new(0.25f, 0.55f, 1f),    // 20  blue
        new(0.72f, 0.3f, 1f),     // 50  purple
        new(1f, 0.82f, 0.2f),     // 100 gold
    };
    private static readonly float[] OrbScales = { 1f, 1.15f, 1.3f, 1.45f, 1.65f };
    private Color[][] tierBurstColors;

    private sealed class Orb
    {
        public GameObject root;
        public RectTransform rect;
        public Image glow;
        public Image core;
        public int tier;
        public Vector2 start;
        public Vector2 control;
        public float time;
        public float duration;
        public float trailTimer;
        public int amount;
        public System.Action<int> onArrive;
        public bool active;
        public bool shown;
    }

    private readonly System.Collections.Generic.List<Orb> orbs = new();
    private RectTransform canvasRect;
    private PixelParticleSystem particles;
    private Color[] burstColors;
    private int orbChain;          // orbs landing in quick succession climb in pitch
    private float lastOrbArrival;

    private const float BannerPopTime = 0.22f;
    private const float BannerExitTime = 0.35f;

    private RectTransform safeArea;
    private RectTransform badge;
    private RectTransform track;
    private RectTransform fillRect;
    private Image fillImage;
    private PixelText levelNumber;
    private PixelText xpText;
    private sealed class Banner
    {
        public RectTransform rect;
        public CanvasGroup group;
        public PixelText text;
        public PixelText subtitle; // optional second line
        public Color color;
        public float time = -1f;   // < 0 = hidden
    }

    private Banner levelUpBanner;
    private Banner clearBanner;

    private float shown;       // animated value:  (level - 1) + fraction of the current level
    private float target;
    private float velocity;
    private int displayedLevel = 1;
    private float barPunch = 1f;
    private float badgePunch = 1f;

    private void Awake()
    {
        BuildUi();
    }

    private void OnEnable()
    {
        if (experience == null) return;
        experience.XpChanged += HandleXpChanged;
        experience.ClearBonusGranted += HandleClearBonus;
        experience.XpVisuals = this;
        SnapTo(experience.Level, experience.CurrentXp, experience.XpToNextLevel);
    }

    private void OnDisable()
    {
        if (experience != null)
        {
            experience.XpChanged -= HandleXpChanged;
            experience.ClearBonusGranted -= HandleClearBonus;
            if (experience.XpVisuals == (IXpVisuals)this) experience.XpVisuals = null;
        }
    }

    private void SnapTo(int level, int current, int required)
    {
        target = shown = (level - 1) + current / (float)Mathf.Max(1, required);
        velocity = 0f;
        displayedLevel = level;
        levelNumber.Text = level.ToString();
        xpText.Text = $"{current}/{required}";
    }

    private void HandleXpChanged(int current, int required, int level)
    {
        float newTarget = (level - 1) + current / (float)Mathf.Max(1, required);
        xpText.Text = $"{current}/{required}";

        if (newTarget < target - 0.0001f) SnapTo(level, current, required); // run was reset
        else if (newTarget > target + 0.0001f) barPunch = 0f;

        target = newTarget;
    }

    private void HandleClearBonus(int amount)
    {
        clearBanner.subtitle.Text = $"+{amount} XP";
        SoundManager.Play(Sfx.ClearFanfare);
        clearBanner.time = 0f;

        // A big pixel blast behind the banner.
        Vector2 center = canvasRect.InverseTransformPoint(clearBanner.rect.position);
        int count = Mathf.RoundToInt(45 * particleAmount);
        particles.Burst(center, count, new[] { clearColor, Color.white, fillColor }, 260f, 820f, 8f, 20f, 0.4f, 0.9f, 3f, 200f);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Bar: glide toward the target; crossing a whole number means a level was gained.
        shown = Mathf.SmoothDamp(shown, target, ref velocity, fillSmoothTime, Mathf.Infinity, dt);
        if (Mathf.Abs(target - shown) < 0.0005f)
        {
            shown = target;
            velocity = 0f;
        }

        int shownLevel = Mathf.FloorToInt(shown + 0.0001f) + 1;
        float fraction = Mathf.Clamp01(shown - (shownLevel - 1));
        if (shownLevel != displayedLevel)
        {
            bool gained = shownLevel > displayedLevel;
            displayedLevel = shownLevel;
            levelNumber.Text = displayedLevel.ToString();
            if (gained)
            {
                badgePunch = 0f;
                levelUpBanner.time = 0f;
                SoundManager.Play(Sfx.LevelUp);
            }
        }

        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(fraction, 1f);
        fillImage.enabled = fraction > 0.003f;

        // Pulses: a quick swell that fades out, with a white flash on the bar.
        barPunch = Mathf.Min(1f, barPunch + dt / 0.22f);
        float barSwell = Mathf.Sin(barPunch * Mathf.PI) * punchStrength;
        track.localScale = new Vector3(1f, 1f + barSwell, 1f);
        fillImage.color = Color.Lerp(Color.white, fillColor, Mathf.SmoothStep(0f, 1f, barPunch));

        badgePunch = Mathf.Min(1f, badgePunch + dt / 0.4f);
        float badgeSwell = Mathf.Sin(badgePunch * Mathf.PI) * (1f - badgePunch) * punchStrength * 2.5f;
        badge.localScale = Vector3.one * (1f + badgeSwell);

        UpdateOrbs(Time.deltaTime); // orbs freeze with the game (level-up pause)
        particles?.Tick(dt);
        UpdateBanner(levelUpBanner, dt);
        UpdateBanner(clearBanner, dt);
    }

    // Pop in with an overshoot and a white flash, breathe, then rise and fade out.
    private void UpdateBanner(Banner banner, float dt)
    {
        if (banner.time < 0f)
        {
            banner.group.alpha = 0f;
            return;
        }

        banner.time += dt;
        float t = banner.time;
        float scale = 1f;
        float alpha = 1f;
        float lift = 0f;

        if (t < BannerPopTime)
        {
            float k = t / BannerPopTime;
            scale = EaseOutBack(k);
            alpha = Mathf.Min(1f, k * 4f);
        }
        else if (t < BannerPopTime + bannerHoldTime)
        {
            scale = 1f + 0.02f * Mathf.Sin((t - BannerPopTime) * 9f); // gentle breathing
        }
        else if (t < BannerPopTime + bannerHoldTime + BannerExitTime)
        {
            float k = (t - BannerPopTime - bannerHoldTime) / BannerExitTime;
            scale = 1f + 0.12f * k;
            alpha = 1f - k;
            lift = 50f * k;
        }
        else
        {
            banner.time = -1f;
            banner.group.alpha = 0f;
            return;
        }

        banner.rect.localScale = Vector3.one * scale;
        banner.rect.anchoredPosition = new Vector2(0f, lift);
        banner.group.alpha = alpha;
        banner.text.color = Color.Lerp(Color.white, banner.color, Mathf.Clamp01(t / 0.15f)); // white flash on arrival
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ---------------------------------------------------------------- UI construction

    // ---------------------------------------------------------------- XP orbs and pixel bursts

    /// <summary>
    /// Merges the XP into the fewest orbs (denominations 1, 5, 20, 50, 100, each with its own color and size)
    /// and launches them from a world position to the XP bar, as a small burst.
    /// Each orb calls onArrive with its value when it lands.
    /// </summary>
    public void Deliver(Vector3 worldPosition, int amount, System.Action<int> onArrive)
    {
        Camera cam = Camera.main;
        if (cam == null || canvasRect == null)
        {
            onArrive?.Invoke(amount); // no visuals available: just add the XP
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f)
        {
            onArrive?.Invoke(amount);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 start);
        Vector2 target = GetBarTarget();

        // Merge the XP into the fewest orbs: biggest denominations first, remainder in smaller ones.
        int orbIndex = 0;
        for (int tier = OrbValues.Length - 1; tier >= 0; tier--)
        {
            int count = amount / OrbValues[tier];
            amount -= count * OrbValues[tier];
            for (int n = 0; n < count; n++)
                LaunchOrb(tier, orbIndex++, start, target, onArrive);
        }
    }

    private void LaunchOrb(int tier, int i, Vector2 start, Vector2 target, System.Action<int> onArrive)
    {
        int portion = OrbValues[tier];
        Orb orb = FindFreeOrb();
        if (orb == null)
        {
            onArrive?.Invoke(portion); // all orbs busy: add this share right away
            return;
        }

        orb.tier = tier;
        Color tint = OrbColors[tier];
        orb.glow.color = new Color(tint.r, tint.g, tint.b, 0.3f);
        orb.core.color = tint;

        orb.start = start + UnityEngine.Random.insideUnitCircle * 18f;
        orb.control = (orb.start + target) * 0.5f + new Vector2(UnityEngine.Random.Range(-220f, 220f), UnityEngine.Random.Range(60f, 260f));
        orb.duration = orbTravelTime * UnityEngine.Random.Range(0.9f, 1.15f);
        orb.time = -i * orbStagger; // negative = waiting for its turn in the burst
        orb.trailTimer = 0f;
        orb.amount = portion;
        orb.onArrive = onArrive;
        orb.active = true;
        orb.shown = false;
    }

    private Orb FindFreeOrb()
    {
        foreach (Orb orb in orbs)
            if (!orb.active) return orb;
        return null;
    }

    private void UpdateOrbs(float dt)
    {
        if (orbs.Count == 0) return;

        Vector2 target = GetBarTarget();
        foreach (Orb orb in orbs)
        {
            if (!orb.active) continue;

            orb.time += dt;
            if (orb.time < 0f) continue; // still waiting for its turn in the burst

            if (!orb.shown)
            {
                orb.shown = true;
                orb.rect.anchoredPosition = orb.start;
                orb.rect.localScale = Vector3.zero;
                orb.root.SetActive(true);
                SoundManager.Play(Sfx.OrbLaunch);

                // Small pop each time an orb comes out of the brick.
                int pop = Mathf.RoundToInt(UnityEngine.Random.Range(3, 6) * (1 + orb.tier * 0.5f) * particleAmount);
                particles.Burst(orb.start, pop, TierBurstColors(orb.tier), 120f, 340f, 6f, 12f, 0.25f, 0.5f, 4f, 0f);
            }

            float u = Mathf.Clamp01(orb.time / orb.duration);
            float e = Mathf.Lerp(u * u, u * u * (3f - 2f * u), 0.5f); // gentle start, accelerates toward the bar
            float inv = 1f - e;
            Vector2 p = inv * inv * orb.start + 2f * inv * e * orb.control + e * e * target;

            orb.rect.anchoredPosition = p;
            orb.rect.localScale = Vector3.one * (Mathf.Min(1f, u / 0.12f) * Mathf.Lerp(1f, 0.75f, u) * OrbScales[orb.tier]);

            // Pixel trail.
            orb.trailTimer -= dt;
            if (orb.trailTimer <= 0f && dt > 0f)
            {
                orb.trailTimer = 0.03f;
                Vector2 jitter = new(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, 5f));
                Vector2 drift = new(UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(-20f, 20f));
                Color color = UnityEngine.Random.value < 0.35f ? Color.white : OrbColors[orb.tier];
                particles.Emit(p + jitter, drift, color, UnityEngine.Random.Range(8f, 13f), UnityEngine.Random.Range(0.25f, 0.45f), 3f, 0f);
            }

            if (u >= 1f) Arrive(orb, target);
        }
    }

    private void Arrive(Orb orb, Vector2 target)
    {
        orb.active = false;
        orb.shown = false;
        orb.root.SetActive(false);

        // Explosion on the bar: scattered squares plus a few big white flashes. Several orbs stack into a bigger blast.
        int count = Mathf.RoundToInt((10 + orb.tier * 8) * particleAmount);
        particles.Burst(target, count, TierBurstColors(orb.tier), 200f, 640f, 8f, 18f, 0.35f, 0.8f, 3.5f, 260f);
        particles.Burst(target, Mathf.RoundToInt((3 + orb.tier) * particleAmount), new[] { Color.white }, 40f, 160f, 18f, 26f, 0.12f, 0.25f, 6f, 0f);

        System.Action<int> callback = orb.onArrive;
        int amount = orb.amount;
        orb.onArrive = null;
        callback?.Invoke(amount); // the XP is added now, which also pulses the bar

        // Quick successions climb in pitch, like a combo.
        orbChain = Time.unscaledTime - lastOrbArrival < 0.35f ? orbChain + 1 : 0;
        lastOrbArrival = Time.unscaledTime;
        SoundManager.Play(Sfx.OrbCollect, 1f + Mathf.Min(orbChain, 12) * 0.05f + orb.tier * 0.1f); // richer orbs ring higher
    }

    private Color[] TierBurstColors(int tier) => tierBurstColors[tier];

    // The tip of the filled part of the bar, in canvas coordinates.
    private Vector2 GetBarTarget()
    {
        Vector3 center = canvasRect.InverseTransformPoint(track.position);
        float width = track.rect.width - 12f;
        float fraction = Mathf.Clamp(shown - Mathf.Floor(shown + 0.0001f), 0.03f, 1f);
        return new Vector2(center.x - width * 0.5f + width * fraction, center.y);
    }

    private void BuildXpEffects(Transform canvasParent)
    {
        canvasRect = (RectTransform)canvasParent;
        burstColors = new[] { fillColor, Color.white, new Color(0.7f, 0.95f, 1f) };
        tierBurstColors = new Color[OrbColors.Length][];
        for (int t = 0; t < OrbColors.Length; t++)
            tierBurstColors[t] = new[] { OrbColors[t], Color.white, Color.Lerp(OrbColors[t], Color.white, 0.5f) };

        RectTransform particleLayer = NewRect("XpParticles", canvasParent);
        Stretch(particleLayer);
        particleLayer.gameObject.AddComponent<Canvas>(); // own canvas: moving sparkles do not rebuild the rest of the HUD
        particles = new PixelParticleSystem(particleLayer, 240);

        RectTransform orbLayer = NewRect("XpOrbs", canvasParent);
        Stretch(orbLayer);
        orbLayer.gameObject.AddComponent<Canvas>();
        Vector2 middle = new(0.5f, 0.5f);
        for (int i = 0; i < 120; i++)
        {
            RectTransform root = NewRect("Orb", orbLayer);
            Place(root, middle, middle, Vector2.zero, new Vector2(26f, 26f));

            Image glow = NewImage("Glow", root, new Color(fillColor.r, fillColor.g, fillColor.b, 0.3f));
            Place(glow.rectTransform, middle, middle, Vector2.zero, new Vector2(46f, 46f));
            Image core = NewImage("Core", root, fillColor);
            Place(core.rectTransform, middle, middle, Vector2.zero, new Vector2(26f, 26f));
            Image shine = NewImage("Shine", root, new Color(1f, 1f, 1f, 0.95f));
            Place(shine.rectTransform, middle, middle, new Vector2(-3f, 3f), new Vector2(10f, 10f));

            root.gameObject.SetActive(false);
            orbs.Add(new Orb { root = root.gameObject, rect = root, glow = glow, core = core });
        }
    }

    /// <summary>Creates the whole HUD. Called automatically on Awake.</summary>
    public Canvas BuildUi()
    {
        var canvasObject = new GameObject("ExperienceCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        // Reference layout is 1080x1920 portrait. No GraphicRaycaster: nothing here is interactive.
        ScreenColumn.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());

        // Corner HUD lives in the playfield column (inside the safe area), so it stays above the game on wide screens.
        safeArea = ScreenColumn.Create(canvasObject.transform, "SafeArea");

        // Level badge (top left): small "LV" label above a big number.
        badge = NewRect("LevelBadge", safeArea);
        Place(badge, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(135f, -115f), new Vector2(190f, 150f));
        NewImage("Panel", badge, panelColor).rectTransform.SetAsFirstSibling();
        Stretch((RectTransform)badge.Find("Panel"));

        PixelText label = NewText("LvLabel", badge, "LV", 5f, levelLabelColor, PixelText.Align.Center);
        Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(190f, 40f));

        levelNumber = NewText("LvNumber", badge, "1", 9f, Color.white, PixelText.Align.Center);
        Place(levelNumber.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 48f), new Vector2(190f, 70f));

        // Progress bar (rounded track + rounded fill).
        track = NewRect("XpTrack", safeArea);
        Place(track, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(540f, -100f), new Vector2(560f, 48f));
        Image trackImage = track.gameObject.AddComponent<Image>();
        ConfigureImage(trackImage, trackColor);

        RectTransform fillArea = NewRect("FillArea", track);
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(6f, 6f);
        fillArea.offsetMax = new Vector2(-6f, -6f);

        fillImage = NewImage("Fill", fillArea, fillColor);
        fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        // XP numbers centered inside the bar, drawn over the fill. A dark outline keeps them readable on the bright fill.
        xpText = NewText("XpNumbers", track, "0/10", 4f, Color.white, PixelText.Align.Center);
        Stretch(xpText.rectTransform);
        AddRetroShadow(xpText, new Color(0.02f, 0.03f, 0.1f, 0.9f), 3f, 3f);

        // Banners, centered horizontally at their heights (not limited to the safe area).
        levelUpBanner = BuildBanner("LevelUpBanner", canvasObject.transform, "LEVEL UP!", bannerHeight, levelUpColor, levelUpOutlineColor, false);
        clearBanner = BuildBanner("ClearBanner", canvasObject.transform, "CLEAR!", clearBannerHeight, clearColor, clearOutlineColor, true);

        BuildXpEffects(canvasObject.transform);
        return canvas;
    }

    // Big pixel word with a dark outline and a hard drop shadow, plus an optional small line under it.
    private static Banner BuildBanner(string name, Transform parent, string word, float height, Color color, Color outlineColor, bool withSubtitle)
    {
        var banner = new Banner { color = color };
        banner.rect = NewRect(name, parent);
        Place(banner.rect, new Vector2(0.5f, height), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 140f));
        banner.group = banner.rect.gameObject.AddComponent<CanvasGroup>();
        banner.group.interactable = false;
        banner.group.blocksRaycasts = false;
        banner.group.alpha = 0f;

        banner.text = NewText(name + "Text", banner.rect, word, 14f, color, PixelText.Align.Center);
        Stretch(banner.text.rectTransform);
        AddRetroShadow(banner.text, outlineColor, 6f, 12f);

        if (withSubtitle)
        {
            banner.subtitle = NewText(name + "Subtitle", banner.rect, "", 6f, Color.white, PixelText.Align.Center);
            Place(banner.subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(900f, 50f));
            AddRetroShadow(banner.subtitle, outlineColor, 3f, 6f);
        }
        return banner;
    }

    private static void AddRetroShadow(PixelText text, Color color, float outline, float drop)
    {
        var outlineEffect = text.gameObject.AddComponent<Outline>();
        outlineEffect.effectColor = color;
        outlineEffect.effectDistance = new Vector2(outline, outline);
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = new Vector2(0f, -drop);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void ConfigureImage(Image image, Color color)
    {
        image.sprite = UiSprites.RoundedRect;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        ConfigureImage(image, color);
        return image;
    }

    private static PixelText NewText(string name, Transform parent, string text, float pixelSize, Color color, PixelText.Align align)
    {
        RectTransform rect = NewRect(name, parent);
        PixelText pixelText = rect.gameObject.AddComponent<PixelText>();
        pixelText.Text = text;
        pixelText.PixelSize = pixelSize;
        pixelText.color = color;
        pixelText.Alignment = align;
        return pixelText;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
