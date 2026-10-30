using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Corruption on the level-up card screen. While the player holds a charge, a red CORRUPT button sits under
/// Reroll / Confirm. Corrupting the selected (capped) card plays a ~3 s sequence on unscaled time:
/// <list type="number">
/// <item>Infection (0 s): red flash and kick, the other cards flicker red, then shatter into pixel shards that are
/// sucked into the chosen card, which glides to the center and grows. The dim bleeds to blood red.</item>
/// <item>Virus (0.7 to 2.6 s): an accelerating glitch on the card: RGB split ghosts, horizontal slice tears,
/// corrupted pixel blocks, scrambled text, frame drops, scanlines, noise, a rolling band, screen shake, data streaks
/// pulled in from the screen edges. Everything ramps up with one intensity value.</item>
/// <item>Collapse (2.6 s): CRT switch-off. The card squashes into a white line, then a dot, then a hard cut to black.</item>
/// <item>Reveal (~3 s): Exquisite slams in with a white-red flash, shockwave rings, a spray of red and gold pixels and its
/// RGB ghosts snapping together; Cursed drops in from above with a dull thud, grey dust and a dying-neon stamp.
/// The corrupted card's lowered cap is typed under it, then a single CONFIRM button pops in.</item>
/// </list>
/// The rules (charges, caps, outcome) live in <see cref="PowerUpManager"/>; this only shows them.
/// </summary>
public partial class PowerUpScreen
{
    // Timeline, in seconds from the CORRUPT tap.
    private const float CxShatterStart = 0.1f;
    private const float CxShatterStagger = 0.09f;
    private const float CxMoveStart = 0.2f;
    private const float CxMoveTime = 0.65f;
    private const float CxGlitchStart = 0.7f;
    private const float CxCollapseStart = 2.6f;
    private const float CxCollapseTime = 0.18f;
    private const float CxBlackTime = 0.22f;
    private const float CxRevealAt = CxCollapseStart + CxCollapseTime + CxBlackTime;
    private const float CxConfirmDelayExquisite = 0.7f;
    private const float CxConfirmDelayCursed = 0.9f;
    private const float CxCursedDropTime = 0.2f; // matches the thud in the Cursed sound
    private const float CorruptScale = 1.22f;
    private const string GlitchGlyphs = "%/*?!+-:#<>01@";
    private const int TearCount = 3;
    private const int BlockCount = 7;

    private static readonly Color CorruptRed = new(1f, 0.16f, 0.29f);
    private static readonly Color CorruptCyan = new(0.2f, 0.95f, 1f);
    private static readonly Color CorruptMagenta = new(1f, 0.15f, 0.85f);
    private static readonly Color BloodDim = new(0.16f, 0f, 0.035f);
    private static readonly Color ExquisiteDim = new(0.13f, 0f, 0.03f);
    private static readonly Color CursedDim = new(0.035f, 0.04f, 0.055f);
    private static readonly Color InfectedBody = new(0.16f, 0.01f, 0.04f, 1f);
    private static readonly Color Gold = new(1f, 0.82f, 0.2f);

    private CorruptionHooks corruption;
    private Color dimTint = Color.black;
    private Vector2 particleAttractor;
    private Vector2 confirmHome;

    // CORRUPT button and its hint line.
    private CanvasGroup corruptGroup;
    private UiButton corruptButton;
    private Image corruptBorder;
    private Image corruptFill;
    private PixelText corruptLabel;
    private PixelText corruptHint;
    private PixelText corruptOdds;
    private PowerUpDefinition hintCard;
    private int hintCharges = -1;
    private bool hintDirty = true;

    // Full-screen overlays.
    private Image flash;
    private RawImage scanlines;
    private RawImage noise;
    private Image band;
    private Image ringA;
    private Image ringB;
    private Image afterglow;
    private PixelText stamp;
    private Shadow stampCyan;
    private Shadow stampMagenta;
    private PixelText capNote;
    private readonly List<Image> blocks = new();
    private readonly Dictionary<Image, Color> ringColors = new(); // a ring keeps its color while its alpha animates
    private Texture2D noiseTexture;
    private Color32[] noisePixels;

    // Sequence state.
    private bool corruptActive;
    private CorruptionResult corruptResult;
    private CardView corruptCard;
    private CardView resultCard;
    private int corruptIndex;
    private Vector2 corruptFrom;
    private float corruptFromScale;
    private float corruptTime;
    private bool[] shattered;
    private bool glitchStarted, collapsed, blackDone, revealed, confirmShown;
    private float intensity;
    private float glitchTimer;
    private float microGlitchTimer;
    private float microGlitchLeft;
    private float shake;
    private float punch;
    private float borderFlash;
    private float flashAlpha;
    private Color flashColor;
    private Vector2 jumpOffset;
    private bool frameDropped;
    private int absorbed;
    private float dataAccumulator;
    private float sparkAccumulator;
    private float smokeAccumulator;
    private float confirmAppear;
    private string capText = "";

    // Copies of the card for the RGB split (behind it) and the slice tears (masked strips in front of it).
    private readonly List<RectTransform> ghosts = new();
    private readonly Vector2[] ghostOffsets = new Vector2[2];
    private readonly List<RectTransform> tearMasks = new();
    private readonly List<RectTransform> tearCopies = new();
    private readonly bool[] tearOn = new bool[TearCount];
    private readonly float[] tearY = new float[TearCount];
    private readonly float[] tearH = new float[TearCount];
    private readonly float[] tearX = new float[TearCount];

    // ---------------------------------------------------------------- CORRUPT button

    private void AnimateCorruptButton(float time)
    {
        int charges = corruption?.Charges != null ? corruption.Charges() : 0;
        bool show = charges > 0 && !corruptActive && phase != Phase.Hidden;
        if (corruptGroup.gameObject.activeSelf != show) corruptGroup.gameObject.SetActive(show);
        if (!show) return;

        PowerUpDefinition card = selected >= 0 && selected < choices.Count ? choices[selected] : null;
        bool can = phase == Phase.Visible && card != null && corruption.CanCorrupt(card);
        corruptButton.Interactable = can;
        corruptGroup.alpha = phase == Phase.Hiding ? title.color.a : Mathf.Clamp01(title.color.a * 1.2f);

        // Ready: a throbbing blood-red frame. Otherwise a dark, dead one.
        float throb = 0.5f + 0.5f * Mathf.Sin(time * 7f);
        corruptBorder.color = can ? Color.Lerp(CorruptRed * 0.7f, Color.Lerp(CorruptRed, Color.white, 0.3f), throb) : new Color(0.32f, 0.1f, 0.14f);
        corruptFill.color = can ? new Color(0.17f, 0.01f, 0.05f) : new Color(0.08f, 0.03f, 0.05f);
        float press = corruptButton.IsPressed ? 0.94f : 1f;
        corruptBorder.rectTransform.localScale = Vector3.one * (press * (can ? 1f + 0.025f * throb : 1f));

        // A teaser of what it does: now and then the label glitches for a couple of frames.
        string label = $"CORRUPT x{charges}";
        bool tease = can && Mathf.Repeat(time, 1.7f) < 0.09f;
        corruptLabel.Text = tease ? Scramble(label, 0.5f) : label;
        corruptLabel.rectTransform.anchoredPosition = tease ? new Vector2(Random.Range(-8f, 8f), 0f) : Vector2.zero;
        corruptLabel.color = can ? Color.Lerp(new Color(1f, 0.45f, 0.55f), Color.white, tease ? 0.6f : 0f) : new Color(0.5f, 0.27f, 0.32f);

        if (hintDirty || card != hintCard || charges != hintCharges)
        {
            hintDirty = false;
            hintCard = card;
            hintCharges = charges;
            corruptHint.Text = HintFor(card);
            corruptOdds.Text = OddsFor(card);
        }
    }

