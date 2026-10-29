using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Retro Game Over screen: the game fades to a dark red tint, "GAME OVER" drops in letter by letter with a bounce,
/// a stats panel slides up and counts up the results, then a pulsing Restart button pops in.
/// Pixel embers rise in the background. Built in code, animated with unscaled time (the game is paused).
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color dimColor = new(0.06f, 0.01f, 0.05f, 0.88f);
    [SerializeField] private Color titleColor = new(1f, 0.3f, 0.4f, 1f);
    [SerializeField] private Color titleOutlineColor = new(0.16f, 0.02f, 0.08f, 1f);
    [SerializeField] private Color panelColor = new(0.07f, 0.09f, 0.2f, 0.95f);
    [SerializeField] private Color labelColor = new(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color bestColor = new(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color buttonColor = new(1f, 0.82f, 0.2f, 1f);
    [SerializeField] private Color dataKeptColor = new(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color dataDiscardedColor = new(0.45f, 0.48f, 0.6f, 1f);

    [Header("Particles")]
    [Tooltip("Scales the number of pixel embers and bursts. 0 = none.")]
    [SerializeField, Range(0f, 2f)] private float particleAmount = 1f;

    // Timeline, in seconds after Show().
    private const float DimTime = 0.4f;
    private const float LetterStart = 0.25f;
    private const float LetterStagger = 0.07f;
    private const float LetterDrop = 0.55f;
    private const float ReasonAt = 0.95f;
    private const float PanelAt = 1.05f;
    private const float PanelTime = 0.45f;
    private const float CountAt = 1.35f;
    private const float CountTime = 0.9f;
    private const float BestAt = 2.3f;
    private const float ButtonAt = 2.4f;
    private const float ButtonTime = 0.4f;
    private const float WipeTime = 0.4f;

    private const float TitleY = 470f;
    private const float LetterPixel = 16f;

    private sealed class Letter
    {
        public RectTransform rect;
        public Vector2 home;
        public bool landed;
    }

    private sealed class Stat
    {
        public PixelText value;
        public float target;
        public bool isTime;
        public int shown = -1;
        public float pop = 1f;
    }

    private RectTransform root;
    private Image dim;
    private RectTransform titleRoot;
    private readonly List<Letter> letters = new();
    private PixelText reasonText;
    private CanvasGroup reasonGroup;
    private RectTransform panel;
    private CanvasGroup panelGroup;
    private readonly List<Stat> stats = new();
    private PixelText bestText;
    private CanvasGroup bestGroup;
    private RectTransform buttonRect;
    private CanvasGroup buttonGroup;
    private UiButton button;
    private Image wipe;
    private PixelParticleSystem particles;
    private CanvasGroup dataGroup;
    private UiButton keepButton;
    private UiButton shareButton;
    private Image keepImage;
    private Image shareImage;
    private PixelText keepLabel;
    private PixelText shareLabel;
    private float shareLabelTime;

    private bool open;
    private bool newBest;
    private bool bestBurstDone;
    private bool restarting;
    private bool restartCalled;
    private float time;
    private float restartTime;
    private float shake;
    private float emberAccumulator;
    private Action onRestart;

    private void Awake()
    {
        EnsureEventSystem();
        BuildUi();
    }

    /// <summary>Opens the screen with the results of the run. onRestart is called after the closing wipe.</summary>
    public void Show(GameManager.LoseReason reason, int level, int bricks, float runTime, int bestLevel, bool isNewBest, Action restart)
    {
        onRestart = restart;
        newBest = isNewBest;
        bestBurstDone = false;
        restarting = false;
        restartCalled = false;
        time = 0f;
        restartTime = 0f;
        shake = 0f;

        reasonText.Text = reason == GameManager.LoseReason.WallReached ? "THE WALL BROKE THROUGH" : "THE BALL WAS LOST";
        stats[0].target = level;
        stats[1].target = bricks;
        stats[2].target = runTime;
        foreach (Stat stat in stats)
        {
            stat.shown = -1;
            stat.value.Text = stat.isTime ? "0:00" : "0";
        }
        bestText.Text = isNewBest ? "NEW BEST!" : $"BEST LV {bestLevel}";

        foreach (Letter letter in letters)
        {
            letter.landed = false;
            letter.rect.localScale = Vector3.zero;
        }

        shareLabelTime = 0f;
        shareLabel.Text = "SHARE";
        RefreshDataRow();

        root.gameObject.SetActive(true);
        open = true;
        Animate(0f); // apply the starting state now, so the first rendered frame does not flash the final layout
    }

    private void HandleRestartClicked()
    {
        if (restarting) return;
        restarting = true;
        wipe.raycastTarget = true;
        SoundManager.Play(Sfx.Restart);
        particles.Burst(buttonRect.anchoredPosition, Mathf.RoundToInt(40 * particleAmount),
            new[] { buttonColor, Color.white, titleColor }, 250f, 700f, 10f, 22f, 0.4f, 0.9f, 3f, 300f);
    }

    // ---------------------------------------------------------------- animation

    private void Update()
    {
        if (open) Animate(Time.unscaledDeltaTime);
    }

    // Applies the whole timeline at the current time. Also called from Show() with dt = 0.
    private void Animate(float dt)
    {
        time += dt;

        // Background tint.
        Color tint = dimColor;
        tint.a *= Mathf.Clamp01(time / DimTime);
        dim.color = tint;

        AnimateTitle(dt);

        reasonGroup.alpha = Mathf.Clamp01((time - ReasonAt) / 0.3f);

        // Stats panel slides up.
        float panelK = Mathf.Clamp01((time - PanelAt) / PanelTime);
        panelGroup.alpha = panelK;
        panel.anchoredPosition = new Vector2(0f, 20f - 140f * (1f - EaseOutBack(panelK)));
        panel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, panelK);

        AnimateStats(dt);
        AnimateBest();
        AnimateButton();
        AnimateDataRow(dt);
        EmitEmbers(dt);

        if (restarting)
        {
            restartTime += dt;
            wipe.color = new Color(0f, 0f, 0f, Mathf.Clamp01(restartTime / WipeTime));
            if (restartTime >= WipeTime && !restartCalled)
            {
                restartCalled = true;
                onRestart?.Invoke();
            }
        }

        particles.Tick(dt);
    }

    private void AnimateTitle(float dt)
    {
        Vector2 titleBase = new(0f, TitleY);
        for (int i = 0; i < letters.Count; i++)
        {
            Letter letter = letters[i];
            float k = (time - LetterStart - i * LetterStagger) / LetterDrop;
            if (k < 0f)
            {
                letter.rect.localScale = Vector3.zero;
                continue;
            }

            k = Mathf.Clamp01(k);
            letter.rect.localScale = Vector3.one;
            letter.rect.anchoredPosition = letter.home + new Vector2(0f, Mathf.Lerp(560f, 0f, EaseOutBounce(k)));

            // First impact with the "ground": dust and a little screen shake.
            if (!letter.landed && k >= 1f / 2.75f)
            {
                letter.landed = true;
                shake += 12f;
                SoundManager.Play(Sfx.LetterThud, 0.9f + i * 0.03f);
                Vector2 feet = titleBase + letter.home - new Vector2(0f, PixelFont.GlyphHeight * LetterPixel * 0.5f);
                particles.Burst(feet, Mathf.RoundToInt(8 * particleAmount), new[] { titleColor, Color.white },
                    120f, 360f, 6f, 12f, 0.25f, 0.5f, 4f, 500f);
            }
        }

        shake = Mathf.MoveTowards(shake, 0f, dt * 70f);
        Vector2 offset = UnityEngine.Random.insideUnitCircle * shake;
        titleRoot.anchoredPosition = titleBase + new Vector2(Mathf.Round(offset.x / 4f) * 4f, Mathf.Round(offset.y / 4f) * 4f);
    }

    private void AnimateStats(float dt)
    {
        float c = Mathf.Clamp01((time - CountAt) / CountTime);
        float eased = 1f - (1f - c) * (1f - c) * (1f - c);

        foreach (Stat stat in stats)
        {
            int value = Mathf.RoundToInt(stat.target * eased);
            if (value != stat.shown)
            {
                stat.shown = value;
                stat.value.Text = stat.isTime ? $"{value / 60}:{value % 60:00}" : value.ToString();
                stat.pop = 0f; // a small tick on every change
                if (value > 0) SoundManager.Play(Sfx.CountTick, 1f + eased * 0.5f);
            }

            stat.pop = Mathf.Min(1f, stat.pop + dt / 0.12f);
            stat.value.rectTransform.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(stat.pop * Mathf.PI));
        }
    }

    private void AnimateBest()
    {
        float k = Mathf.Clamp01((time - BestAt) / 0.25f);
        bestGroup.alpha = k;
        if (k <= 0f) return;

        if (newBest)
        {
            bestText.color = Color.Lerp(bestColor, Color.white, 0.5f + 0.5f * Mathf.Sin(time * 10f));
            if (!bestBurstDone)
            {
                bestBurstDone = true;
                SoundManager.Play(Sfx.NewBest);
                Vector2 at = panel.anchoredPosition + bestText.rectTransform.anchoredPosition;
                particles.Burst(at, Mathf.RoundToInt(30 * particleAmount), new[] { bestColor, Color.white },
                    200f, 560f, 8f, 16f, 0.4f, 0.9f, 3f, 300f);
            }
        }
        else
        {
            bestText.color = new Color(0.75f, 0.78f, 0.9f, 1f);
        }
    }

    private void AnimateButton()
    {
        float k = Mathf.Clamp01((time - ButtonAt) / ButtonTime);
        buttonGroup.alpha = Mathf.Clamp01(k * 2f);
        button.Interactable = k >= 1f && !restarting;

        float pulse = k >= 1f && !restarting ? 1f + 0.04f * Mathf.Sin(time * 5f) : 1f;
        float press = button.IsPressed ? 0.93f : 1f;
        buttonRect.localScale = Vector3.one * (EaseOutBack(k) * pulse * press);
    }

    private void EmitEmbers(float dt)
    {
        emberAccumulator += dt * 22f * particleAmount * Mathf.Clamp01(time / DimTime);
        while (emberAccumulator >= 1f)
        {
            emberAccumulator -= 1f;
            Vector2 start = new(UnityEngine.Random.Range(-540f, 540f), -980f);
            Vector2 velocity = new(UnityEngine.Random.Range(-25f, 25f), UnityEngine.Random.Range(80f, 220f));
            Color color = UnityEngine.Random.value switch
            {
                < 0.5f => titleColor,
                < 0.8f => new Color(1f, 0.55f, 0.3f),
                _ => bestColor
            };
            particles.Emit(start, velocity, color, UnityEngine.Random.Range(6f, 12f), UnityEngine.Random.Range(2.5f, 4.5f), 0.2f, -10f);
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private static float EaseOutBounce(float x)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (x < 1f / d1) return n1 * x * x;
        if (x < 2f / d1) { x -= 1.5f / d1; return n1 * x * x + 0.75f; }
        if (x < 2.5f / d1) { x -= 2.25f / d1; return n1 * x * x + 0.9375f; }
        x -= 2.625f / d1;
        return n1 * x * x + 0.984375f;
    }

    // ---------------------------------------------------------------- construction

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("GameOverCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;

        ScreenColumn.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>()); // centered layout fits the column
        canvasObject.AddComponent<GraphicRaycaster>();

        root = (RectTransform)canvasObject.transform;

        // Dark tint that also swallows taps on the game behind.
        dim = NewImage("Dim", root, Color.clear, null);
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);

        RectTransform particleLayer = NewRect("Particles", root);
        Stretch(particleLayer);
        particleLayer.gameObject.AddComponent<Canvas>(); // moving squares do not rebuild the rest of the screen
        particles = new PixelParticleSystem(particleLayer, 260);

        // "GAME" / "OVER", one object per letter so each can drop on its own.
        titleRoot = NewRect("Title", root);
        Place(titleRoot, new Vector2(0f, TitleY), new Vector2(10f, 10f));
        AddWord("GAME", 70f);
        AddWord("OVER", -70f);

        reasonText = NewText("Reason", root, "", 4f, new Color(0.85f, 0.8f, 0.9f, 1f));
        Place(reasonText.rectTransform, new Vector2(0f, 290f), new Vector2(1000f, 40f));
        reasonGroup = reasonText.gameObject.AddComponent<CanvasGroup>();

        // Stats panel.
        Image panelImage = NewImage("Stats", root, panelColor, UiSprites.RoundedRect);
        panel = panelImage.rectTransform;
        Place(panel, new Vector2(0f, 20f), new Vector2(760f, 420f));
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

        AddStat("LEVEL", 130f, false);
        AddStat("BRICKS", 40f, false);
        AddStat("TIME", -50f, true);

        bestText = NewText("Best", panel, "", 5f, bestColor);
        Place(bestText.rectTransform, new Vector2(0f, -150f), new Vector2(700f, 50f));
        bestGroup = bestText.gameObject.AddComponent<CanvasGroup>();

        // Restart button.
        Image buttonImage = NewImage("RestartButton", root, buttonColor, UiSprites.RoundedRect);
        buttonImage.raycastTarget = true;
        buttonRect = buttonImage.rectTransform;
        Place(buttonRect, new Vector2(0f, -330f), new Vector2(480f, 150f));
        buttonGroup = buttonRect.gameObject.AddComponent<CanvasGroup>();
        button = buttonRect.gameObject.AddComponent<UiButton>();
        button.Clicked += HandleRestartClicked;
        PixelText label = NewText("Label", buttonRect, "RESTART", 7f, new Color(0.2f, 0.08f, 0.02f, 1f));
        Stretch(label.rectTransform);

        // Run data row under Restart: keep / discard the recorded data of this run, and send it off the device.
        RectTransform dataRoot = NewRect("RunData", root);
        Place(dataRoot, new Vector2(0f, -520f), new Vector2(900f, 110f));
        dataGroup = dataRoot.gameObject.AddComponent<CanvasGroup>();
        dataRoot.gameObject.SetActive(RunProfiler.Available); // developer tool: hidden in release builds
        keepButton = AddSmallButton(dataRoot, "Keep", new Vector2(-170f, 0f), new Vector2(520f, 100f), out keepImage, out keepLabel);
        keepButton.Clicked += HandleKeepClicked;
        shareButton = AddSmallButton(dataRoot, "Share", new Vector2(270f, 0f), new Vector2(260f, 100f), out shareImage, out shareLabel);
        shareButton.Clicked += HandleShareClicked;
        shareLabel.Text = "SHARE";

        // Black wipe on top, used when restarting.
        wipe = NewImage("Wipe", root, Color.clear, null);
        wipe.raycastTarget = false;
        Stretch(wipe.rectTransform);

        root.gameObject.SetActive(false);
    }

    private UiButton AddSmallButton(Transform parent, string name, Vector2 position, Vector2 size, out Image image, out PixelText label)
    {
        image = NewImage(name + "Button", parent, dataKeptColor, UiSprites.RoundedRect);
        image.raycastTarget = true;
        Place(image.rectTransform, position, size);
        UiButton uiButton = image.gameObject.AddComponent<UiButton>();
        label = NewText("Label", image.rectTransform, "", 4f, new Color(0.02f, 0.06f, 0.12f, 1f));
        Stretch(label.rectTransform);
        return uiButton;
    }

    private void HandleKeepClicked()
    {
        RunProfiler profiler = RunProfiler.Instance;
        if (profiler == null) return;
        profiler.SetKept(!profiler.IsKept);
        SoundManager.Play(Sfx.CardSelect);
        RefreshDataRow();
    }

    private void HandleShareClicked()
    {
        RunProfiler profiler = RunProfiler.Instance;
        if (profiler == null) return;

        // Sharing a run that was discarded would be a surprise: keep it again first.
        if (!profiler.IsKept) profiler.SetKept(true);
        shareLabel.Text = profiler.Share();
        shareLabelTime = 1.6f;
        SoundManager.Play(Sfx.Confirm);
        RefreshDataRow();
    }

    private void RefreshDataRow()
    {
        RunProfiler profiler = RunProfiler.Instance;
        bool kept = profiler != null && profiler.IsKept;
        keepLabel.Text = kept ? "RUN DATA: KEPT" : "RUN DATA: DISCARDED";
        keepImage.color = kept ? dataKeptColor : dataDiscardedColor;
        shareImage.color = dataKeptColor;
    }

    private void AnimateDataRow(float dt)
    {
        RunProfiler profiler = RunProfiler.Instance;
        bool available = profiler != null && profiler.HasSavedRun;
        float k = Mathf.Clamp01((time - ButtonAt - 0.15f) / ButtonTime);
        dataGroup.alpha = available ? k : 0f;

        bool ready = available && k >= 1f && !restarting;
        keepButton.Interactable = ready;
        shareButton.Interactable = ready;
        keepButton.transform.localScale = Vector3.one * (keepButton.IsPressed ? 0.95f : 1f);
        shareButton.transform.localScale = Vector3.one * (shareButton.IsPressed ? 0.95f : 1f);

        if (shareLabelTime > 0f)
        {
            shareLabelTime -= dt;
            if (shareLabelTime <= 0f) shareLabel.Text = "SHARE";
        }
    }

    private void AddWord(string word, float y)
    {
        float advance = (PixelFont.GlyphWidth + 1f) * LetterPixel;
        for (int i = 0; i < word.Length; i++)
        {
            PixelText text = NewText($"Letter_{word[i]}", titleRoot, word[i].ToString(), LetterPixel, titleColor);
            Vector2 home = new((i - (word.Length - 1) * 0.5f) * advance, y);
            Place(text.rectTransform, home, new Vector2(advance, 140f));

            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = titleOutlineColor;
            outline.effectDistance = new Vector2(7f, 7f);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = titleOutlineColor;
            shadow.effectDistance = new Vector2(0f, -14f);

            letters.Add(new Letter { rect = text.rectTransform, home = home });
        }
    }

    private void AddStat(string label, float y, bool isTime)
    {
        PixelText labelText = NewText(label + "Label", panel, label, 5f, labelColor);
        labelText.Alignment = PixelText.Align.Left;
        Place(labelText.rectTransform, new Vector2(-160f, y), new Vector2(400f, 50f));

        PixelText valueText = NewText(label + "Value", panel, "0", 6f, Color.white);
        valueText.Alignment = PixelText.Align.Right;
        Place(valueText.rectTransform, new Vector2(160f, y), new Vector2(400f, 60f));

        stats.Add(new Stat { value = valueText, isTime = isTime });
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image NewImage(string name, Transform parent, Color color, Sprite sprite)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static PixelText NewText(string name, Transform parent, string text, float pixelSize, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        PixelText pixelText = rect.gameObject.AddComponent<PixelText>();
        pixelText.Text = text;
        pixelText.PixelSize = pixelSize;
        pixelText.color = color;
        pixelText.Alignment = PixelText.Align.Center;
        return pixelText;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // Centered anchors: positions are offsets from the middle of the parent.
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
