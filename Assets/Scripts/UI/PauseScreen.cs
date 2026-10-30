using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Pause button (top right) and pause screen: freezes the run and shows the run stats (level, XP, bricks, time,
/// ball damage, wall HP window) and every power-up picked so far, with its level, pips and what it does in total.
/// Resume counts down 3-2-1 before the game starts again; Restart asks for a second tap. Also opens with Escape / P
/// and when the app goes to the background. Builds itself at startup (no scene setup) and runs on unscaled time.
/// </summary>
public class PauseScreen : MonoBehaviour
{
    // Layout, in the 1080x1920 reference column (centered origin).
    private const float TitleY = 735f;
    private const float TitlePixel = 14f;
    private const float StatsY = 515f;
    private const float ListTop = 365f;
    private const float ListBottom = -540f;
    private const float HpDisplayY = -608f;
    private const float HpDisplayHeight = 96f;
    private const float ListWidth = 960f;
    private const float RowHeight = 124f;
    private const float RowGap = 14f;
    private const float ButtonsY = -755f;
    private const float EndRunY = -890f; // dev-only "END RUN" button, under the Restart / Resume row

    // Timeline, in seconds after opening.
    private const float DimTime = 0.18f;
    private const float LetterStagger = 0.04f;
    private const float LetterPop = 0.3f;
    private const float StatsAt = 0.08f;
    private const float ListAt = 0.16f;
    private const float RowStagger = 0.04f;
    private const float RowSlide = 0.3f;
    private const float ButtonsAt = 0.24f;
    private const float FadeOutTime = 0.15f;
    private const float CountStep = 0.4f;
    private const float RestartConfirmTime = 2.5f;
    private const float WipeTime = 0.3f;

    [Header("Colors")]
    [SerializeField] private Color dimColor = new(0.02f, 0.02f, 0.07f, 0.9f);
    [SerializeField] private Color panelColor = new(0.07f, 0.09f, 0.2f, 0.95f);
    [SerializeField] private Color rowColor = new(0.09f, 0.11f, 0.24f, 0.95f);
    [SerializeField] private Color accentColor = new(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color titleOutlineColor = new(0.02f, 0.1f, 0.2f, 1f);
    [SerializeField] private Color detailColor = new(0.72f, 0.77f, 0.92f, 1f);
    [SerializeField] private Color levelColor = new(0.4f, 1f, 0.5f, 1f);
    [SerializeField] private Color maxLevelColor = new(1f, 0.8f, 0.25f, 1f);
    [SerializeField] private Color emptyPipColor = new(0.17f, 0.2f, 0.29f, 1f);
    [SerializeField] private Color resumeColor = new(0.25f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color restartColor = new(1f, 0.3f, 0.42f, 1f);

    [Header("Particles")]
    [Tooltip("Scales the drifting pixel motes behind the panels. 0 = none.")]
    [SerializeField, Range(0f, 2f)] private float particleAmount = 1f;

    private enum State { Hidden, Open, Closing, Restarting }

    private sealed class Letter
    {
        public RectTransform rect;
        public Vector2 home;
    }

    private sealed class Row
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float y;
    }

    private GameManager game;
    private PowerUpManager powerUps;
    private PixelText damageLabel;
    private const int DamageStat = 3; // index in the stats grid
    private ExperienceSystem experience;
    private BrickGrid grid;

    // Pause button (HUD canvas).
    private CanvasGroup buttonGroup;
    private RectTransform pauseButtonRect;
    private UiButton pauseButton;

    // Pause screen (overlay canvas).
    private RectTransform root;
    private Image dim;
    private CanvasGroup content;
    private PixelParticleSystem particles;
    private readonly List<Letter> letters = new();
    private RectTransform statsPanel;
    private CanvasGroup statsGroup;
    private readonly List<PixelText> statValues = new();
    private PixelText pickCount;
    private CanvasGroup listGroup;
    private ScrollRect scroll;
    private RectTransform listContent;
    private PixelText emptyText;
    private readonly List<Row> rows = new();
    private RectTransform resumeRect;
    private UiButton resumeButton;
    private RectTransform restartRect;
    private Image restartImage;
    private PixelText restartLabel;
    private UiButton restartButton;
    private UiButton endRunButton;
    private RectTransform endRunRect;
    private CanvasGroup buttonsGroup;
    private readonly List<(BrickHpDisplay.Mode mode, Image image, PixelText label, UiButton button)> hpDisplayOptions = new();
    private PixelText countText;
    private Image wipe;

    private State state;
    private float time;
    private float closeTime;
    private float restartArmedLeft;
    private float savedTimeScale = 1f;
    private float moteAccumulator;
    private int lastCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Spawn();
        // The scene is reloaded by the Restart buttons: bring the pause screen back with it.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => Spawn();
    }

    private static void Spawn()
    {
        if (FindAnyObjectByType<PauseScreen>() == null && FindAnyObjectByType<GameManager>() != null)
            new GameObject("PauseScreen").AddComponent<PauseScreen>();
    }

    private void Awake()
    {
        game = FindAnyObjectByType<GameManager>();
        powerUps = FindAnyObjectByType<PowerUpManager>();
        experience = FindAnyObjectByType<ExperienceSystem>();
        grid = FindAnyObjectByType<BrickGrid>();

        EnsureEventSystem();
        BuildButton();
        BuildScreen();
    }

    private void OnDestroy()
    {
        // Never leave the game frozen behind a destroyed screen.
        if (state != State.Hidden && state != State.Restarting) Time.timeScale = savedTimeScale;
    }

    // A phone call or the home button pauses the run, so the player does not come back to a lost ball.
    private void OnApplicationPause(bool paused)
    {
        if (paused) Open();
    }

    /// <summary>The run can be paused: in play, no menu open, no slow-down running.</summary>
    private bool CanPause =>
        (game == null || !game.IsGameOver) && (powerUps == null || !powerUps.IsChoosing) && Time.timeScale > 0f;

    // ---------------------------------------------------------------- open / close

    private void Open()
    {
        if (state == State.Closing)
        {
            // Paused again during the countdown: the game is still frozen, just bring the screen back.
            state = State.Open;
            SoundManager.Play(Sfx.CardSelect, 0.9f);
            countText.gameObject.SetActive(false);
            dim.raycastTarget = true;
            Fill();
            time = 0f;
            Animate(0f);
            return;
        }

        if (state != State.Hidden || !CanPause) return;

        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        state = State.Open;
        time = 0f;
        restartArmedLeft = 0f;
        SetRestartArmed(false);
        SoundManager.Play(Sfx.CardSelect, 0.9f);

        Fill();
        root.gameObject.SetActive(true);
        dim.raycastTarget = true;
        Animate(0f); // starting state now, so the first frame does not flash the final layout
    }

    private void Resume()
    {
        if (state != State.Open) return;
        state = State.Closing;
        closeTime = 0f;
        lastCount = 0;
        dim.raycastTarget = false;
        SoundManager.Play(Sfx.Confirm);
    }

    private void HandleRestartClicked()
    {
        if (state != State.Open) return;
        if (restartArmedLeft <= 0f)
        {
            // First tap only arms the button: a run is too precious to lose to a stray tap.
            restartArmedLeft = RestartConfirmTime;
            SetRestartArmed(true);
            SoundManager.Play(Sfx.Denied, 1.2f);
            return;
        }

        state = State.Restarting;
        closeTime = 0f;
        wipe.raycastTarget = true;
        SoundManager.Play(Sfx.Restart);
    }

    private void SetRestartArmed(bool armed)
    {
        restartLabel.Text = armed ? "SURE?" : "RESTART";
    }

    // Rebuilds the stats and the power-up list from the current run.
    private void Fill()
    {
        RefreshHpDisplaySelector();

        int level = experience != null ? experience.Level : 1;
        float runTime = game != null ? game.RunTime : 0f;
        int seconds = Mathf.FloorToInt(runTime);

        int damage = 1;
        foreach (BallController ball in BallController.Active) damage = Mathf.Max(damage, ball.Damage);

        statValues[0].Text = NumberFormat.Short(level);
        statValues[1].Text = $"{seconds / 60}:{seconds % 60:00}";
        statValues[2].Text = game != null ? NumberFormat.Short(game.BricksDestroyed) : "0";
        statValues[DamageStat].Text = NumberFormat.Short(damage);
        // The label also tells how much base damage each level-up adds, and the card multiplier on top of it.
        if (damageLabel != null)
        {
            float cardFactor = 1f;
            foreach (BallController ball in BallController.Active) cardFactor = Mathf.Max(cardFactor, ball.CardDamageMultiplier);
            string factor = cardFactor > 1.001f ? $" X{cardFactor:0.0#}" : "";
            damageLabel.Text = powerUps != null ? $"DAMAGE +{powerUps.DamagePerLevel}/LV{factor}" : "DAMAGE";
        }
        statValues[4].Text = grid != null ? $"{NumberFormat.Short(grid.CurrentMinHp)}-{NumberFormat.Short(grid.CurrentMaxHp)}" : "-";
        statValues[5].Text = experience != null ? $"{NumberFormat.Short(experience.CurrentXp)}/{NumberFormat.Short(experience.XpToNextLevel)}" : "-";

        foreach (Row row in rows) Destroy(row.rect.gameObject);
        rows.Clear();

        var owned = new List<KeyValuePair<PowerUpDefinition, int>>();
        int picks = 0;
        if (powerUps != null)
            foreach (KeyValuePair<PowerUpDefinition, int> pair in powerUps.Owned)
                if (pair.Key != null && pair.Value > 0)
                {
                    owned.Add(pair);
                    picks += pair.Value;
                }

        // Rarest first, then the most picked.
        owned.Sort((a, b) =>
        {
            int byRarity = RarityStyle.SortRank(b.Key.Rarity).CompareTo(RarityStyle.SortRank(a.Key.Rarity));
            if (byRarity != 0) return byRarity;
            int byStacks = b.Value.CompareTo(a.Value);
            return byStacks != 0 ? byStacks : string.CompareOrdinal(a.Key.DisplayName, b.Key.DisplayName);
        });

        pickCount.Text = picks == 1 ? "1 PICK" : $"{picks} PICKS";
        emptyText.gameObject.SetActive(owned.Count == 0);

        for (int i = 0; i < owned.Count; i++) rows.Add(BuildRow(owned[i].Key, owned[i].Value, i));
        listContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, owned.Count * (RowHeight + RowGap) - RowGap));
        listContent.anchoredPosition = Vector2.zero;
        scroll.velocity = Vector2.zero;
    }

    // ---------------------------------------------------------------- update

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        ReadKeyboard();
        AnimateButton(dt);

        if (state == State.Hidden) return;
        Animate(dt);
    }

    private void ReadKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (!keyboard.escapeKey.wasPressedThisFrame && !keyboard.pKey.wasPressedThisFrame) return;

        if (state == State.Open) Resume();
        else Open();
    }

    private void AnimateButton(float dt)
    {
        bool visible = state == State.Hidden && CanPause;
        buttonGroup.alpha = Mathf.MoveTowards(buttonGroup.alpha, visible ? 1f : 0f, dt * 6f);
        buttonGroup.blocksRaycasts = visible;
        pauseButton.Interactable = visible;
        pauseButtonRect.localScale = Vector3.one * (pauseButton.IsPressed ? 0.9f : 1f);
    }

    // Applies the whole timeline at the current time. Also called on open with dt = 0.
    private void Animate(float dt)
    {
        time += dt;

        if (state == State.Open)
        {
            content.alpha = 1f;
            SetDim(Mathf.Clamp01(time / DimTime));
            restartArmedLeft = Mathf.Max(0f, restartArmedLeft - dt);
            if (restartArmedLeft <= 0f && restartLabel.Text != "RESTART") SetRestartArmed(false);
        }

        AnimateTitle();
        AnimatePanels();
        AnimateButtons();
        EmitMotes(dt);

        if (state == State.Closing) AnimateCountdown(dt);
        if (state == State.Restarting) AnimateRestart(dt);

        particles.Tick(dt);
    }

    private void AnimateTitle()
    {
        // Letters pop in one by one, then ride a slow sine wave.
        for (int i = 0; i < letters.Count; i++)
        {
            Letter letter = letters[i];
            float k = Mathf.Clamp01((time - i * LetterStagger) / LetterPop);
            letter.rect.localScale = Vector3.one * EaseOutBack(k);
            float wave = Mathf.Sin(time * 3f - i * 0.6f) * 8f;
            letter.rect.anchoredPosition = letter.home + new Vector2(0f, Mathf.Round(wave / 2f) * 2f);
        }
    }

    private void AnimatePanels()
    {
        float statsK = Mathf.Clamp01((time - StatsAt) / 0.3f);
        statsGroup.alpha = statsK;
        statsPanel.anchoredPosition = new Vector2(0f, StatsY - 50f * (1f - EaseOutBack(statsK)));

        listGroup.alpha = Mathf.Clamp01((time - ListAt) / 0.2f);
        for (int i = 0; i < rows.Count; i++)
        {
            float k = Mathf.Clamp01((time - ListAt - i * RowStagger) / RowSlide);
            rows[i].group.alpha = k;
            rows[i].rect.anchoredPosition = new Vector2(120f * (1f - EaseOutBack(k)), rows[i].y);
        }
    }

    private void AnimateButtons()
    {
        float k = Mathf.Clamp01((time - ButtonsAt) / 0.3f);
        buttonsGroup.alpha = Mathf.Clamp01(k * 2f);
        bool interactive = state == State.Open && k >= 1f;
        resumeButton.Interactable = interactive;
        restartButton.Interactable = interactive;
        if (endRunButton != null)
        {
            endRunButton.Interactable = interactive;
            endRunRect.localScale = Vector3.one * (EaseOutBack(k) * (endRunButton.IsPressed ? 0.93f : 1f));
        }
        foreach (var option in hpDisplayOptions)
        {
            option.button.Interactable = interactive;
            option.image.rectTransform.localScale = Vector3.one * (option.button.IsPressed ? 0.93f : 1f);
        }

        float scale = EaseOutBack(k);
        float pulse = interactive ? 1f + 0.035f * Mathf.Sin(time * 5f) : 1f;
        resumeRect.localScale = Vector3.one * (scale * pulse * (resumeButton.IsPressed ? 0.93f : 1f));

        bool armed = restartArmedLeft > 0f;
        float shake = armed ? Mathf.Round(Mathf.Sin(time * 40f) * 2f) * 2f : 0f;
        restartRect.localScale = Vector3.one * (scale * (restartButton.IsPressed ? 0.93f : 1f));
        restartRect.anchoredPosition = new Vector2(-300f + shake, ButtonsY);
        restartImage.color = armed ? Color.Lerp(restartColor, Color.white, 0.25f + 0.25f * Mathf.Sin(time * 14f)) : restartColor;
    }

    // Resume: the panels fade out, then 3-2-1 pops in the middle before the game runs again.
    private void AnimateCountdown(float dt)
    {
        closeTime += dt;
        content.alpha = 1f - Mathf.Clamp01(closeTime / FadeOutTime);

        float countTime = closeTime - FadeOutTime;
        float total = CountStep * 3f;
        SetDim(1f - Mathf.Clamp01(countTime / total) * 0.6f);

        int count = countTime < 0f ? 0 : 3 - Mathf.FloorToInt(countTime / CountStep);
        if (count != lastCount && count >= 1)
        {
            lastCount = count;
            countText.Text = count.ToString();
            SoundManager.Play(Sfx.CountTick, 0.8f + (3 - count) * 0.15f);
            particles.Burst(Vector2.zero, Mathf.RoundToInt(14 * particleAmount), new[] { accentColor, Color.white },
                160f, 420f, 6f, 12f, 0.25f, 0.5f, 3f, 0f);
        }

        if (count >= 1)
        {
            float step = Mathf.Repeat(countTime, CountStep) / CountStep;
            countText.gameObject.SetActive(true);
            countText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, EaseOutBack(Mathf.Clamp01(step * 2.5f)));
            Color c = countText.color;
            c.a = 1f - Mathf.Clamp01((step - 0.7f) / 0.3f);
            countText.color = c;
        }
        else
        {
            countText.gameObject.SetActive(false);
        }

        if (countTime >= total)
        {
            countText.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            particles.Clear();
            state = State.Hidden;
            Time.timeScale = savedTimeScale;
        }
    }

    private void AnimateRestart(float dt)
    {
        closeTime += dt;
        wipe.color = new Color(0f, 0f, 0f, Mathf.Clamp01(closeTime / WipeTime));
        if (closeTime < WipeTime) return;

        state = State.Hidden; // the scene reload below destroys this screen
        if (game != null) game.Restart();
    }

    private void SetDim(float k)
    {
        Color tint = dimColor;
        tint.a *= k;
        dim.color = tint;
    }

    // Slow pixel motes drifting up behind the panels, in the UI accent colors.
    private void EmitMotes(float dt)
    {
        if (state != State.Open) return;
        moteAccumulator += dt * 12f * particleAmount;
        while (moteAccumulator >= 1f)
        {
            moteAccumulator -= 1f;
            Vector2 start = new(Random.Range(-540f, 540f), -980f);
            Vector2 velocity = new(Random.Range(-15f, 15f), Random.Range(60f, 160f));
            Color color = Random.value switch
            {
                < 0.5f => accentColor,
                < 0.8f => new Color(0.95f, 0.3f, 0.9f),
                _ => new Color(0.6f, 0.45f, 1f)
            };
            particles.Emit(start, velocity, color, Random.Range(6f, 10f), Random.Range(4f, 7f), 0f, 0f);
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ---------------------------------------------------------------- construction

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static Canvas NewCanvas(string name, Transform parent, int order)
    {
        var canvasObject = new GameObject(name, typeof(RectTransform));
        canvasObject.transform.SetParent(parent, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        ScreenColumn.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // Top-right pause button, level with the XP bar, above the active bonus rows.
    private void BuildButton()
    {
        Canvas canvas = NewCanvas("PauseButtonCanvas", transform, 11); // just above the XP HUD
        RectTransform column = ScreenColumn.Create(canvas.transform, "SafeArea");

        Image frame = NewImage("PauseButton", column, accentColor, UiSprites.RoundedRect);
        frame.raycastTarget = true;
        pauseButtonRect = frame.rectTransform;
        pauseButtonRect.anchorMin = pauseButtonRect.anchorMax = new Vector2(1f, 1f);
        pauseButtonRect.pivot = new Vector2(0.5f, 0.5f);
        pauseButtonRect.anchoredPosition = new Vector2(-85f, -100f);
        pauseButtonRect.sizeDelta = new Vector2(96f, 96f);
        buttonGroup = frame.gameObject.AddComponent<CanvasGroup>();
        buttonGroup.alpha = 0f;

        Image inner = NewImage("Inner", pauseButtonRect, panelColor, UiSprites.RoundedRect);
        Place(inner.rectTransform, Vector2.zero, new Vector2(84f, 84f));
        for (int i = 0; i < 2; i++)
        {
            Image bar = NewImage($"Bar{i}", pauseButtonRect, accentColor, null);
            Place(bar.rectTransform, new Vector2(i == 0 ? -13f : 13f, 0f), new Vector2(14f, 42f));
        }

        pauseButton = frame.gameObject.AddComponent<UiButton>();
        pauseButton.Pressed += Open; // on touch-down, so the same tap cannot launch the ball or drag the paddle
    }

    private void BuildScreen()
    {
        Canvas canvas = NewCanvas("PauseCanvas", transform, 25); // over the HUD and cards, under Game Over
        root = (RectTransform)canvas.transform;

        // Dark tint that also swallows taps on the game behind.
        dim = NewImage("Dim", root, Color.clear, null);
        Stretch(dim.rectTransform);

        RectTransform particleLayer = NewRect("Particles", root);
        Stretch(particleLayer);
        particleLayer.gameObject.AddComponent<Canvas>(); // moving squares do not rebuild the rest of the screen
        particles = new PixelParticleSystem(particleLayer, 120);

        RectTransform contentRect = NewRect("Content", root);
        Stretch(contentRect);
        content = contentRect.gameObject.AddComponent<CanvasGroup>();

        BuildTitle(contentRect);
        BuildStats(contentRect);
        BuildList(contentRect);
        BuildBottomButtons(contentRect);

        countText = NewText("Countdown", root, "3", 26f, Color.white);
        Place(countText.rectTransform, Vector2.zero, new Vector2(300f, 220f));
        var countOutline = countText.gameObject.AddComponent<Outline>();
        countOutline.effectColor = new Color(accentColor.r * 0.4f, accentColor.g * 0.4f, accentColor.b * 0.4f, 1f);
        countOutline.effectDistance = new Vector2(8f, 8f);
        countText.gameObject.SetActive(false);

        wipe = NewImage("Wipe", root, Color.clear, null);
        wipe.raycastTarget = false;
        Stretch(wipe.rectTransform);

        root.gameObject.SetActive(false);
    }

    private void BuildTitle(RectTransform parent)
    {
        const string word = "PAUSED";
        RectTransform title = NewRect("Title", parent);
        Place(title, new Vector2(0f, TitleY), new Vector2(10f, 10f));

        float advance = (PixelFont.GlyphWidth + 1f) * TitlePixel;
        for (int i = 0; i < word.Length; i++)
        {
            PixelText text = NewText($"Letter_{word[i]}", title, word[i].ToString(), TitlePixel, accentColor);
            Vector2 home = new((i - (word.Length - 1) * 0.5f) * advance, 0f);
            Place(text.rectTransform, home, new Vector2(advance, 120f));

            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = titleOutlineColor;
            outline.effectDistance = new Vector2(6f, 6f);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.55f, 0.15f, 0.6f, 1f); // magenta drop, like the neon tube trims
            shadow.effectDistance = new Vector2(0f, -12f);

            letters.Add(new Letter { rect = text.rectTransform, home = home });
        }
    }

    // Six run stats in a 3x2 grid: small cyan label over a white value.
    private void BuildStats(RectTransform parent)
    {
        Image panelImage = NewImage("Stats", parent, panelColor, UiSprites.RoundedRect);
        statsPanel = panelImage.rectTransform;
        Place(statsPanel, new Vector2(0f, StatsY), new Vector2(ListWidth, 250f));
        statsGroup = statsPanel.gameObject.AddComponent<CanvasGroup>();

        string[] labels = { "LEVEL", "TIME", "BRICKS", "DAMAGE", "WALL HP", "XP" };
        for (int i = 0; i < labels.Length; i++)
        {
            float x = (i % 3 - 1) * 310f;
            float y = i < 3 ? 58f : -60f;

            PixelText label = NewText(labels[i] + "Label", statsPanel, labels[i], 3f, accentColor);
            Place(label.rectTransform, new Vector2(x, y + 30f), new Vector2(300f, 24f));
            if (i == DamageStat) damageLabel = label;

            PixelText value = NewText(labels[i] + "Value", statsPanel, "0", 6f, Color.white);
            Place(value.rectTransform, new Vector2(x, y - 14f), new Vector2(300f, 48f));
            statValues.Add(value);
        }

        // Thin dividers between the two rows and the three columns.
        Color line = new(accentColor.r, accentColor.g, accentColor.b, 0.15f);
        Place(NewImage("RowLine", statsPanel, line, null).rectTransform, Vector2.zero, new Vector2(ListWidth - 80f, 4f));
        for (int c = 0; c < 2; c++)
            Place(NewImage($"ColLine{c}", statsPanel, line, null).rectTransform, new Vector2(c == 0 ? -155f : 155f, 0f), new Vector2(4f, 190f));
    }

    private void BuildList(RectTransform parent)
    {
        RectTransform list = NewRect("PowerUps", parent);
        Place(list, new Vector2(0f, (ListTop + ListBottom) * 0.5f), new Vector2(ListWidth, ListTop - ListBottom));
        listGroup = list.gameObject.AddComponent<CanvasGroup>();
        float half = (ListTop - ListBottom) * 0.5f;

        PixelText header = NewText("Header", list, "POWER-UPS", 4f, accentColor);
        header.Alignment = PixelText.Align.Left;
        Place(header.rectTransform, new Vector2(-ListWidth * 0.5f + 210f, half - 20f), new Vector2(420f, 30f));

        pickCount = NewText("Picks", list, "0 PICKS", 3f, detailColor);
        pickCount.Alignment = PixelText.Align.Right;
        Place(pickCount.rectTransform, new Vector2(ListWidth * 0.5f - 210f, half - 20f), new Vector2(420f, 30f));

        Image rule = NewImage("Rule", list, new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f), null);
        Place(rule.rectTransform, new Vector2(0f, half - 52f), new Vector2(ListWidth, 4f));

        // Scrolling viewport: a long run picks more power-ups than fit on screen.
        Image viewportImage = NewImage("Viewport", list, Color.clear, null);
        viewportImage.raycastTarget = true; // catches the drag for the scroll
        RectTransform viewport = viewportImage.rectTransform;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = new Vector2(0f, -70f);
        viewport.gameObject.AddComponent<RectMask2D>();

        listContent = NewRect("Rows", viewport);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.anchoredPosition = Vector2.zero;
        listContent.sizeDelta = Vector2.zero;

        scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;

        emptyText = NewText("Empty", viewport, "NO POWER-UPS YET\nLEVEL UP TO PICK ONE", 4f, detailColor);
        Place(emptyText.rectTransform, new Vector2(0f, 180f), new Vector2(ListWidth, 80f));
    }

    // One power-up: rarity bar, icon on its diamond, name, LV x/y with pips, and what it does in total.
    private Row BuildRow(PowerUpDefinition def, int stacks, int index)
    {
        Color rarity = RarityStyle.GetColor(def.Rarity);
        Color rarityDark = new(rarity.r * 0.35f, rarity.g * 0.35f, rarity.b * 0.35f, 1f);
        int max = powerUps != null ? powerUps.GetMaxStacks(def) : def.MaxStacks; // corruptions lower caps
        bool maxed = max > 0 && stacks >= max;
        Color level = maxed ? maxLevelColor : levelColor;
        const float left = -ListWidth * 0.5f;
        const float right = ListWidth * 0.5f;

        Image background = NewImage($"Row_{def.DisplayName}", listContent, rowColor, UiSprites.RoundedRect);
        RectTransform rect = background.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ListWidth, RowHeight);
        float y = -index * (RowHeight + RowGap) - RowHeight * 0.5f;
        rect.anchoredPosition = new Vector2(0f, y);

        Image bar = NewImage("Rarity", rect, rarity, null);
        Place(bar.rectTransform, new Vector2(left + 14f, 0f), new Vector2(8f, RowHeight - 36f));

        Image diamond = NewImage("Diamond", rect, rarityDark, null);
        Place(diamond.rectTransform, new Vector2(left + 88f, 0f), new Vector2(72f, 72f));
        diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Image icon = NewImage("Icon", rect, rarity, null);
        icon.sprite = UiSprites.PixelArt(def.Icon);
        icon.preserveAspect = true;
        Place(icon.rectTransform, new Vector2(left + 88f, 0f), new Vector2(56f, 56f));

        const float textLeft = left + 160f;
        PixelText nameText = NewText("Name", rect, def.DisplayName.ToUpperInvariant(), 4f, rarity);
        nameText.Alignment = PixelText.Align.Left;
        PlaceLeft(nameText.rectTransform, new Vector2(textLeft, 28f), new Vector2(560f, 30f));

        string detail = def.Effect != null ? def.Effect.Describe(stacks) : null;
        if (string.IsNullOrEmpty(detail)) detail = def.Description;
        PixelText detailText = NewText("Detail", rect, detail.ToUpperInvariant(), 3f, detailColor);
        detailText.Alignment = PixelText.Align.Left;
        PlaceLeft(detailText.rectTransform, new Vector2(textLeft, -32f), new Vector2(right - 24f - textLeft, 24f));

        string levelLabel = max > 0 ? $"LV {stacks}/{max}" : $"LV {stacks}";
        PixelText levelText = NewText("Level", rect, maxed ? "MAX " + levelLabel : levelLabel, 4f, level);
        levelText.Alignment = PixelText.Align.Right;
        Place(levelText.rectTransform, new Vector2(right - 24f - 150f, 28f), new Vector2(300f, 30f));

        if (max > 0)
        {
            const float pipWidth = 26f;
            const float pipGap = 6f;
            for (int i = 0; i < max; i++)
            {
                float x = right - 24f - pipWidth * 0.5f - (max - 1 - i) * (pipWidth + pipGap);
                Image pip = NewImage($"Pip{i}", rect, i < stacks ? level : emptyPipColor, null);
                Place(pip.rectTransform, new Vector2(x, -6f), new Vector2(pipWidth, 10f));
            }
        }

        return new Row { rect = rect, group = rect.gameObject.AddComponent<CanvasGroup>(), y = y };
    }

    private void BuildBottomButtons(RectTransform parent)
    {
        RectTransform buttons = NewRect("Buttons", parent);
        Stretch(buttons);
        buttonsGroup = buttons.gameObject.AddComponent<CanvasGroup>();
        Color ink = new(0.04f, 0.05f, 0.12f, 1f);

        restartImage = NewImage("RestartButton", buttons, restartColor, UiSprites.RoundedRect);
        restartImage.raycastTarget = true;
        restartRect = restartImage.rectTransform;
        Place(restartRect, new Vector2(-300f, ButtonsY), new Vector2(340f, 130f));
        restartButton = restartRect.gameObject.AddComponent<UiButton>();
        restartButton.Clicked += HandleRestartClicked;
        restartLabel = NewText("Label", restartRect, "RESTART", 5f, ink);
        Stretch(restartLabel.rectTransform);

        Image resumeImage = NewImage("ResumeButton", buttons, resumeColor, UiSprites.RoundedRect);
        resumeImage.raycastTarget = true;
        resumeRect = resumeImage.rectTransform;
        Place(resumeRect, new Vector2(185f, ButtonsY), new Vector2(560f, 150f));
        resumeButton = resumeRect.gameObject.AddComponent<UiButton>();
        resumeButton.Clicked += Resume;
        PixelText resumeLabel = NewText("Label", resumeRect, "RESUME", 7f, ink);
        Stretch(resumeLabel.rectTransform);

        BuildHpDisplaySelector(buttons);

        // Developer tool (editor and development builds only): end the run on purpose, to get the Game Over screen
        // and its run data without having to lose a long run.
        if (RunProfiler.Available)
        {
            Image endImage = NewImage("EndRunButton", buttons, new Color(1f, 0.75f, 0.2f, 1f), UiSprites.RoundedRect);
            endImage.raycastTarget = true;
            endRunRect = endImage.rectTransform;
            Place(endRunRect, new Vector2(0f, EndRunY), new Vector2(460f, 90f));
            endRunButton = endRunRect.gameObject.AddComponent<UiButton>();
            endRunButton.Clicked += HandleEndRunClicked;
            PixelText endLabel = NewText("Label", endRunRect, "END RUN (DEV)", 4f, ink);
            Stretch(endLabel.rectTransform);
        }
    }

    // Closes the pause screen at once and ends the run as a lost ball (animation, Game Over screen, run data).
    private void HandleEndRunClicked()
    {
        if (state != State.Open || game == null) return;
        state = State.Hidden;
        root.gameObject.SetActive(false);
        particles.Clear();
        Time.timeScale = savedTimeScale;
        SoundManager.Play(Sfx.Confirm);
        game.EndRun(GameManager.LoseReason.BallLost);
    }

    // "BRICK HP" setting: a segmented control (PIPS / COLOR / NUMBERS) between the list and the buttons.
    private void BuildHpDisplaySelector(RectTransform parent)
    {
        Image panel = NewImage("HpDisplay", parent, panelColor, UiSprites.RoundedRect);
        Place(panel.rectTransform, new Vector2(0f, HpDisplayY), new Vector2(ListWidth, HpDisplayHeight));

        PixelText title = NewText("Label", panel.rectTransform, "BRICK HP", 3f, accentColor);
        Place(title.rectTransform, new Vector2(-ListWidth * 0.5f + 115f, 0f), new Vector2(210f, 30f));

        const float segmentWidth = 236f;
        const float gap = 8f;
        BrickHpDisplay.Mode[] modes = BrickHpDisplay.Order;
        for (int i = 0; i < modes.Length; i++)
        {
            BrickHpDisplay.Mode mode = modes[i];
            float x = ListWidth * 0.5f - 12f - (modes.Length - 1 - i) * (segmentWidth + gap) - segmentWidth * 0.5f;
            Image image = NewImage($"Option_{mode}", panel.rectTransform, rowColor, UiSprites.RoundedRect);
            image.raycastTarget = true;
            Place(image.rectTransform, new Vector2(x, 0f), new Vector2(segmentWidth, HpDisplayHeight - 20f));
            PixelText label = NewText("Label", image.rectTransform, BrickHpDisplay.Label(mode), 4f, detailColor);
            Stretch(label.rectTransform);
            var button = image.gameObject.AddComponent<UiButton>();
            button.Clicked += () =>
            {
                if (BrickHpDisplay.Current == mode) return;
                BrickHpDisplay.Current = mode;
                SoundManager.Play(Sfx.CardSelect, 1.1f);
                RefreshHpDisplaySelector();
            };
            hpDisplayOptions.Add((mode, image, label, button));
        }
        RefreshHpDisplaySelector();
    }

    // The chosen option is filled with the accent color, the others stay dark.
    private void RefreshHpDisplaySelector()
    {
        Color ink = new(0.04f, 0.05f, 0.12f, 1f);
        foreach (var option in hpDisplayOptions)
        {
            bool selected = option.mode == BrickHpDisplay.Current;
            option.image.color = selected ? accentColor : rowColor;
            option.label.color = selected ? ink : detailColor;
        }
    }

    // ---------------------------------------------------------------- small helpers

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
        image.type = sprite != null && sprite == UiSprites.RoundedRect ? Image.Type.Sliced : Image.Type.Simple; // icons stay Simple
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

    // Centered anchors, left pivot: x is where the rect starts.
    private static void PlaceLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        Place(rect, position, size);
        rect.pivot = new Vector2(0f, 0.5f);
    }
}
