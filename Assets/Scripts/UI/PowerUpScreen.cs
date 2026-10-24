using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// The level-up choice screen: the game fades to black, a row of power-up cards slides in, the player taps one
/// (it gets a pulsing halo and pixel sparkles) and presses Confirm. A Reroll button is shown but disabled for now.
/// Built in code, animated with unscaled time so it works while the game is paused.
/// </summary>
public class PowerUpScreen : MonoBehaviour
{
    private enum Phase { Hidden, Showing, Visible, Hiding }

    [Header("Look")]
    [Tooltip("How dark the game gets behind the cards (1 = black).")]
    [SerializeField, Range(0f, 1f)] private float dimAmount = 0.8f;
    [SerializeField] private Color cardColor = new(0.08f, 0.1f, 0.22f, 1f);
    [SerializeField] private Color confirmColor = new(0.25f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color disabledColor = new(0.17f, 0.2f, 0.29f, 1f);

    [Header("Layout (reference resolution 1080x1920)")]
    [SerializeField, Min(100f)] private float cardWidth = 300f;
    [SerializeField, Min(100f)] private float cardHeight = 560f;
    [SerializeField, Min(0f)] private float cardGap = 28f;
    [Tooltip("Vertical position of the cards, relative to the screen center.")]
    [SerializeField] private float cardCenterY = 60f;

    [Header("Particles")]
    [Tooltip("Scales how many pixel sparkles appear. 0 = none.")]
    [SerializeField, Range(0f, 2f)] private float particleAmount = 1f;

    private const int ParticleCapacity = 180;
    private const float ReferenceWidth = 1080f;
    // Halo (38) plus the selected card's 6% zoom and breathing, so the side cards' glow is never cut by the screen edge.
    private const float SideMargin = 60f;
    private const float DimFadeTime = 0.35f;
    private const float CardStagger = 0.09f;
    private const float CardAppearTime = 0.4f;
    private const float HideTime = 0.3f;

    private sealed class Particle
    {
        public RectTransform rect;
        public Image image;
        public Vector2 pos;
        public Vector2 vel;
        public float life;
        public float maxLife;
        public float size;
        public float drag;
        public Color color;
    }

    private RectTransform root;
    private Image dim;
    private PixelText title;
    private RectTransform cardsRoot;
    private RectTransform particlesRoot;
    private UiButton confirmButton;
    private Image confirmImage;
    private PixelText confirmLabel;
    private UiButton rerollButton;
    private Image rerollImage;
    private PixelText rerollLabel;

    private readonly List<CardView> cards = new();
    private readonly List<Particle> particles = new();
    private List<PowerUpDefinition> choices = new();
    private Action<PowerUpDefinition> onConfirm;
    private Action onClosed;
    private Phase phase = Phase.Hidden;
    private float phaseTime;
    private int selected = -1;
    private int particleCursor;

    public bool IsOpen => phase != Phase.Hidden;

    private void Awake()
    {
        EnsureEventSystem();
        BuildUi();
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Opens the screen with these choices. onConfirm runs when the player confirms; onClosed after the fade-out.</summary>
    public void Show(IReadOnlyList<PowerUpDefinition> options, Func<PowerUpDefinition, int> stackCount,
        Action<PowerUpDefinition> confirmed, Action closed)
    {
        ClearCards();
        choices = new List<PowerUpDefinition>(options);
        onConfirm = confirmed;
        onClosed = closed;
        selected = -1;

        // The canvas is always ReferenceWidth wide (it scales with the screen width), so the row of cards is sized to
        // fit inside it, leaving room on both sides for the halo and the selected card's zoom.
        int count = choices.Count;
        float available = ReferenceWidth - 2f * SideMargin - cardGap * (count - 1);
        float width = Mathf.Min(cardWidth, available / count);
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) * 0.5f) * (width + cardGap);
            cards.Add(BuildCard(i, choices[i], stackCount != null ? stackCount(choices[i]) : 0, width, x));
        }