    private void RefreshCorruptHint() => hintDirty = true;

    private bool IsCorruptible(PowerUpDefinition card) =>
        card != null && corruption.CanCorrupt(card) && corruption.Odds != null;

    // The odds line, shown in bright red while a corruptible card is selected: "55% EXQUISITE+   45% CURSED-".
    private string OddsFor(PowerUpDefinition card)
    {
        if (!IsCorruptible(card)) return "";
        CorruptionOdds odds = corruption.Odds(card);
        int exquisite = Mathf.RoundToInt(odds.ExquisiteChance * 100f);
        string good = $"{exquisite}% {RarityStyle.GetLabel(odds.ExquisiteRarity)}";
        string bad = $"{100 - exquisite}% {RarityStyle.GetLabel(odds.CursedRarity)}";
        return exquisite >= 100 ? good : exquisite <= 0 ? bad : $"{good}   {bad}";
    }

    private string HintFor(PowerUpDefinition card)
    {
        if (card == null) return "SELECT A CARD TO CORRUPT IT";
        int max = corruption.MaxStacks != null ? corruption.MaxStacks(card) : card.MaxStacks;
        int owned = stackLookup != null ? stackLookup(card) : 0;
        string name = card.DisplayName.ToUpperInvariant();
        if (card.MaxStacks <= 0)
        {
            // Unlimited card: the price is one level you own.
            if (owned <= 0) return "NEEDS AN OWNED LEVEL TO CORRUPT";
            if (!corruption.CanCorrupt(card)) return "THIS CARD CANNOT BE CORRUPTED";
            string levelCost = $"{name} LV {owned} > {owned - 1}";
            if (corruption.Odds == null) return $"BECOMES A RANDOM EXQUISITE OR CURSED CARD\n{levelCost}";
            CorruptionOdds unlimitedOdds = corruption.Odds(card);
            string unlimitedDowry = unlimitedOdds.ExquisiteChance >= 1f ? "" : $"\n{RarityStyle.GetLabel(unlimitedOdds.CursedRarity)} PAYS: {CorruptionDowryInfo.Text(unlimitedOdds.Dowry)}";
            return $"{levelCost}{unlimitedDowry}";
        }
        if (max <= 0 || !corruption.CanCorrupt(card)) return "THIS CARD CANNOT BE CORRUPTED";
        string cost = max - 1 <= owned ? $"{name} GONE FOR THE RUN" : $"{name} MAX LV {max} > {max - 1}";
        if (corruption.Odds == null) return $"BECOMES A RANDOM EXQUISITE OR CURSED CARD\n{cost}";

        CorruptionOdds odds = corruption.Odds(card);
        string dowry = odds.ExquisiteChance >= 1f ? "" : $"\n{RarityStyle.GetLabel(odds.CursedRarity)} PAYS: {CorruptionDowryInfo.Text(odds.Dowry)}";
        return $"{cost}{dowry}";
    }

    // ---------------------------------------------------------------- start

    private void StartCorruption()
    {
        if (phase != Phase.Visible || selected < 0 || corruption?.Corrupt == null) return;
        PowerUpDefinition source = choices[selected];
        if (!corruption.CanCorrupt(source))
        {
            SoundManager.Play(Sfx.Denied);
            return;
        }

        corruptResult = corruption.Corrupt(source);
        if (corruptResult.Outcome == null) return; // both pools used up (CanCorrupt should have said so)

        corruptActive = true;
        corruptIndex = selected;
        corruptCard = cards[selected];
        corruptFrom = corruptCard.Rect.anchoredPosition;
        corruptFromScale = corruptCard.Rect.localScale.x;
        corruptTime = 0f;
        shattered = new bool[cards.Count];
        glitchStarted = collapsed = blackDone = revealed = confirmShown = false;
        intensity = 0f;
        absorbed = 0;
        punch = 0f;
        capText = corruptResult.LevelLost
            ? $"{source.DisplayName.ToUpperInvariant()} LV {corruptResult.OldMax} > {corruptResult.NewMax}"
            : corruptResult.SourceGone
            ? $"{source.DisplayName.ToUpperInvariant()} GONE FOR THE RUN"
            : $"{source.DisplayName.ToUpperInvariant()} MAX LV {corruptResult.OldMax} > {corruptResult.NewMax}";
        if (corruptResult.Dowry != CorruptionDowry.None)
            capText += $"\nDOWRY: {CorruptionDowryInfo.Text(corruptResult.Dowry)}";

        phase = Phase.Corrupting;
        rerollButton.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(false);
        corruptGroup.gameObject.SetActive(false);

        SoundManager.Play(Sfx.CorruptStart);
        Flash(CorruptRed, 0.55f);
        shake = 16f;
        borderFlash = 1f;
        corruptCard.transform.SetAsLastSibling();
        MakeCopies(corruptCard);
        Burst(corruptCard, 24, 1.2f);
    }

    // ---------------------------------------------------------------- per frame

    private void AnimateCorruption(float dt, float time)
    {
        corruptTime += dt;
        float t = corruptTime;

        shake *= Mathf.Exp(-dt * 7f);
        punch *= Mathf.Exp(-dt * 9f);
        borderFlash = Mathf.MoveTowards(borderFlash, 0f, dt * 5f);
        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 2.2f);

        if (!collapsed) AnimateInfection(dt, time, t);
        else if (!revealed) AnimateBlackout(t);
        else AnimateReveal(dt, time, t - CxRevealAt);

        // Screen shake moves the cards and the particles together (the attractor lives in the same space).
        Vector2 shakeOffset = shake > 0.3f ? Random.insideUnitCircle * shake : Vector2.zero;
        cardsRoot.anchoredPosition = shakeOffset;
        particlesRoot.anchoredPosition = shakeOffset;

        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha * flashAlpha);
        if (corruptCard != null) particleAttractor = corruptCard.Rect.anchoredPosition;
        UpdateParticles(dt);
        AnimateConfirmReveal(dt, time);
    }

    // Phases 1 and 2: infection, shatter, glide to the center, then the virus glitch building up.
    private void AnimateInfection(float dt, float time, float t)
    {
        CardView card = corruptCard;

        // The dim bleeds from black to blood red.
        float redK = Smooth01(t / 0.6f);
        dimTint = Color.Lerp(Color.black, BloodDim, redK);
        SetDim(Mathf.Lerp(dimAmount, 0.93f, redK));

        // The other cards flicker red, then burst one after the other, nearest first.
        int shatterOrder = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (i == corruptIndex || shattered[i]) continue;
            float at = CxShatterStart + Mathf.Abs(i - corruptIndex) * CxShatterStagger - CxShatterStagger;
            if (t >= at)
            {
                shattered[i] = true;
                Shatter(cards[i]);
                SoundManager.Play(Sfx.BrickBlast, 1.5f + 0.15f * shatterOrder++, 0.55f);
                continue;
            }
            bool on = Mathf.Repeat(time * 24f, 1f) < 0.5f;
            cards[i].Border.color = on ? CorruptRed : Color.white;
            cards[i].Rect.anchoredPosition = new Vector2(cards[i].BaseX, cardCenterY) + Random.insideUnitCircle * 5f;
        }

        // Glide to the center, a bit bigger, a slight unsteady tilt.
        float moveK = Smooth01((t - CxMoveStart) / CxMoveTime);
        moveK = moveK * moveK * (3f - 2f * moveK); // smootherstep feel
        Vector2 center = new(0f, cardCenterY + 10f);
        Vector2 pos = Vector2.Lerp(corruptFrom, center, moveK);
        float scale = Mathf.Lerp(corruptFromScale, CorruptScale, moveK) * (1f + punch);

        // Virus intensity: 0 at the glitch start, 1 at the collapse.
        intensity = t < CxGlitchStart ? 0f : Mathf.Pow(Smooth01((t - CxGlitchStart) / (CxCollapseStart - CxGlitchStart)), 1.25f);
        if (!glitchStarted && t >= CxGlitchStart)
        {
            glitchStarted = true;
            SoundManager.Play(Sfx.CorruptGlitch);
            glitchTimer = 0f;
        }

        if (t >= CxCollapseStart)
        {
            AnimateCollapse(t - CxCollapseStart, pos);
            return;
        }

        float rot = Mathf.Sin(time * 31f) * intensity * 2.5f + Mathf.Sin(time * 3.1f) * 1.2f * moveK;
        shake = Mathf.Max(shake, 2f + 15f * intensity);

        if (glitchStarted)
        {
            glitchTimer -= dt;
            if (glitchTimer <= 0f)
            {
                glitchTimer = Mathf.Lerp(0.11f, 0.028f, intensity) * Random.Range(0.7f, 1.3f);
                GlitchTick(card, intensity);
            }
        }

        card.Rect.anchoredPosition = pos + jumpOffset;
        card.Rect.localScale = new Vector3(scale, scale, 1f);
        card.Rect.localRotation = Quaternion.Euler(0f, 0f, rot);
        card.Group.alpha = frameDropped ? 0f : 1f;

        // Colors: rarity bleeding into red, white kicks on every absorbed shard, glitch flicker on top.
        float infect = Smooth01(t / 0.8f);
        Color border = Color.Lerp(card.RarityColor, CorruptRed, infect);
        if (glitchStarted && Random.value < 0.25f + 0.5f * intensity)
            border = PickGlitchColor();
        card.Border.color = Color.Lerp(border, Color.white, borderFlash);
        card.Body.color = Color.Lerp(card.BodyColor, InfectedBody, infect);
        card.DiamondBorder.color = Color.Lerp(card.RarityColor, CorruptRed, infect);
        card.Icon.color = Random.value < intensity * 0.3f ? Color.white : Color.Lerp(card.RarityColor, CorruptRed, infect);
        Color halo = CorruptRed;
        halo.a = (0.25f + 0.35f * intensity) * (0.75f + 0.25f * Mathf.Sin(time * 11f));
        card.Halo.color = halo;
        card.Halo.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(time * 9f));

        // Title: decodes into CORRUPTING, keeps glitching harder.
        if (Random.value < 0.35f) title.Text = Scramble("CORRUPTING...", Mathf.Lerp(0.6f, 0.1f, Smooth01(t / 0.5f)) + 0.45f * intensity);
        title.color = Color.Lerp(new Color(1f, 0.35f, 0.45f), Color.white, Random.value < 0.15f ? 0.8f : 0f);

        SyncCopies(card, intensity);

        // Overlays ramp with the intensity.
        float overlay = glitchStarted ? intensity : 0f;
        SetOverlays(0.08f + 0.4f * overlay, 0.03f + 0.17f * overlay, 0.1f + 0.3f * overlay, time, Mathf.Lerp(1.1f, 0.3f, overlay));
        if (overlay > 0f && Time.frameCount % 2 == 0) RefreshNoise(0.02f + 0.07f * overlay);

        // Sparks off the card and data streaks pulled in from the screen edges.
        if (glitchStarted)
        {
            sparkAccumulator += (25f + 80f * intensity) * particleAmount * dt;
            while (sparkAccumulator >= 1f)
            {
                sparkAccumulator -= 1f;
                EmitGlitchSpark(card);
            }
            dataAccumulator += 45f * intensity * particleAmount * dt;
            while (dataAccumulator >= 1f)
            {
                dataAccumulator -= 1f;
                EmitDataStreak(pos);
            }
        }
    }

    // One random glitch frame: RGB split, slice tears, corrupted blocks, scrambled text, maybe a dropped frame.
    private void GlitchTick(CardView card, float k)
    {
        float split = Random.Range(4f, 10f + 34f * k);
        float vertical = Random.value < 0.25f ? Random.Range(-10f, 10f) * k : 0f;
        ghostOffsets[0] = new Vector2(-split, vertical);
        ghostOffsets[1] = new Vector2(split, -vertical);

        float halfHeight = cardHeight * 0.5f;
        for (int i = 0; i < TearCount; i++)
        {
            tearOn[i] = Random.value < 0.25f + 0.65f * k;
            tearH[i] = Random.Range(14f, 30f + 80f * k);
            tearY[i] = Random.Range(-halfHeight, halfHeight);
            tearX[i] = (Random.value < 0.5f ? -1f : 1f) * Random.Range(10f, 24f + 90f * k);
        }

        float scale = card.Rect.localScale.x;
        for (int i = 0; i < blocks.Count; i++)
        {
            Image block = blocks[i];
            bool on = Random.value < 0.15f + 0.6f * k;
            block.enabled = on;
            if (!on) continue;
            block.color = Random.value < 0.15f ? new Color(0f, 0f, 0f, 0.9f) : WithAlpha(PickGlitchColor(), Random.Range(0.6f, 0.95f));
            block.rectTransform.sizeDelta = new Vector2(Random.Range(20f, 60f + 120f * k), Random.Range(6f, 14f + 30f * k));
            block.rectTransform.anchoredPosition = card.Rect.anchoredPosition +
                new Vector2(Random.Range(-card.Width * 0.6f, card.Width * 0.6f), Random.Range(-halfHeight, halfHeight)) * scale;
        }

        for (int i = 0; i < card.Texts.Length; i++)
            card.Texts[i].Text = Scramble(card.TextsOriginal[i], Random.value < 0.6f ? 0.08f + 0.6f * k : 0f);

        frameDropped = Random.value < 0.07f * k;
        jumpOffset = Random.value < 0.12f + 0.25f * k ? Random.insideUnitCircle * (6f + 26f * k) : Vector2.zero;
        if (Random.value < 0.06f * k) Flash(Random.value < 0.5f ? CorruptRed : Color.white, 0.32f);
    }

    // Phase 3: CRT switch-off. The card turns white, squashes into a line, then into nothing.
    private void AnimateCollapse(float c, Vector2 pos)
    {
        CardView card = corruptCard;
        float k = Mathf.Clamp01(c / CxCollapseTime);
        float sx, sy;
        if (k < 0.55f)
        {
            float u = Smooth01(k / 0.55f);
            sx = Mathf.Lerp(CorruptScale, CorruptScale * 1.4f, u);
            sy = Mathf.Lerp(CorruptScale, 0.025f, u * u);
        }
        else
        {
            float u = Smooth01((k - 0.55f) / 0.45f);
            sx = Mathf.Lerp(CorruptScale * 1.4f, 0f, u);
            sy = 0.025f;
        }

        card.Rect.anchoredPosition = pos;
        card.Rect.localRotation = Quaternion.identity;
        card.Rect.localScale = new Vector3(sx, sy, 1f);
        card.Group.alpha = 1f;
        card.Body.color = Color.white;
        card.Border.color = Color.white;
        card.Halo.color = WithAlpha(Color.white, 0.6f * (1f - k));
        foreach (PixelText text in card.Texts) text.color = WithAlpha(text.color, 0f);
        HideCopies();
        foreach (Image block in blocks) block.enabled = false;
        SetOverlays(0.5f * (1f - k), 0.4f * (1f - k), 0f, Time.unscaledTime, 0.3f);
        shake = 4f;

        if (k >= 1f)
        {
            // Hard cut to black: nothing but a fading dot where the card was.
            collapsed = true;
            card.Group.alpha = 0f;
            ClearParticles();
            SetOverlays(0f, 0f, 0f, 0f, 1f);
            dimTint = Color.black;
            SetDim(1f);
            title.Text = "";
            shake = 0f;
            flashAlpha = 0f;
            afterglow.enabled = true;
            afterglow.rectTransform.anchoredPosition = pos;
        }
    }

    private void AnimateBlackout(float t)
    {
        float k = Mathf.Clamp01((t - CxCollapseStart - CxCollapseTime) / CxBlackTime);
        afterglow.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(60f, 10f, Smooth01(k * 2f)), 10f);
        afterglow.color = WithAlpha(Color.white, (1f - k) * (1f - k));
        if (k >= 1f) Reveal();
    }

    // Phase 4: the outcome card appears in place of the corrupted one.
    private void Reveal()
    {
        revealed = true;
        afterglow.enabled = false;
        bool exquisite = corruptResult.IsExquisite;
        PowerUpDefinition outcome = corruptResult.Outcome;

        // Swap the cards for the outcome: it becomes the only (selected) choice, so CONFIRM applies it as the pick.
        DestroyCopies();
        ClearCards();
        int stacks = stackLookup != null ? stackLookup(outcome) : 0;
        resultCard = BuildCard(0, outcome, stacks, cardWidth, 0f);
        resultCard.Clicked = null;
        resultCard.Appear = 1f;
        resultCard.SelectT = 1f;
        cards.Add(resultCard);
        choices = new List<PowerUpDefinition> { outcome };
        selected = 0;
        corruptCard = resultCard;
        MakeCopies(resultCard);

        Vector2 center = new(0f, cardCenterY + 10f);
        string tierName = RarityStyle.GetLabel(outcome.Rarity);
        stamp.Text = exquisite ? tierName + "!" : tierName + "...";
        stamp.color = exquisite ? CorruptRed : RarityStyle.CursedColor;
        stampCyan.enabled = stampMagenta.enabled = exquisite;
        stamp.enabled = true;
        capNote.Text = "";
        capNote.color = exquisite ? new Color(1f, 0.5f, 0.58f) : new Color(0.62f, 0.64f, 0.72f);
        capNote.enabled = true;

        if (exquisite)
        {
            SoundManager.Play(Sfx.CorruptExquisite);
            dimTint = ExquisiteDim;
            Flash(Color.white, 1f);
            shake = 26f;
            StartRing(ringA, CorruptRed);
            StartRing(ringB, Color.white);
            for (int i = 0; i < 110 * particleAmount; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                float roll = Random.value;
                Color color = roll < 0.45f ? CorruptRed : roll < 0.75f ? Color.white : roll < 0.9f ? Gold : CorruptCyan;
                Spawn(center + dir * Random.Range(20f, 120f), dir * Random.Range(300f, 1100f), color,
                    Random.Range(10f, 24f), Random.Range(0.6f, 1.3f), 2.4f);
            }
        }
        else
        {
            SoundManager.Play(Sfx.CorruptCursed);
            dimTint = CursedDim;
            flashColor = new Color(0.02f, 0.02f, 0.03f);
            flashAlpha = 1f; // the black lingers a breath before the card falls in
        }
        SetDim(0.94f);
        particleAttractor = center;
    }

    private void AnimateReveal(float dt, float time, float rt)
    {
        CardView card = resultCard;
        bool exquisite = corruptResult.IsExquisite;
        Vector2 center = new(0f, cardCenterY + 10f);
        float breathe = 1f + 0.012f * Mathf.Sin(time * 4f);
        Vector2 pos;
        float sx, sy;

        SetDim(0.94f + 0.03f * Mathf.Sin(time * 2.2f));

        if (exquisite)
        {
            // Slams in from big, its RGB ghosts snapping together, then lives with small glitch hiccups.
            float k = Mathf.Clamp01(rt / 0.42f);
            float s = Mathf.Lerp(1.75f, 1f, EaseOutBack(k)) * CorruptScale * breathe * (1f + punch);
            sx = sy = s;
            pos = center + new Vector2(0f, 6f * Mathf.Sin(time * 2f));
            if (rt < 0.2f) flashColor = Color.Lerp(Color.white, CorruptRed, rt / 0.2f);

            UpdateMicroGlitch(dt, card);
            float snap = 54f * (1f - Smooth01(rt / 0.5f)) + (microGlitchLeft > 0f ? 14f : 0f);
            ghostOffsets[0] = new Vector2(-snap, 0f);
            ghostOffsets[1] = new Vector2(snap, 0f);

            Color halo = CorruptRed;
            halo.a = 0.45f + 0.2f * Mathf.Sin(time * 5f);
            card.Halo.color = halo;
            card.Halo.rectTransform.localScale = Vector3.one * (1.02f + 0.03f * Mathf.Sin(time * 5f));
            card.Border.color = microGlitchLeft > 0f ? Color.white : Color.Lerp(CorruptRed, Color.white, 0.2f + 0.2f * Mathf.Sin(time * 8f));

            AnimateRing(ringA, rt, 0.7f, 1500f);
            AnimateRing(ringB, rt - 0.08f, 0.55f, 1100f);

            sparkAccumulator += 26f * particleAmount * dt;
            while (sparkAccumulator >= 1f)
            {
                sparkAccumulator -= 1f;
                EmitGlitchSpark(card, Random.value < 0.25f ? Gold : (Color?)null);
            }

            // Stamp: slams down with a chromatic shadow that jitters.
            float sk = Mathf.Clamp01(rt / 0.28f);
            stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, EaseOutBack(sk));
            stamp.color = WithAlpha(CorruptRed, Mathf.Clamp01(rt / 0.08f));
            float jitter = microGlitchLeft > 0f ? 10f : 4f;
            stampCyan.effectDistance = new Vector2(-jitter, Random.Range(-2f, 2f));
            stampMagenta.effectDistance = new Vector2(jitter, Random.Range(-2f, 2f));
            SetOverlays(0.06f, 0f, microGlitchLeft > 0f ? 0.15f : 0f, time, 0.8f);
        }
        else
        {
            // Drops from above like a dead weight, squashes on impact, dust everywhere, then just sits there.
            float k = Mathf.Clamp01(rt / CxCursedDropTime);
            pos = new Vector2(0f, Mathf.Lerp(cardCenterY + 460f, center.y, k * k));
            float squash = 0f;
            if (rt >= CxCursedDropTime)
            {
                float u = rt - CxCursedDropTime;
                squash = Mathf.Exp(-u * 8f) * Mathf.Cos(u * 30f) * 0.14f;
                if (!impactDone)
                {
                    impactDone = true;
                    Impact(center);
                }
            }
            sx = CorruptScale * (1f + squash);
            sy = CorruptScale * (1f - squash);
            ghostOffsets[0] = ghostOffsets[1] = Vector2.zero;

            card.Halo.color = WithAlpha(new Color(0.25f, 0.26f, 0.3f), 0.25f);
            // A broken neon: the frame and the icon stutter now and then.
            float neon = Mathf.PerlinNoise(time * 7f, 0.3f) > 0.32f ? 1f : 0.25f;
            card.Border.color = WithAlpha(RarityStyle.CursedColor * (0.55f + 0.45f * neon), 1f);
            card.Icon.color = WithAlpha(RarityStyle.CursedColor, 0.4f + 0.6f * neon);

            AnimateRing(ringA, rt - CxCursedDropTime, 1.1f, 1400f);

            smokeAccumulator += 9f * particleAmount * dt;
            while (smokeAccumulator >= 1f)
            {
                smokeAccumulator -= 1f;
                Vector2 from = center + new Vector2(Random.Range(-card.Width * 0.6f, card.Width * 0.6f), Random.Range(-cardHeight * 0.5f, cardHeight * 0.6f)) * CorruptScale;
                bool ember = Random.value < 0.2f;
                Color color = ember ? new Color(0.55f, 0.06f, 0.1f) : new Color(0.24f, 0.25f, 0.29f);
                Spawn(from, new Vector2(Random.Range(-20f, 20f), Random.Range(40f, 110f)), color,
                    ember ? Random.Range(6f, 10f) : Random.Range(18f, 34f), Random.Range(1.2f, 2.2f), 0.4f);
            }

            // Stamp: falls with the card, then sags and flickers like a dying sign.
            float drop = Mathf.Lerp(380f, 0f, k * k);
            float sag = rt > CxCursedDropTime ? 16f * Smooth01((rt - CxCursedDropTime) / 1.6f) : 0f;
            stamp.rectTransform.anchoredPosition = new Vector2(0f, StampY + drop - sag);
            stamp.rectTransform.localScale = Vector3.one;
            float flicker = Mathf.PerlinNoise(time * 11f, 1.7f) > 0.3f ? 1f : 0.3f;
            stamp.color = WithAlpha(RarityStyle.CursedColor, Mathf.Clamp01(rt / 0.1f) * flicker);
            SetOverlays(0.05f, 0f, 0f, time, 1f);
        }

        card.Rect.anchoredPosition = pos;
        card.Rect.localScale = new Vector3(sx * breathe, sy * breathe, 1f);
        card.Rect.localRotation = Quaternion.identity;
        card.Group.alpha = 1f;
        SyncCopies(card, exquisite ? 1f : 0f);

        // The cost, typed out under the card.
        float typeT = rt - (exquisite ? 0.35f : 0.45f);
        int chars = typeT > 0f ? Mathf.Min(capText.Length, Mathf.FloorToInt(typeT * 45f)) : 0;
        string shown = capText.Substring(0, chars);
        if (capNote.Text != shown)
        {
            capNote.Text = shown;
            if (chars > 0 && chars % 3 == 0) SoundManager.Play(Sfx.CountTick, 1.6f, 0.25f);
        }

        if (!confirmShown && rt >= (exquisite ? CxConfirmDelayExquisite : CxConfirmDelayCursed))
        {
            confirmShown = true;
            phase = Phase.Revealed;
            confirmButton.gameObject.SetActive(true);
            confirmButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, confirmHome.y - 150f);
            confirmAppear = 0f;
        }
    }

    private bool impactDone;

    // Cursed impact: heavy shake, grey dust thrown sideways along the floor of the card, a dull ring.
    private void Impact(Vector2 center)
    {
        shake = 34f;
        Flash(new Color(0.3f, 0.3f, 0.36f), 0.4f);
        StartRing(ringA, new Color(0.45f, 0.47f, 0.55f));
        float bottom = center.y - cardHeight * 0.5f * CorruptScale;
        for (int i = 0; i < 60 * particleAmount; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector2 from = new(Random.Range(-cardWidth * 0.6f, cardWidth * 0.6f), bottom + Random.Range(-10f, 30f));
            Vector2 velocity = new(side * Random.Range(150f, 700f), Random.Range(80f, 460f));
            Color color = Random.value < 0.15f ? new Color(0.6f, 0.08f, 0.12f) : new Color(0.38f, 0.39f, 0.45f);
            Spawn(from, velocity, color, Random.Range(10f, 22f), Random.Range(0.6f, 1.2f), 1.6f, 1500f);
        }
    }

    // Exquisite only: every second or so the card hiccups for a few frames, as if the corruption still runs in it.
    private void UpdateMicroGlitch(float dt, CardView card)
    {
        if (microGlitchLeft > 0f)
        {
            microGlitchLeft -= dt;
            if (microGlitchLeft <= 0f)
            {
                for (int i = 0; i < TearCount; i++) tearOn[i] = false;
                foreach (Image block in blocks) block.enabled = false;
                for (int i = 0; i < card.Texts.Length; i++) card.Texts[i].Text = card.TextsOriginal[i];
            }
            return;
        }

        microGlitchTimer -= dt;
        if (microGlitchTimer > 0f) return;
        microGlitchTimer = Random.Range(0.8f, 1.8f);
        microGlitchLeft = Random.Range(0.06f, 0.12f);
        GlitchTick(card, 0.35f);
        for (int i = 1; i < TearCount; i++) tearOn[i] = false; // a single tear
        frameDropped = false;
        jumpOffset = Vector2.zero;
        SoundManager.Play(Sfx.CountTick, Random.Range(0.5f, 0.7f), 0.3f);
    }

    private void AnimateConfirmReveal(float dt, float time)
    {
        if (!confirmShown) return;
        confirmAppear = Mathf.Min(1f, confirmAppear + dt / 0.3f);
        bool ready = phase == Phase.Revealed;
        confirmButton.Interactable = ready;
        float pulse = ready ? 1f + 0.03f * Mathf.Sin(time * 6f) : 1f;
        float press = confirmButton.IsPressed ? 0.94f : 1f;
        confirmButton.transform.localScale = Vector3.one * (EaseOutBack(confirmAppear) * pulse * press);
        confirmImage.color = corruptResult.IsExquisite ? Color.Lerp(CorruptRed, Color.white, 0.15f) : new Color(0.42f, 0.44f, 0.52f);
        confirmLabel.color = corruptResult.IsExquisite ? new Color(0.2f, 0f, 0.04f) : new Color(0.06f, 0.06f, 0.08f);
    }

    // While the screen hides after a corruption: the outcome card leaves from its own spot and size.
    private void AnimateResultCardOut(CardView card)
    {
        float appear = Mathf.Clamp01(card.Appear);
        float s = CorruptScale * Mathf.Lerp(0.6f, 1f, appear);
        card.Rect.localScale = new Vector3(s, s, 1f);
        card.Rect.anchoredPosition = new Vector2(0f, cardCenterY + 10f - (1f - appear) * 160f);
        card.Group.alpha = appear;
    }

    private void FadeCorruptionUi(float alpha)
    {
        if (!corruptActive) return;
        stamp.color = WithAlpha(stamp.color, Mathf.Min(stamp.color.a, alpha));
        capNote.color = WithAlpha(capNote.color, alpha);
        ringA.enabled = ringB.enabled = false;
        foreach (Image block in blocks) block.enabled = false;
        HideCopies();
        SetOverlays(0f, 0f, 0f, 0f, 1f);
    }

    // ---------------------------------------------------------------- particles

    // A card bursts into a grid of square shards in its colors, infected with red. After a short flight they are
    // sucked into the corrupted card.
    private void Shatter(CardView card)
    {
        const int columns = 5;
        const int rows = 9;
        Vector2 center = card.Rect.anchoredPosition;
        float scale = card.Rect.localScale.x;
        Vector2 cell = new Vector2(card.Width / columns, cardHeight / rows) * scale;
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            Vector2 offset = new((x - (columns - 1) * 0.5f) * cell.x, (y - (rows - 1) * 0.5f) * cell.y);
            Vector2 dir = offset.sqrMagnitude > 1f ? offset.normalized : Random.insideUnitCircle.normalized;
            bool edge = x == 0 || y == 0 || x == columns - 1 || y == rows - 1;
            float roll = Random.value;
            Color color = edge ? card.RarityColor : roll < 0.45f ? CorruptRed : roll < 0.6f ? Color.white : card.RarityColor;
            Vector2 velocity = dir * Random.Range(220f, 620f) + new Vector2(0f, Random.Range(0f, 200f));
            Spawn(center + offset, velocity, color, Random.Range(18f, 30f), 2.2f, 2.8f, 0f, Random.Range(2600f, 3600f), Random.Range(0.18f, 0.42f));
        }
        Burst(card, 12, 1.4f);
        card.Group.alpha = 0f;
        card.gameObject.SetActive(false);
    }

    private void OnParticleAbsorbed(Color color)
    {
        if (!corruptActive || revealed) return;
        punch = Mathf.Min(punch + 0.018f, 0.1f);
        borderFlash = Mathf.Max(borderFlash, 0.6f);
        if (++absorbed % 3 == 0) SoundManager.Play(Sfx.CountTick, Random.Range(1.4f, 2.1f), 0.22f);
    }

    private void EmitGlitchSpark(CardView card, Color? tint = null)
    {
        float scale = card.Rect.localScale.x;
        Vector2 half = new Vector2(card.Width, cardHeight) * 0.5f * scale;
        Vector2 center = card.Rect.anchoredPosition;
        bool horizontal = Random.value < 0.5f;
        Vector2 offset = horizontal
            ? new Vector2(Random.value < 0.5f ? -half.x : half.x, Random.Range(-half.y, half.y))
            : new Vector2(Random.Range(-half.x, half.x), Random.value < 0.5f ? -half.y : half.y);
        Vector2 velocity = offset.normalized * Random.Range(120f, 420f);
        Color color = tint ?? (Random.value < 0.35f ? Color.white : Random.value < 0.75f ? CorruptRed : CorruptCyan);
        Spawn(center + offset, velocity, color, Random.Range(6f, 14f), Random.Range(0.3f, 0.7f), 3f);
    }

    // Thin bright pixels that come in from the screen edges and get swallowed by the card.
    private void EmitDataStreak(Vector2 target)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector2 from = target + dir * Random.Range(800f, 1100f);
        Vector2 tangent = new Vector2(-dir.y, dir.x) * Random.Range(-300f, 300f);
        Color color = Random.value < 0.5f ? CorruptRed : Random.value < 0.6f ? Color.white : CorruptCyan;
        Spawn(from, tangent, color, Random.Range(6f, 12f), 1.5f, 1.2f, 0f, 5200f);
    }

    private void ClearParticles()
    {
        foreach (Particle p in particles)
        {
            p.life = 0f;
            p.image.enabled = false;
        }
    }

    // ---------------------------------------------------------------- card copies (RGB split, tears)

    private void MakeCopies(CardView source)
    {
        DestroyCopies();
        Color[] tints = { CorruptCyan, CorruptMagenta };
        for (int i = 0; i < 2; i++)
        {
            RectTransform ghost = CopyCard(source, $"Ghost{i}", cardsRoot);
            foreach (Graphic graphic in ghost.GetComponentsInChildren<Graphic>(true))
                graphic.color = WithAlpha(tints[i], graphic.color.a);
            ghost.SetSiblingIndex(source.transform.GetSiblingIndex()); // behind the card
            ghosts.Add(ghost);
            ghostOffsets[i] = Vector2.zero;
        }

        for (int i = 0; i < TearCount; i++)
        {
            RectTransform mask = NewRect($"Tear{i}", cardsRoot);
            mask.anchorMin = mask.anchorMax = mask.pivot = new Vector2(0.5f, 0.5f);
            mask.gameObject.AddComponent<RectMask2D>();
            RectTransform copy = CopyCard(source, "Copy", mask);
            if (i == 0) // one tear is a red negative, the others show the card shifted
                foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true))
                    graphic.color = WithAlpha(Color.Lerp(CorruptRed, Color.white, graphic is PixelText ? 0.8f : 0f), graphic.color.a);
            tearMasks.Add(mask);
            tearCopies.Add(copy);
            tearOn[i] = false;
        }
        HideCopies();
    }

    private RectTransform CopyCard(CardView source, string name, Transform parent)
    {
        GameObject copy = Instantiate(source.gameObject, parent, false);
        copy.name = name;
        Destroy(copy.GetComponent<CardView>());
        var group = copy.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;
        foreach (string part in new[] { "Halo", "Tag" })
        {
            Transform child = copy.transform.Find(part);
            if (child != null) child.gameObject.SetActive(false);
        }
        foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        return (RectTransform)copy.transform;
    }

    private void SyncCopies(CardView card, float k)
    {
        Vector2 pos = card.Rect.anchoredPosition;
        Vector3 scale = card.Rect.localScale;
        Quaternion rotation = card.Rect.localRotation;
        bool visible = card.Group.alpha > 0f;

        for (int i = 0; i < ghosts.Count; i++)
        {
            RectTransform ghost = ghosts[i];
            bool on = visible && ghostOffsets[i].sqrMagnitude > 1f;
            ghost.gameObject.SetActive(on);
            if (!on) continue;
            ghost.anchoredPosition = pos + ghostOffsets[i];
            ghost.localScale = scale;
            ghost.localRotation = rotation;
            ghost.GetComponent<CanvasGroup>().alpha = 0.5f + 0.2f * k;
        }

        for (int i = 0; i < tearMasks.Count; i++)
        {
            bool on = visible && tearOn[i];
            tearMasks[i].gameObject.SetActive(on);
            if (!on) continue;
            tearMasks[i].SetAsLastSibling();
            tearMasks[i].anchoredPosition = pos + new Vector2(0f, tearY[i] * scale.y);
            tearMasks[i].sizeDelta = new Vector2(card.Width * scale.x + 320f, tearH[i]);
            tearCopies[i].anchoredPosition = new Vector2(tearX[i], -tearY[i] * scale.y);
            tearCopies[i].localScale = scale;
            tearCopies[i].localRotation = rotation;
        }

        foreach (Image block in blocks) block.rectTransform.SetAsLastSibling();
    }

    private void HideCopies()
    {
        foreach (RectTransform ghost in ghosts) ghost.gameObject.SetActive(false);
        foreach (RectTransform mask in tearMasks) mask.gameObject.SetActive(false);
    }

    private void DestroyCopies()
    {
        foreach (RectTransform ghost in ghosts) if (ghost != null) Destroy(ghost.gameObject);
        foreach (RectTransform mask in tearMasks) if (mask != null) Destroy(mask.gameObject);
        ghosts.Clear();
        tearMasks.Clear();
        tearCopies.Clear();
    }

    // ---------------------------------------------------------------- overlays

    private void Flash(Color color, float strength)
    {
        flashColor = color;
        flashAlpha = Mathf.Max(flashAlpha, strength);
    }

    private void SetOverlays(float scanAlpha, float noiseAlpha, float bandAlpha, float time, float bandPeriod)
    {
        scanlines.enabled = scanAlpha > 0f;
        scanlines.color = WithAlpha(Color.white, scanAlpha);
        float rows = 1920f / 8f;
        scanlines.uvRect = new Rect(0f, Mathf.Repeat(time * 0.5f, 1f), 1f, rows);

        noise.enabled = noiseAlpha > 0f;
        noise.color = WithAlpha(Color.white, noiseAlpha);

        band.enabled = bandAlpha > 0f;
        if (band.enabled)
        {
            float y = Mathf.Lerp(1100f, -1100f, Mathf.Repeat(time / bandPeriod, 1f));
            band.rectTransform.anchoredPosition = new Vector2(0f, y);
            band.color = WithAlpha(CorruptRed, bandAlpha);
        }
    }

    private void RefreshNoise(float density)
    {
        for (int i = 0; i < noisePixels.Length; i++)
        {
            float roll = Random.value;
            noisePixels[i] = roll < density * 0.25f ? new Color32(255, 255, 255, 200)
                : roll < density ? new Color32(255, 40, 70, 160)
                : new Color32(0, 0, 0, 0);
        }
        noiseTexture.SetPixels32(noisePixels);
        noiseTexture.Apply(false);
    }

    private void StartRing(Image ring, Color color)
    {
        ring.enabled = true;
        ring.color = WithAlpha(color, 0f);
        ring.rectTransform.sizeDelta = Vector2.zero;
        ring.rectTransform.anchoredPosition = new Vector2(0f, cardCenterY + 10f);
        ringColors[ring] = color;
    }

    private void AnimateRing(Image ring, float t, float duration, float maxSize)
    {
        if (!ring.enabled || t < 0f) return;
        float k = Mathf.Clamp01(t / duration);
        float size = Mathf.Lerp(160f, maxSize, 1f - (1f - k) * (1f - k) * (1f - k));
        ring.rectTransform.sizeDelta = new Vector2(size, size);
        ring.color = WithAlpha(ringColors[ring], 0.7f * (1f - k) * (1f - k));
        if (k >= 1f) ring.enabled = false;
    }

    // ---------------------------------------------------------------- construction and reset

    private float StampY => cardCenterY + 10f + (cardHeight * 0.5f + 62f) * CorruptScale + 110f;

    private void BuildCorruptionUi(float buttonY)
    {
        // CORRUPT button: a blood-red frame around a dark fill, under Reroll / Confirm.
        RectTransform container = NewRect("Corruption", root);
        Stretch(container);
        corruptGroup = container.gameObject.AddComponent<CanvasGroup>();

        corruptBorder = NewImage("CorruptButton", container, CorruptRed, true);
        Place(corruptBorder.rectTransform, Vector2.zero, new Vector2(0f, buttonY - 150f), new Vector2(440f, 110f));
        corruptButton = corruptBorder.gameObject.AddComponent<UiButton>();
        corruptButton.Clicked += StartCorruption;
        corruptButton.DeniedClick += () => SoundManager.Play(Sfx.Denied);
        corruptFill = NewImage("Fill", corruptBorder.rectTransform, Color.black, false);
        Stretch(corruptFill.rectTransform);
        corruptFill.rectTransform.offsetMin = new Vector2(7f, 7f);
        corruptFill.rectTransform.offsetMax = new Vector2(-7f, -7f);
        RectTransform labelHolder = NewRect("LabelHolder", corruptBorder.rectTransform);
        Stretch(labelHolder);
        corruptLabel = NewText("Label", labelHolder, "CORRUPT", 5f, CorruptRed, PixelText.Align.Center);
        Stretch(corruptLabel.rectTransform);

        // The odds of an Exquisite outcome, big and bright just under the button, then the cost and the Cursed dowry.
        corruptOdds = NewText("Odds", container, "", 4f, new Color(1f, 0.55f, 0.62f), PixelText.Align.Center);
        Place(corruptOdds.rectTransform, Vector2.zero, new Vector2(0f, buttonY - 232f), new Vector2(1000f, 40f));
        corruptHint = NewText("Hint", container, "", 3f, new Color(0.85f, 0.5f, 0.56f), PixelText.Align.Center);
        corruptHint.Wrap = true;
        Place(corruptHint.rectTransform, Vector2.zero, new Vector2(0f, buttonY - 305f), new Vector2(960f, 100f));
        container.gameObject.SetActive(false);

        // Overlays, on top of everything and never catching taps.
        scanlines = NewRaw("Scanlines", BuildScanlineTexture());
        noiseTexture = new Texture2D(108, 192, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        noisePixels = new Color32[noiseTexture.width * noiseTexture.height];
        noise = NewRaw("Noise", noiseTexture);

        band = NewImage("Band", root, Color.clear, false);
        band.sprite = null;
        band.type = Image.Type.Simple;
        band.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        band.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        band.rectTransform.sizeDelta = new Vector2(0f, 70f);
        band.enabled = false;

        for (int i = 0; i < BlockCount; i++)
        {
            Image block = NewImage("Block", cardsRoot, Color.clear, false);
            block.sprite = null;
            block.type = Image.Type.Simple;
            block.enabled = false;
            blocks.Add(block);
        }

        ringA = NewRing("RingA");
        ringB = NewRing("RingB");
        // Behind the cards, so a shockwave never crosses the card's text.
        ringA.transform.SetSiblingIndex(cardsRoot.GetSiblingIndex());
        ringB.transform.SetSiblingIndex(cardsRoot.GetSiblingIndex());

        stamp = NewText("Stamp", root, "", 10f, CorruptRed, PixelText.Align.Center);
        Place(stamp.rectTransform, Vector2.zero, new Vector2(0f, StampY), new Vector2(1060f, 90f));
        stampCyan = stamp.gameObject.AddComponent<Shadow>();
        stampCyan.effectColor = WithAlpha(CorruptCyan, 0.8f);
        stampMagenta = stamp.gameObject.AddComponent<Shadow>();
        stampMagenta.effectColor = WithAlpha(CorruptMagenta, 0.8f);
        stamp.enabled = false;

        capNote = NewText("CapNote", root, "", 3f, Color.white, PixelText.Align.Center);
        Place(capNote.rectTransform, Vector2.zero, new Vector2(0f, cardCenterY + 10f - (cardHeight * 0.5f) * CorruptScale - 62f), new Vector2(960f, 70f)); // two lines: the lowered cap, then a Cursed outcome's dowry
        capNote.enabled = false;

        afterglow = NewImage("Afterglow", root, Color.white, false);
        afterglow.sprite = null;
        afterglow.type = Image.Type.Simple;
        Place(afterglow.rectTransform, Vector2.zero, Vector2.zero, new Vector2(10f, 10f));
        afterglow.enabled = false;

        flash = NewImage("Flash", root, Color.clear, false);
        flash.sprite = null;
        flash.type = Image.Type.Simple;
        Stretch(flash.rectTransform);
    }

    private RawImage NewRaw(string name, Texture texture)
    {
        RectTransform rect = NewRect(name, root);
        Stretch(rect);
        var raw = rect.gameObject.AddComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;
        raw.enabled = false;
        return raw;
    }

    private Image NewRing(string name)
    {
        Image ring = NewImage(name, root, Color.clear, false);
        ring.sprite = UiSprites.Ring;
        ring.type = Image.Type.Simple;
        Place(ring.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero);
        ring.enabled = false;
        return ring;
    }

    private void ResetCorruption()
    {
        if (root == null) return;
        corruptActive = false;
        revealed = collapsed = confirmShown = impactDone = false;
        corruptCard = null;
        resultCard = null;
        DestroyCopies();
        foreach (Image block in blocks) block.enabled = false;
        SetOverlays(0f, 0f, 0f, 0f, 1f);
        ringA.enabled = ringB.enabled = false;
        stamp.enabled = false;
        stamp.rectTransform.anchoredPosition = new Vector2(0f, StampY);
        capNote.enabled = false;
        afterglow.enabled = false;
        flashAlpha = 0f;
        flash.color = Color.clear;
        dimTint = Color.black;
        title.Text = "CHOOSE AN UPGRADE";
        cardsRoot.anchoredPosition = Vector2.zero;
        particlesRoot.anchoredPosition = Vector2.zero;
        shake = punch = borderFlash = 0f;
        microGlitchTimer = 0.9f;
        microGlitchLeft = 0f;
        frameDropped = false;
        jumpOffset = Vector2.zero;
        rerollButton.gameObject.SetActive(true);
        confirmButton.gameObject.SetActive(true);
        confirmButton.GetComponent<RectTransform>().anchoredPosition = confirmHome;
        confirmButton.transform.localScale = Vector3.one;
        hintDirty = true;
    }

    // A dark diagonal hatching over a Cursed card's body.
    private static void AddHatch(RectTransform card)
    {
        RectTransform rect = NewRect("Hatch", card);
        Stretch(rect);
        var raw = rect.gameObject.AddComponent<RawImage>();
        raw.texture = HatchTexture;
        raw.raycastTarget = false;
        raw.uvRect = new Rect(0f, 0f, 300f / 24f, 560f / 24f);
        raw.color = new Color(1f, 1f, 1f, 0.9f);
    }

    private static Texture2D hatchTexture;
    private static Texture2D HatchTexture
    {
        get
        {
            if (hatchTexture != null) return hatchTexture;
            hatchTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                hatchTexture.SetPixel(x, y, (x + y) % 8 < 3 ? new Color(0f, 0f, 0f, 0.55f) : new Color(0.5f, 0.52f, 0.6f, 0.05f));
            hatchTexture.Apply(false);
            return hatchTexture;
        }
    }

    private static Texture2D BuildScanlineTexture()
    {
        var texture = new Texture2D(1, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        texture.SetPixels(new[] { Color.clear, Color.clear, new Color(0f, 0f, 0f, 0.75f), new Color(0f, 0f, 0f, 0.75f) });
        texture.Apply(false);
        return texture;
    }

    // ---------------------------------------------------------------- helpers

    private static string Scramble(string text, float amount)
    {
        if (amount <= 0f || string.IsNullOrEmpty(text)) return text;
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] != ' ' && chars[i] != '\n' && Random.value < amount)
                chars[i] = GlitchGlyphs[Random.Range(0, GlitchGlyphs.Length)];
        return new string(chars);
    }

    private static Color PickGlitchColor()
    {
        float roll = Random.value;
        return roll < 0.45f ? CorruptRed : roll < 0.65f ? Color.white : roll < 0.85f ? CorruptCyan : CorruptMagenta;
    }

    private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

    private static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