        root.gameObject.SetActive(true);
        phase = Phase.Showing;
        phaseTime = 0f;
        SetDim(0f);
        title.color = new Color(1f, 1f, 1f, 0f);
        SoundManager.Play(Sfx.CardsAppear);
        Animate(0f); // apply the starting state now, so the first rendered frame does not flash the cards
    }

    // ---------------------------------------------------------------- interaction

    private void SelectCard(int index)
    {
        if (phase != Phase.Visible || index == selected) return;
        selected = index;
        SoundManager.Play(Sfx.CardSelect);
        // Draw the chosen card last so its halo is not covered by the neighbouring cards.
        cards[index].transform.SetAsLastSibling();
        Burst(cards[index], 18, 1f);
    }

    private void ConfirmSelection()
    {
        if (phase != Phase.Visible || selected < 0) return;

        CardView card = cards[selected];
        Burst(card, 45, 1.6f);
        PowerUpDefinition chosen = choices[selected];
        SoundManager.Play(Sfx.Confirm);
        phase = Phase.Hiding;
        phaseTime = 0f;
        onConfirm?.Invoke(chosen);
    }

    // ---------------------------------------------------------------- animation

    private void Update()
    {
        if (phase != Phase.Hidden) Animate(Time.unscaledDeltaTime);
    }

    // Applies the whole animation at the current time. Also called from Show() with dt = 0.
    private void Animate(float dt)
    {
        float time = Time.unscaledTime;
        phaseTime += dt;

        float dimAlpha;
        float titleAlpha;
        switch (phase)
        {
            case Phase.Showing:
            {
                float k = Mathf.Clamp01(phaseTime / DimFadeTime);
                dimAlpha = k * dimAmount;
                titleAlpha = k;
                float lastCardDone = 0.2f + CardStagger * (cards.Count - 1) + CardAppearTime;
                for (int i = 0; i < cards.Count; i++)
                {
                    float t = Mathf.Clamp01((phaseTime - 0.2f - i * CardStagger) / CardAppearTime);
                    cards[i].Appear = EaseOutBack(t);
                }
                if (phaseTime >= lastCardDone) phase = Phase.Visible;
                break;
            }
            case Phase.Hiding:
            {
                float k = Mathf.Clamp01(phaseTime / HideTime);
                dimAlpha = (1f - k) * dimAmount;
                titleAlpha = 1f - k;
                foreach (CardView card in cards) card.Appear = 1f - k;
                if (k >= 1f)
                {
                    FinishHiding();
                    return;
                }
                break;
            }
            default:
                dimAlpha = dimAmount;
                titleAlpha = 1f;
                foreach (CardView card in cards) card.Appear = 1f;
                break;
        }

        SetDim(dimAlpha);
        title.color = new Color(1f, 1f, 1f, titleAlpha);

        for (int i = 0; i < cards.Count; i++) AnimateCard(cards[i], i, dt, time);
        AnimateButtons(time);
        UpdateParticles(dt);
    }

    private void AnimateCard(CardView card, int index, float dt, float time)
    {
        bool isSelected = index == selected;
        card.SelectT = Mathf.MoveTowards(card.SelectT, isSelected ? 1f : 0f, dt * 6f);

        float appear = Mathf.Clamp01(card.Appear);
        float selectScale = selected < 0 ? 1f : Mathf.Lerp(0.95f, 1.06f, card.SelectT);
        card.Rect.localScale = Vector3.one * (selectScale * Mathf.Lerp(0.6f, 1f, card.Appear));
        card.Rect.anchoredPosition = new Vector2(card.BaseX, cardCenterY + card.SelectT * 24f - (1f - appear) * 160f);
        card.Group.alpha = appear * (selected >= 0 && !isSelected ? 0.8f : 1f);

        // Halo: a soft colored glow that breathes around the chosen card.
        Color halo = card.RarityColor;
        halo.a = card.SelectT * (0.3f + 0.15f * Mathf.Sin(time * 5f));
        card.Halo.color = halo;
        card.Halo.rectTransform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(time * 5f) * card.SelectT);

        // Border brightens when chosen.
        Color dark = new(card.RarityColor.r * 0.5f, card.RarityColor.g * 0.5f, card.RarityColor.b * 0.5f, 1f);
        Color bright = Color.Lerp(card.RarityColor, Color.white, 0.25f * card.SelectT);
        card.Border.color = Color.Lerp(dark, bright, card.SelectT);

        // Sparkles: lots around the chosen card, a few on rare finds.
        float rate = isSelected ? 40f : 0f;
        if (!isSelected && card.Rarity >= PowerUpRarity.Epic) rate = card.Rarity == PowerUpRarity.Legendary ? 14f : 6f;
        if (phase != Phase.Hiding && rate > 0f && card.Appear > 0.8f)
        {
            card.EmitAccumulator += rate * particleAmount * dt;
            while (card.EmitAccumulator >= 1f)
            {
                card.EmitAccumulator -= 1f;
                EmitAround(card);
            }
        }
    }

    private void AnimateButtons(float time)
    {
        bool canConfirm = phase == Phase.Visible && selected >= 0;
        confirmButton.Interactable = canConfirm;

        float pulse = canConfirm ? 1f + 0.03f * Mathf.Sin(time * 6f) : 1f;
        float press = confirmButton.IsPressed ? 0.94f : 1f;
        confirmButton.transform.localScale = Vector3.one * (pulse * press);
        confirmImage.color = canConfirm ? confirmColor : disabledColor;
        confirmLabel.color = canConfirm ? new Color(0.03f, 0.13f, 0.07f) : new Color(0.45f, 0.5f, 0.62f);

        // Reroll is intentionally dead for now.
        rerollButton.Interactable = false;
        rerollImage.color = disabledColor;
        rerollLabel.color = new Color(0.45f, 0.5f, 0.62f);
    }

    private void FinishHiding()
    {
        ClearCards();
        foreach (Particle p in particles)
        {
            p.life = 0f;
            p.image.enabled = false;
        }
        root.gameObject.SetActive(false);
        phase = Phase.Hidden;

        Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    private void SetDim(float alpha)
    {
        dim.color = new Color(0f, 0f, 0f, alpha);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ---------------------------------------------------------------- particles (retro square sparkles)

    private void EmitAround(CardView card)
    {
        Vector2 half = new Vector2(card.Width, cardHeight) * 0.5f * card.Rect.localScale.x;
        Vector2 center = card.Rect.anchoredPosition;
        Vector2 offset;
        switch (UnityEngine.Random.Range(0, 4))
        {
            case 0: offset = new Vector2(UnityEngine.Random.Range(-half.x, half.x), half.y); break;
            case 1: offset = new Vector2(UnityEngine.Random.Range(-half.x, half.x), -half.y); break;
            case 2: offset = new Vector2(-half.x, UnityEngine.Random.Range(-half.y, half.y)); break;
            default: offset = new Vector2(half.x, UnityEngine.Random.Range(-half.y, half.y)); break;
        }

        Color color = UnityEngine.Random.value < 0.3f ? Color.white : card.RarityColor;
        Vector2 velocity = new(UnityEngine.Random.Range(-25f, 25f), UnityEngine.Random.Range(60f, 190f));
        Spawn(center + offset, velocity, color, UnityEngine.Random.Range(8f, 16f), UnityEngine.Random.Range(0.7f, 1.4f), 0.3f);
    }

    private void Burst(CardView card, int count, float power)
    {
        if (particleAmount <= 0f) return;
        Vector2 center = card.Rect.anchoredPosition;
        Vector2 half = new Vector2(card.Width, cardHeight) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            Vector2 start = center + new Vector2(UnityEngine.Random.Range(-half.x, half.x), UnityEngine.Random.Range(-half.y, half.y)) * 0.8f;
            Vector2 direction = (start - center).normalized;
            if (direction == Vector2.zero) direction = Vector2.up;
            Vector2 velocity = direction * UnityEngine.Random.Range(180f, 420f) * power;
            Color color = UnityEngine.Random.value < 0.4f ? Color.white : card.RarityColor;
            Spawn(start, velocity, color, UnityEngine.Random.Range(8f, 18f), UnityEngine.Random.Range(0.5f, 1.0f), 2.5f);
        }
    }

    private void Spawn(Vector2 position, Vector2 velocity, Color color, float size, float life, float drag)
    {
        // Reuse the next free slot, scanning from a rolling cursor.
        for (int n = 0; n < particles.Count; n++)
        {
            particleCursor = (particleCursor + 1) % particles.Count;
            Particle p = particles[particleCursor];
            if (p.life > 0f) continue;

            p.pos = position;
            p.vel = velocity;
            p.color = color;
            p.size = size;
            p.life = p.maxLife = life;
            p.drag = drag;
            p.image.enabled = true;
            return;
        }
    }

    private void UpdateParticles(float dt)
    {
        foreach (Particle p in particles)
        {
            if (p.life <= 0f) continue;

            p.life -= dt;
            if (p.life <= 0f)
            {
                p.image.enabled = false;
                continue;
            }

            p.vel *= 1f - Mathf.Min(1f, p.drag * dt);
            p.pos += p.vel * dt;

            float k = p.life / p.maxLife;
            // Snap to a 4px grid so the motion steps like old pixel art.
            p.rect.anchoredPosition = new Vector2(Mathf.Round(p.pos.x / 4f) * 4f, Mathf.Round(p.pos.y / 4f) * 4f);
            float size = Mathf.Max(4f, Mathf.Round(Mathf.Lerp(0.4f, 1f, k) * p.size / 2f) * 2f);
            p.rect.sizeDelta = new Vector2(size, size);
            Color c = p.color;
            c.a = Mathf.Clamp01(k * 1.6f);
            p.image.color = c;
        }
    }

    // ---------------------------------------------------------------- construction

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    private void ClearCards()
    {
        foreach (CardView card in cards)
            if (card != null) Destroy(card.gameObject);
        cards.Clear();
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("PowerUpCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f; // portrait: width is the limiting side, taller phones just get more height
        canvasObject.AddComponent<GraphicRaycaster>();

        root = (RectTransform)canvasObject.transform;

        // Dark overlay: also swallows taps so nothing behind the screen is touched.
        dim = NewImage("Dim", root, Color.clear, false);
        Stretch(dim.rectTransform);
        dim.sprite = null;
        dim.type = Image.Type.Simple;
        dim.raycastTarget = true;

        title = NewText("Title", root, "CHOOSE AN UPGRADE", 6f, Color.white, PixelText.Align.Center);
        Place(title.rectTransform, Vector2.zero, new Vector2(0f, 520f), new Vector2(1000f, 60f));
        var titleShadow = title.gameObject.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0.1f, 0.05f, 0.25f, 1f);
        titleShadow.effectDistance = new Vector2(0f, -6f);

        cardsRoot = NewRect("Cards", root);
        Stretch(cardsRoot);

        particlesRoot = NewRect("Particles", root);
        Stretch(particlesRoot);
        for (int i = 0; i < ParticleCapacity; i++)
        {
            Image image = NewImage("P", particlesRoot, Color.white, false);
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.enabled = false;
            particles.Add(new Particle { rect = image.rectTransform, image = image });
        }

        // Sparkles go behind the cards so they frame them without covering the text.
        particlesRoot.SetSiblingIndex(cardsRoot.GetSiblingIndex());

        float buttonY = cardCenterY - cardHeight * 0.5f - 110f;
        BuildButton("RerollButton", "REROLL", new Vector2(-190f, buttonY), out rerollButton, out rerollImage, out rerollLabel);
        BuildButton("ConfirmButton", "CONFIRM", new Vector2(190f, buttonY), out confirmButton, out confirmImage, out confirmLabel);
        confirmButton.Clicked += ConfirmSelection;
        confirmButton.DeniedClick += () => SoundManager.Play(Sfx.Denied); // no card chosen yet
        rerollButton.DeniedClick += () => SoundManager.Play(Sfx.Denied);  // reroll not available yet

        root.gameObject.SetActive(false);
    }

    private void BuildButton(string name, string label, Vector2 position, out UiButton button, out Image image, out PixelText text)
    {
        image = NewImage(name, root, disabledColor, true);
        Place(image.rectTransform, Vector2.zero, position, new Vector2(320f, 120f));
        button = image.gameObject.AddComponent<UiButton>();
        text = NewText("Label", image.rectTransform, label, 6f, Color.white, PixelText.Align.Center);
        Stretch(text.rectTransform);
    }

    private CardView BuildCard(int index, PowerUpDefinition def, int stacks, float width, float x)
    {
        Color rarity = RarityStyle.GetColor(def.Rarity);
        Color rarityDark = new(rarity.r * 0.5f, rarity.g * 0.5f, rarity.b * 0.5f, 1f);

        RectTransform rect = NewRect($"Card{index}", cardsRoot);
        Place(rect, Vector2.zero, new Vector2(x, cardCenterY), new Vector2(width, cardHeight));
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        var view = rect.gameObject.AddComponent<CardView>();

        Image halo = NewImage("Halo", rect, Color.clear, false);
        Stretch(halo.rectTransform);
        halo.rectTransform.offsetMin = new Vector2(-38f, -38f);
        halo.rectTransform.offsetMax = new Vector2(38f, 38f);

        Image border = NewImage("Border", rect, rarityDark, false);
        Stretch(border.rectTransform);
        border.rectTransform.offsetMin = new Vector2(-8f, -8f);
        border.rectTransform.offsetMax = new Vector2(8f, 8f);

        Image body = NewImage("Body", rect, cardColor, true); // the tappable area
        Stretch(body.rectTransform);

        // Header diamond with the power-up icon, half outside the top edge.
        float topY = cardHeight * 0.5f;
        Image diamondBorder = NewImage("DiamondBorder", rect, rarity, false);
        Place(diamondBorder.rectTransform, Vector2.zero, new Vector2(0f, topY), new Vector2(124f, 124f));
        diamondBorder.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image diamond = NewImage("Diamond", rect, new Color(0.04f, 0.05f, 0.12f, 1f), false);
        Place(diamond.rectTransform, Vector2.zero, new Vector2(0f, topY), new Vector2(104f, 104f));
        diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Image icon = NewImage("Icon", rect, rarity, false);
        icon.type = Image.Type.Simple;
        icon.sprite = UiSprites.PixelArt(def.Icon);
        icon.preserveAspect = true;
        Place(icon.rectTransform, Vector2.zero, new Vector2(0f, topY), new Vector2(70f, 70f));

        PixelText nameText = NewText("Name", rect, def.DisplayName, 4f, rarity, PixelText.Align.Center);
        nameText.Wrap = true;
        Place(nameText.rectTransform, Vector2.zero, new Vector2(0f, 60f), new Vector2(width - 36f, 130f));

        PixelText description = NewText("Description", rect, def.Description, 3f, new Color(0.88f, 0.9f, 1f), PixelText.Align.Center);
        description.Wrap = true;
        Place(description.rectTransform, Vector2.zero, new Vector2(0f, -60f), new Vector2(width - 44f, 190f));

        PixelText rarityText = NewText("Rarity", rect, RarityStyle.GetLabel(def.Rarity), 3f, rarity, PixelText.Align.Center);
        Place(rarityText.rectTransform, Vector2.zero, new Vector2(0f, -196f), new Vector2(width - 20f, 30f));

        string levelLabel = stacks <= 0 ? "NEW!" : $"LV {stacks + 1}";
        PixelText level = NewText("Level", rect, levelLabel, 4f, new Color(0.4f, 1f, 0.5f), PixelText.Align.Center);
        Place(level.rectTransform, Vector2.zero, new Vector2(0f, -238f), new Vector2(width - 20f, 34f));

        view.Index = index;
        view.Clicked = SelectCard;
        view.Rect = rect;
        view.Group = group;
        view.Halo = halo;
        view.Border = border;
        view.Rarity = def.Rarity;
        view.RarityColor = rarity;
        view.Width = width;
        view.BaseX = x;
        view.Appear = 0f;
        return view;
    }

    // ---------------------------------------------------------------- small helpers

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image NewImage(string name, Transform parent, Color color, bool raycast)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = UiSprites.RoundedRect;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = raycast;
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

    // Anchored to the screen center, so positions are offsets from the middle.
    private static void Place(RectTransform rect, Vector2 anchorOffsetUnused, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
