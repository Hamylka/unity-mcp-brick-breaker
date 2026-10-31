using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Corruption, reached from the level-up card screen. While the player holds a charge, a red CORRUPT button sits under
/// Reroll / Confirm. It does not use the offer: it slides the card screen up and out and brings in the corruption
/// screen from below, a scrolling inventory of every owned upgrade (3 per row) over an infected, flickering background.
/// The player taps an upgrade (target brackets lock on it), reads the odds and the cost in the bottom bar, then:
/// <list type="number">
/// <item>Infection (0 s): red flash and kick, the other visible cards flicker red, then shatter into pixel shards that
/// are sucked into the chosen card, which glides to the center and grows. The background ramps up with the virus.</item>
/// <item>Virus (0.7 to 2.6 s): an accelerating glitch on the card: RGB split ghosts, horizontal slice tears,
/// corrupted pixel blocks, scrambled text, frame drops, scanlines, noise, a rolling band, screen shake, data streaks
/// pulled in from the screen edges. Everything ramps up with one intensity value.</item>
/// <item>Collapse (2.6 s): CRT switch-off. The card squashes into a white line, then a dot, then a hard cut to black.</item>
/// <item>Reveal (~3 s): Exquisite slams in with a white-red flash, shockwave rings, a spray of red and gold pixels and its
/// RGB ghosts snapping together; Cursed drops in from above with a dull thud, grey dust and a dying-neon stamp.
/// The level lost is typed under it, then a single CONFIRM button pops in.</item>
/// <item>Return: the outcome card is sucked up out of the screen and the card screen slides back down with the same
/// offer and rerolls; the level-up pick is still to make.</item>
/// </list>
/// The rules (charges, levels, outcome) live in <see cref="PowerUpManager"/>; this only shows them.
/// </summary>
public partial class PowerUpScreen
{
    // Timeline, in seconds from the CORRUPT tap.
    private const float CxShatterStart = 0.1f;
    private const float CxShatterStagger = 0.06f;
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

    // Slide between the card screen and the corruption screen, and the inventory layout (reference units).
    private const float SlideTime = 0.7f;
    private const float ReturnSlideDelay = 0.22f;
    private const int InvColumns = 3;
    private const float InvCardWidth = 290f;
    private const float InvScale = 0.8f;      // inventory cards are offer cards drawn smaller
    private const float InvColumnPitch = 320f;
    private const float InvRowPitch = 530f;
    private const float InvTopPad = 70f;      // room for the header diamond and the brackets of the first row
    private const float InvTop = 300f;        // from the top of the screen: title, subtitle, charges
    private const float InvBottom = 440f;     // from the bottom: odds, cost, buttons
    private const float InvStagger = 0.035f;
    private const float OddsBarWidth = 760f;
    private const float NoiseRate = 4f;       // background noise redraws per second (calm; faster during the virus)
    private const float BgDipTime = 0.6f;     // a soft dim of the background field, down and back up
    private const float BgTearTime = 0.25f;   // a rare tear fades out over this long

    private static readonly Color CorruptRed = new(1f, 0.16f, 0.29f);
    private static readonly Color CorruptCyan = new(0.2f, 0.95f, 1f);
    private static readonly Color CorruptMagenta = new(1f, 0.15f, 0.85f);
    private static readonly Color BloodDim = new(0.16f, 0f, 0.035f);
    private static readonly Color ExquisiteDim = new(0.13f, 0f, 0.03f);
    private static readonly Color CursedDim = new(0.035f, 0.04f, 0.055f);
    private static readonly Color InfectedBody = new(0.16f, 0.01f, 0.04f, 1f);
    private static readonly Color Gold = new(1f, 0.82f, 0.2f);
    private static readonly Color LockedGrey = new(0.55f, 0.57f, 0.65f);

    private CorruptionHooks corruption;
    private Color dimTint = Color.black;
    private Vector2 particleAttractor;

    // CORRUPT button on the card screen and its hint line.
    private CanvasGroup corruptGroup;
    private UiButton corruptButton;
    private Image corruptBorder;
    private Image corruptFill;
    private PixelText corruptLabel;
    private PixelText corruptHint;
    private string hintText;

    // Slide: 0 = card screen, 1 = corruption screen.
    private float slideProgress;
    private float slideTarget;
    private Image seam;
    private float seamSparkAccumulator;

    // Corruption screen: background, title, inventory, bottom bar.
    private RectTransform corruptRoot;
    private CanvasGroup backgroundGroup;
    private RawImage field;
    private RawImage bgScan;
    private RawImage bgNoise;
    private Image bgBand;
    private readonly Image[] bgTears = new Image[2];
    private Texture2D bgNoiseTexture;
    private Color32[] bgNoisePixels;
    private float bgNoiseTimer;
    private float bgTearTimer;
    private float bgTearLeft;
    private float bgDropLeft;
    private float infection;        // how hard the background flickers, 0 to 1
    private PixelText corruptTitle;
    private Shadow corruptTitleCyan;
    private Shadow corruptTitleMagenta;
    private float titleGlitchTimer;
    private float titleGlitchLeft;
    private CanvasGroup inventoryGroup;
    private PixelText chargesText;
    private RectTransform viewport;
    private RectTransform content;
    private ScrollRect inventoryScroll;
    private PixelText emptyText;
    private PixelText oddsGood;
    private PixelText oddsBad;
    private Image oddsTrack;
    private Image oddsFillGood;
    private Image oddsFillBad;
    private Image oddsTick;
    private PixelText costText;
    private UiButton backButton;
    private Image backImage;
    private PixelText backLabel;
    private UiButton goButton;
    private Image goBorder;
    private Image goFill;
    private PixelText goLabel;
    private RectTransform brackets;
    private readonly Image[] bracketBars = new Image[8];
    private float bracketClose;

    private readonly List<CardView> invCards = new();
    private readonly List<PowerUpDefinition> invDefs = new();
    private readonly List<bool> invLocked = new();
    private readonly List<RectTransform> invLocks = new();
    private int invSelected = -1;
    private float invTime;
    private float invGlitchTimer;
    private int invGlitchCard = -1;
    private float invGlitchLeft;

    // Stage: the cards of the sequence, lifted out of the scrolling inventory, and their glitch copies.
    private RectTransform stageRoot;
    private readonly List<CardView> seqCards = new();
    private readonly List<Vector2> seqHome = new();
    private float[] shatterAt;

    // Full-screen overlays of the sequence.
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
    private UiButton revealButton;
    private Image revealImage;
    private PixelText revealLabel;
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
    private bool glitchStarted, collapsed, revealed, confirmShown, impactDone;
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
    private float returnTime;
    private Vector2 returnFrom;
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

    private Vector2 StageCenter => new(0f, cardCenterY + 10f);

    // ---------------------------------------------------------------- CORRUPT button (card screen)

    private void AnimateCorruptButton(float time)
    {
        int charges = corruption?.Charges != null ? corruption.Charges() : 0;
        bool show = charges > 0 && !corruptActive && phase != Phase.Hidden;
        if (corruptGroup.gameObject.activeSelf != show) corruptGroup.gameObject.SetActive(show);
        if (!show) return;

        bool can = phase == Phase.Visible && corruption.CanOpen != null && corruption.CanOpen();
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

        string hint = can || phase != Phase.Visible ? "SACRIFICE A LEVEL OF ANY UPGRADE YOU OWN" : "NOTHING YOU OWN CAN BE CORRUPTED YET";
        if (hint != hintText) corruptHint.Text = hintText = hint;
    }

    // Card screen > corruption screen.
    private void OpenCorruption()
    {
        if (phase != Phase.Visible || corruption?.CanOpen == null || !corruption.CanOpen())
        {
            SoundManager.Play(Sfx.Denied);
            return;
        }

        ApplyBackdrop(); // the Inspector choice may have changed since the last time
        BuildInventory();
        phase = Phase.Inventory;
        slideTarget = 1f;
        invTime = 0f;
        infection = 0.6f; // arrives hot, settles while the cards come in
        corruptTitle.Text = "CORRUPT";
        backgroundGroup.alpha = 1f;
        inventoryGroup.alpha = 1f;
        Flash(CorruptRed, 0.35f);
        SoundManager.Play(Sfx.CorruptStart, 0.8f, 0.5f);
    }

    // Corruption screen > card screen, the charge unspent.
    private void CloseCorruption()
    {
        if (phase != Phase.Inventory || slideTarget < 1f) return;
        SelectInventory(-1);
        slideTarget = 0f;
        SoundManager.Play(Sfx.CardsAppear, 0.8f, 0.7f);
    }

    // ---------------------------------------------------------------- inventory

    private void BuildInventory()
    {
        ClearInventory();
        var owned = new List<KeyValuePair<PowerUpDefinition, int>>();
        if (corruption?.Owned != null)
            foreach (var pair in corruption.Owned())
                if (pair.Key != null && pair.Value > 0) owned.Add(pair);

        // Rarest first, like the pause screen: Exquisite on top, Cursed at the end.
        owned.Sort((a, b) =>
        {
            int byRank = InventoryRank(b.Key.Rarity).CompareTo(InventoryRank(a.Key.Rarity));
            return byRank != 0 ? byRank : string.CompareOrdinal(a.Key.DisplayName, b.Key.DisplayName);
        });

        float halfHeight = cardHeight * 0.5f * InvScale;
        for (int i = 0; i < owned.Count; i++)
        {
            int column = i % InvColumns;
            int row = i / InvColumns;
            float x = (column - (InvColumns - 1) * 0.5f) * InvColumnPitch;
            CardView card = BuildCard(i, owned[i].Key, owned[i].Value, InvCardWidth, x, content, owned: true);
            card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0.5f, 1f);
            card.Rect.anchoredPosition = new Vector2(x, -InvTopPad - halfHeight - row * InvRowPitch);
            card.Rect.localScale = Vector3.one * InvScale;
            card.Clicked = SelectInventory;
            card.Appear = 0f;

            bool locked = corruption.IsCorruptible == null || !corruption.IsCorruptible(owned[i].Key);
            invCards.Add(card);
            invDefs.Add(owned[i].Key);
            invLocked.Add(locked);
            invLocks.Add(locked ? AddLockPlate(card) : null);
        }

        int rows = Mathf.CeilToInt(owned.Count / (float)InvColumns);
        content.sizeDelta = new Vector2(0f, InvTopPad + rows * InvRowPitch);
        content.anchoredPosition = Vector2.zero;
        inventoryScroll.velocity = Vector2.zero;
        emptyText.enabled = owned.Count == 0;
        invSelected = -1;
        invGlitchTimer = 1.2f;
        RefreshBar();
    }

    private static int InventoryRank(PowerUpRarity rarity) =>
        RarityStyle.IsExquisite(rarity) ? 100 : RarityStyle.IsCursed(rarity) ? -100 : (int)rarity;

    // A dark plate with a padlock over a card that cannot pay a level.
    private RectTransform AddLockPlate(CardView card)
    {
        Image plate = NewImage("Lock", card.Rect, new Color(0.03f, 0.03f, 0.05f, 0.92f), false);
        Place(plate.rectTransform, Vector2.zero, new Vector2(0f, -10f), new Vector2(InvCardWidth - 30f, 84f));
        plate.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -9f);
        Image edge = NewImage("Edge", plate.rectTransform, Color.clear, false);
        Stretch(edge.rectTransform);
        edge.rectTransform.offsetMin = new Vector2(-5f, -5f);
        edge.rectTransform.offsetMax = new Vector2(5f, 5f);
        edge.color = WithAlpha(LockedGrey, 0.5f);
        edge.transform.SetAsFirstSibling();
        PixelText label = NewText("Label", plate.rectTransform, "$ LOCKED", 5f, LockedGrey, PixelText.Align.Center);
        Stretch(label.rectTransform);
        return plate.rectTransform;
    }

    private void ClearInventory()
    {
        foreach (CardView card in invCards)
            if (card != null) Destroy(card.gameObject);
        invCards.Clear();
        invDefs.Clear();
        invLocked.Clear();
        invLocks.Clear();
        invSelected = -1;
        if (brackets != null)
        {
            brackets.SetParent(content, false);
            brackets.gameObject.SetActive(false);
        }
    }

    private void SelectInventory(int index)
    {
        if (phase != Phase.Inventory || slideTarget < 1f) return;
        if (index >= 0 && invLocked[index])
        {
            SoundManager.Play(Sfx.Denied);
            if (invLocks[index] != null) invLocks[index].localScale = Vector3.one * 1.18f; // the lock jolts
            return;
        }
        if (index == invSelected) return;

        invSelected = index;
        if (index < 0)
        {
            brackets.gameObject.SetActive(false);
            RefreshBar();
            return;
        }

        CardView card = invCards[index];
        card.transform.SetAsLastSibling();
        SoundManager.Play(Sfx.CardSelect, 0.85f);
        SoundManager.Play(Sfx.CountTick, 0.6f, 0.35f);
        Burst(card, 16, 0.9f);
        brackets.SetParent(card.Rect, false);
        brackets.gameObject.SetActive(true);
        bracketClose = 0f;
        RefreshBar();
    }

    private void RefreshBar()
    {
        int charges = corruption?.Charges != null ? corruption.Charges() : 0;
        chargesText.Text = $"CHARGES x{charges}";

        PowerUpDefinition card = invSelected >= 0 ? invDefs[invSelected] : null;
        bool hasCard = card != null;
        oddsTrack.enabled = oddsFillGood.enabled = oddsFillBad.enabled = oddsTick.enabled = hasCard;
        if (!hasCard)
        {
            oddsGood.Text = invCards.Count == 0 ? "" : "SELECT AN UPGRADE";
            oddsGood.Alignment = PixelText.Align.Center;
            oddsGood.rectTransform.anchoredPosition = new Vector2(0f, oddsGood.rectTransform.anchoredPosition.y);
            oddsGood.color = new Color(1f, 0.55f, 0.62f);
            oddsBad.Text = "";
            costText.Text = "IT LOSES ONE LEVEL. YOU GAIN A RANDOM\nEXQUISITE OR CURSED CARD.";
            return;
        }

        float chance = corruption.ExquisiteChance != null ? corruption.ExquisiteChance(card) : 0.5f;
        int good = Mathf.RoundToInt(chance * 100f);
        oddsGood.Alignment = PixelText.Align.Left;
        oddsGood.rectTransform.anchoredPosition = new Vector2(0f, oddsGood.rectTransform.anchoredPosition.y);
        oddsGood.color = Color.Lerp(CorruptRed, Color.white, 0.25f);
        oddsGood.Text = good > 0 ? $"{good}% {RarityStyle.GetLabel(PowerUpRarity.Exquisite)}" : "";
        oddsBad.Text = good < 100 ? $"{100 - good}% {RarityStyle.GetLabel(PowerUpRarity.Cursed)}" : "";

        int level = stackLookup != null ? stackLookup(card) : 1;
        string name = card.DisplayName.ToUpperInvariant();
        string cost = level <= 1 ? $"{name} IS REMOVED (1 > 0)" : $"LOSE 1 LEVEL OF {name} ({level} > {level - 1})";
        costText.Text = $"{cost}\nYOU GAIN A RANDOM EXQUISITE OR CURSED CARD";
    }

    private void AnimateInventory(float dt, float time)
    {
        invTime += dt;
        bool ready = phase == Phase.Inventory && slideTarget >= 1f && slideProgress >= 1f;

        // An idle infected card now and then: a short scramble and a red border kick.
        invGlitchTimer -= dt;
        if (invGlitchLeft > 0f)
        {
            invGlitchLeft -= dt;
            if (invGlitchLeft <= 0f && invGlitchCard >= 0 && invGlitchCard < invCards.Count)
            {
                CardView c = invCards[invGlitchCard];
                for (int i = 0; i < c.Texts.Length; i++) c.Texts[i].Text = c.TextsOriginal[i];
                c.Rect.localRotation = Quaternion.identity;
                invGlitchCard = -1;
            }
        }
        else if (invGlitchTimer <= 0f && invCards.Count > 0)
        {
            invGlitchTimer = Random.Range(0.7f, 2.2f);
            invGlitchCard = Random.Range(0, invCards.Count);
            invGlitchLeft = Random.Range(0.06f, 0.11f);
            CardView c = invCards[invGlitchCard];
            for (int i = 0; i < c.Texts.Length; i++) c.Texts[i].Text = Scramble(c.TextsOriginal[i], 0.25f);
            c.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-1.5f, 1.5f));
        }

        float halfHeight = cardHeight * 0.5f * InvScale;
        for (int i = 0; i < invCards.Count; i++)
        {
            CardView card = invCards[i];
            int column = i % InvColumns;
            int row = i / InvColumns;
            Vector2 home = new((column - (InvColumns - 1) * 0.5f) * InvColumnPitch, -InvTopPad - halfHeight - row * InvRowPitch);

            // Pop in row by row while the screen arrives, flickering red as they come.
            float start = 0.25f + (row * 2 + column * 0.6f) * InvStagger;
            float t = Mathf.Clamp01((invTime - start) / 0.32f);
            card.Appear = t;
            bool isSelected = i == invSelected;
            card.SelectT = Mathf.MoveTowards(card.SelectT, isSelected ? 1f : 0f, dt * 7f);
            bool glitching = i == invGlitchCard;

            float scale = InvScale * Mathf.Lerp(0.6f, 1f, EaseOutBack(t)) * (1f + 0.05f * card.SelectT);
            card.Rect.localScale = new Vector3(scale, scale, 1f);
            Vector2 jitter = glitching ? new Vector2(Random.Range(-6f, 6f), 0f) : Vector2.zero;
            card.Rect.anchoredPosition = home + new Vector2(0f, card.SelectT * 14f - (1f - t) * 60f) + jitter;
            bool locked = invLocked[i];
            float dimmed = locked ? 0.5f : invSelected >= 0 && !isSelected ? 0.75f : 1f;
            card.Group.alpha = t * dimmed;

            Color dark = new(card.RarityColor.r * 0.5f, card.RarityColor.g * 0.5f, card.RarityColor.b * 0.5f, 1f);
            bool arriving = t > 0f && t < 0.5f && Mathf.Repeat(time * 30f, 1f) < 0.5f;
            Color border = locked ? new Color(0.22f, 0.23f, 0.28f) : dark;
            if (arriving || glitching) border = CorruptRed;
            float throb = 0.5f + 0.5f * Mathf.Sin(time * 8f);
            border = Color.Lerp(border, Color.Lerp(CorruptRed, Color.white, 0.3f * throb), card.SelectT);
            card.Border.color = border;
            card.Halo.color = WithAlpha(CorruptRed, card.SelectT * (0.3f + 0.18f * Mathf.Sin(time * 6f)));
            card.Body.color = Color.Lerp(card.BodyColor, InfectedBody, card.SelectT * 0.6f);

            if (invLocks[i] != null) invLocks[i].localScale = Vector3.one * Mathf.MoveTowards(invLocks[i].localScale.x, 1f, dt * 2f);

            // Red motes rise off the chosen card.
            if (isSelected && ready)
            {
                card.EmitAccumulator += 26f * particleAmount * dt;
                while (card.EmitAccumulator >= 1f)
                {
                    card.EmitAccumulator -= 1f;
                    EmitGlitchSpark(card, Random.value < 0.3f ? Color.white : CorruptRed, true);
                }
            }
        }

        // Target brackets: snap in from wide, then breathe.
        if (invSelected >= 0)
        {
            bracketClose = Mathf.MoveTowards(bracketClose, 1f, dt / 0.22f);
            float margin = Mathf.Lerp(70f, 20f, Smooth01(bracketClose)) + 5f * Mathf.Sin(time * 7f);
            brackets.offsetMin = new Vector2(-margin, -margin);
            brackets.offsetMax = new Vector2(margin, margin);
            Color bracket = Color.Lerp(CorruptRed, Color.white, bracketClose < 1f ? 0.6f : 0.15f + 0.15f * Mathf.Sin(time * 14f));
            foreach (Image bar in bracketBars) bar.color = bracket;
        }

        AnimateOddsBar(time);
        AnimateInventoryButtons(time, ready);
    }

    // The odds as a split bar: red Exquisite from the left, grey Cursed from the right, a white needle that will not sit still.
    private void AnimateOddsBar(float time)
    {
        if (!oddsTrack.enabled) return;
        PowerUpDefinition card = invDefs[invSelected];
        float chance = corruption.ExquisiteChance != null ? corruption.ExquisiteChance(card) : 0.5f;
        float split = OddsBarWidth * chance;
        oddsFillGood.rectTransform.sizeDelta = new Vector2(split, 22f);
        oddsFillBad.rectTransform.sizeDelta = new Vector2(OddsBarWidth - split, 22f);
        float wobble = (Mathf.PerlinNoise(time * 6f, 0.4f) - 0.5f) * 14f;
        oddsTick.rectTransform.anchoredPosition = new Vector2(-OddsBarWidth * 0.5f + split + wobble, oddsTrack.rectTransform.anchoredPosition.y);
        oddsFillGood.color = Color.Lerp(CorruptRed * 0.8f, CorruptRed, 0.5f + 0.5f * Mathf.Sin(time * 5f));
    }

    private void AnimateInventoryButtons(float time, bool ready)
    {
        bool canGo = ready && invSelected >= 0 && (corruption.Charges == null || corruption.Charges() > 0);
        goButton.Interactable = canGo;
        float throb = 0.5f + 0.5f * Mathf.Sin(time * 7f);
        goBorder.color = canGo ? Color.Lerp(CorruptRed * 0.7f, Color.Lerp(CorruptRed, Color.white, 0.35f), throb) : new Color(0.32f, 0.1f, 0.14f);
        goFill.color = canGo ? new Color(0.2f, 0.01f, 0.05f) : new Color(0.08f, 0.03f, 0.05f);
        goBorder.rectTransform.localScale = Vector3.one * ((goButton.IsPressed ? 0.94f : 1f) * (canGo ? 1f + 0.03f * throb : 1f));
        bool tease = canGo && Mathf.Repeat(time, 1.3f) < 0.08f;
        goLabel.Text = tease ? Scramble("CORRUPT", 0.45f) : "CORRUPT";
        goLabel.color = canGo ? Color.Lerp(new Color(1f, 0.5f, 0.6f), Color.white, tease ? 0.7f : 0.15f) : new Color(0.5f, 0.27f, 0.32f);

        backButton.Interactable = ready;
        backButton.transform.localScale = Vector3.one * (backButton.IsPressed ? 0.94f : 1f);
        backImage.color = ready ? new Color(0.2f, 0.24f, 0.36f) : disabledColor;
        backLabel.color = ready ? new Color(0.8f, 0.86f, 1f) : new Color(0.45f, 0.5f, 0.62f);
    }

    // ---------------------------------------------------------------- slide and background

    private void UpdateSlide(float dt)
    {
        float before = slideProgress;
        slideProgress = Mathf.MoveTowards(slideProgress, slideTarget, dt / SlideTime);
        float e = SmootherStep(slideProgress);
        float height = root.rect.height;
        offerRoot.anchoredPosition = new Vector2(0f, e * height);
        corruptRoot.anchoredPosition = new Vector2(0f, (e - 1f) * height);

        // The seam between the two screens: a hot red line that throws sparks while it moves.
        bool moving = slideProgress > 0f && slideProgress < 1f;
        seam.enabled = moving;
        if (moving)
        {
            float y = height * 0.5f + (e - 1f) * height;
            seam.rectTransform.anchoredPosition = new Vector2(Random.value < 0.25f ? Random.Range(-14f, 14f) : 0f, y);
            seam.color = WithAlpha(Color.Lerp(CorruptRed, Color.white, Random.value * 0.5f), 0.9f);
            seamSparkAccumulator += 70f * particleAmount * dt;
            while (seamSparkAccumulator >= 1f)
            {
                seamSparkAccumulator -= 1f;
                Vector2 from = new(Random.Range(-540f, 540f), y);
                float dir = slideTarget > 0.5f ? 1f : -1f;
                Color color = Random.value < 0.6f ? CorruptRed : Random.value < 0.5f ? Color.white : CorruptCyan;
                Spawn(from, new Vector2(Random.Range(-60f, 60f), dir * Random.Range(80f, 420f)), color, Random.Range(6f, 14f), Random.Range(0.25f, 0.6f), 2.5f);
            }
        }

        // Inventory back to the card screen: nothing left to show.
        if (phase == Phase.Inventory && slideTarget <= 0f && slideProgress <= 0f && before > 0f)
        {
            ClearInventory();
            phase = Phase.Visible;
        }
    }

    // The infected background, kept calm so the cards read: a blood-red field that breathes slowly and dims softly now
    // and then, a faint rolling band, faint drifting scanlines, sparse noise near the edges, a rare soft tear.
    // It only gets restless with the virus of the sequence (level near 1).
    private void AnimateBackground(float dt, float time, float level)
    {
        if (backgroundGroup.alpha <= 0f) return;
        if (backdrop == CorruptionBackdrop.Veins)
        {
            AnimateVeins(dt, time, level);
            return;
        }

        float breathe = 0.86f + 0.07f * Mathf.Sin(time * 1.1f) + 0.05f * (Mathf.PerlinNoise(time * 0.6f, 0.2f) - 0.5f);
        if (bgDropLeft > 0f) bgDropLeft = Mathf.Max(0f, bgDropLeft - dt);
        else if (Random.value < dt * (0.08f + 0.6f * level)) bgDropLeft = BgDipTime;
        float dip = bgDropLeft > 0f ? Mathf.Sin(Mathf.PI * (1f - bgDropLeft / BgDipTime)) : 0f; // eases down and back up
        float brightness = breathe * (1f - (0.12f + 0.2f * level) * dip);
        field.color = new Color(brightness, brightness, brightness, 0.96f);

        float bandY = Mathf.Lerp(1300f, -1300f, Mathf.Repeat(time / Mathf.Lerp(6f, 1.6f, level), 1f));
        bgBand.rectTransform.anchoredPosition = new Vector2(0f, bandY);
        bgBand.color = WithAlpha(CorruptRed, 0.035f + 0.12f * level);

        bgScan.color = WithAlpha(Color.white, 0.1f + 0.25f * level);
        bgScan.uvRect = new Rect(0f, time * 0.15f, 1f, root.rect.height / 8f);

        bgNoiseTimer -= dt;
        if (bgNoiseTimer <= 0f)
        {
            bgNoiseTimer = 1f / Mathf.Lerp(NoiseRate, 12f, level);
            RefreshBackgroundNoise(0.006f + 0.04f * level);
        }
        bgNoise.color = WithAlpha(Color.white, 0.18f + 0.45f * level);

        if (bgTearLeft > 0f)
        {
            bgTearLeft -= dt;
            float fade = Mathf.Clamp01(bgTearLeft / BgTearTime);
            for (int i = 0; i < bgTears.Length; i++)
                bgTears[i].color = WithAlpha(i == 0 ? CorruptRed : CorruptCyan, (0.14f + 0.2f * level) * fade);
            if (bgTearLeft <= 0f) foreach (Image tear in bgTears) tear.enabled = false;
        }
        else
        {
            bgTearTimer -= dt;
            if (bgTearTimer <= 0f)
            {
                bgTearTimer = Random.Range(3f, 7f) * Mathf.Lerp(1f, 0.15f, level);
                bgTearLeft = BgTearTime;
                float y = Random.Range(-0.45f, 0.45f) * root.rect.height;
                for (int i = 0; i < bgTears.Length; i++)
                {
                    Image tear = bgTears[i];
                    tear.enabled = true;
                    tear.rectTransform.anchoredPosition = new Vector2(Random.Range(-40f, 40f), y + i * Random.Range(4f, 10f));
                    tear.rectTransform.sizeDelta = new Vector2(1200f, Random.Range(3f, 8f));
                }
            }
        }
    }

    // The CORRUPT title: red with a cyan / magenta split that jitters, now and then a skewed, scrambled jolt.
    private void AnimateCorruptTitle(float dt, float time)
    {
        float jitter = 3f + 3f * infection;
        corruptTitleCyan.effectDistance = new Vector2(-jitter + Random.Range(-1f, 1f), Random.Range(-1.5f, 1.5f));
        corruptTitleMagenta.effectDistance = new Vector2(jitter + Random.Range(-1f, 1f), Random.Range(-1.5f, 1.5f));

        if (titleGlitchLeft > 0f)
        {
            titleGlitchLeft -= dt;
            if (titleGlitchLeft <= 0f)
            {
                corruptTitle.rectTransform.localScale = Vector3.one;
                corruptTitle.rectTransform.localRotation = Quaternion.identity;
                corruptTitle.Text = "CORRUPT";
            }
            return;
        }

        titleGlitchTimer -= dt;
        corruptTitle.color = Color.Lerp(CorruptRed, Color.white, 0.12f + 0.08f * Mathf.Sin(time * 9f));
        if (titleGlitchTimer > 0f) return;
        titleGlitchTimer = Random.Range(0.9f, 2.4f);
        titleGlitchLeft = Random.Range(0.07f, 0.13f);
        corruptTitle.rectTransform.localScale = new Vector3(Random.Range(1.05f, 1.2f), Random.Range(0.85f, 1f), 1f);
        corruptTitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-4f, 4f));
        corruptTitle.Text = Scramble("CORRUPT", 0.35f);
        corruptTitle.color = Random.value < 0.5f ? Color.white : CorruptCyan;
    }

    private void RefreshBackgroundNoise(float density)
    {
        int w = bgNoiseTexture.width, h = bgNoiseTexture.height;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            // Thicker toward the edges so the cards in the middle stay readable.
            float edge = Mathf.Max(Mathf.Abs(x / (w - 1f) - 0.5f), Mathf.Abs(y / (h - 1f) - 0.5f)) * 2f;
            float local = density * 3f * Mathf.Max(0f, edge - 0.55f) / 0.45f; // none in the middle third of the screen
            float roll = Random.value;
            bgNoisePixels[y * w + x] = roll < local * 0.2f ? new Color32(255, 255, 255, 70)
                : roll < local ? new Color32(255, 30, 60, 80)
                : new Color32(0, 0, 0, 0);
        }
        bgNoiseTexture.SetPixels32(bgNoisePixels);
        bgNoiseTexture.Apply(false);
    }

    // ---------------------------------------------------------------- per frame

    private void AnimateCorruption(float dt, float time)
    {
        UpdateSlide(dt);
        if (phase == Phase.Visible) return; // the slide just brought the card screen back (BACK)

        // The card screen keeps living while it slides.
        if (slideProgress < 1f && phase != Phase.Corrupting && phase != Phase.Revealed)
        {
            for (int i = 0; i < cards.Count; i++) AnimateCard(cards[i], i, dt, time);
            AnimateButtons(time);
        }

        float targetInfection = phase == Phase.Corrupting ? 0.5f + 0.5f * intensity
            : invSelected >= 0 ? 0.5f : 0.3f;
        infection = Mathf.MoveTowards(infection, targetInfection, dt * 0.8f);
        AnimateBackground(dt, time, infection);
        if (!collapsed) AnimateCorruptTitle(dt, time);

        if (phase == Phase.Inventory)
        {
            SetDim(dimAmount);
            AnimateInventory(dt, time);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 2.2f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha * flashAlpha);
            UpdateParticles(dt);
            return;
        }

        if (phase == Phase.Returning)
        {
            AnimateReturn(dt, time);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 2.2f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha * flashAlpha);
            UpdateParticles(dt);
            return;
        }

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
        stageRoot.anchoredPosition = shakeOffset;
        particlesRoot.anchoredPosition = shakeOffset;

        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha * flashAlpha);
        if (corruptCard != null) particleAttractor = corruptCard.Rect.anchoredPosition;
        UpdateParticles(dt);
        AnimateConfirmReveal(dt, time);
    }

    // ---------------------------------------------------------------- start

    private void StartCorruption()
    {
        if (phase != Phase.Inventory || invSelected < 0 || corruption?.Corrupt == null) return;
        PowerUpDefinition source = invDefs[invSelected];
        if (invLocked[invSelected] || (corruption.Charges != null && corruption.Charges() <= 0))
        {
            SoundManager.Play(Sfx.Denied);
            return;
        }

        corruptResult = corruption.Corrupt(source);
        if (corruptResult.Outcome == null) return; // both pools used up (CanOpen should have said so)

        LiftInventoryToStage(invCards[invSelected]);
        corruptActive = true;
        corruptCard = seqCards[corruptIndex];
        corruptFrom = corruptCard.Rect.anchoredPosition;
        corruptFromScale = corruptCard.Rect.localScale.x;
        corruptTime = 0f;
        shattered = new bool[seqCards.Count];
        glitchStarted = collapsed = revealed = confirmShown = impactDone = false;
        intensity = 0f;
        absorbed = 0;
        punch = 0f;
        string name = source.DisplayName.ToUpperInvariant();
        capText = corruptResult.SourceRemoved
            ? $"{name} IS REMOVED (1 > 0)"
            : $"LOSE 1 LEVEL OF {name} ({corruptResult.LevelBefore} > {corruptResult.LevelAfter})";
        capText += "\nTHEN BACK TO YOUR LEVEL-UP PICK";

        phase = Phase.Corrupting;
        brackets.gameObject.SetActive(false);

        SoundManager.Play(Sfx.CorruptStart);
        Flash(CorruptRed, 0.55f);
        shake = 16f;
        borderFlash = 1f;
        corruptCard.transform.SetAsLastSibling();
        MakeCopies(corruptCard);
        Burst(corruptCard, 24, 1.2f);
    }

    // The cards visible in the scrolling list move to the stage, where the sequence plays (cards scrolled out simply go).
    // They shatter in order of distance from the chosen card.
    private void LiftInventoryToStage(CardView chosen)
    {
        seqCards.Clear();
        seqHome.Clear();
        var corners = new Vector3[4];
        viewport.GetWorldCorners(corners);
        float bottom = corners[0].y, top = corners[1].y;

        foreach (CardView card in invCards)
        {
            float y = card.Rect.position.y;
            if (card != chosen && (y < bottom || y > top))
            {
                Destroy(card.gameObject);
                continue;
            }
            Vector3 world = card.Rect.position;
            card.Rect.SetParent(stageRoot, true);
            card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            card.Rect.position = world;
            card.Rect.localRotation = Quaternion.identity;
            card.Clicked = null;
            Transform lockPlate = card.Rect.Find("Lock");
            if (lockPlate != null) lockPlate.gameObject.SetActive(false);
            seqCards.Add(card);
            seqHome.Add(card.Rect.anchoredPosition);
        }
        invCards.Clear();
        invDefs.Clear();
        invLocked.Clear();
        invLocks.Clear();
        invSelected = -1;
        brackets.SetParent(content, false);

        corruptIndex = seqCards.IndexOf(chosen);
        Vector2 from = seqHome[corruptIndex];
        var order = new List<int>();
        for (int i = 0; i < seqCards.Count; i++) if (i != corruptIndex) order.Add(i);
        order.Sort((a, b) => (seqHome[a] - from).sqrMagnitude.CompareTo((seqHome[b] - from).sqrMagnitude));
        shatterAt = new float[seqCards.Count];
        for (int rank = 0; rank < order.Count; rank++) shatterAt[order[rank]] = CxShatterStart + rank * CxShatterStagger;
    }

    // Phases 1 and 2: infection, shatter, glide to the center, then the virus glitch building up.
    private void AnimateInfection(float dt, float time, float t)
    {
        CardView card = corruptCard;

        // The dim bleeds from black to blood red, the inventory text fades, the background thins so the virus reads.
        float redK = Smooth01(t / 0.6f);
        dimTint = Color.Lerp(Color.black, BloodDim, redK);
        SetDim(Mathf.Lerp(dimAmount, 0.93f, redK));
        inventoryGroup.alpha = 1f - Smooth01(t / 0.2f);
        backgroundGroup.alpha = Mathf.Lerp(1f, 0.55f, intensity);

        // The other cards flicker red, then burst one after the other, nearest first.
        int shatterOrder = 0;
        for (int i = 0; i < seqCards.Count; i++)
        {
            if (i == corruptIndex || shattered[i]) continue;
            if (t >= shatterAt[i])
            {
                shattered[i] = true;
                Shatter(seqCards[i]);
                SoundManager.Play(Sfx.BrickBlast, 1.5f + 0.12f * shatterOrder++, 0.5f);
                continue;
            }
            bool on = Mathf.Repeat(time * 24f, 1f) < 0.5f;
            seqCards[i].Border.color = on ? CorruptRed : Color.white;
            seqCards[i].Rect.anchoredPosition = seqHome[i] + Random.insideUnitCircle * 5f;
        }

        // Glide to the center, a bit bigger, a slight unsteady tilt.
        float moveK = Smooth01((t - CxMoveStart) / CxMoveTime);
        moveK = moveK * moveK * (3f - 2f * moveK); // smootherstep feel
        Vector2 pos = Vector2.Lerp(corruptFrom, StageCenter, moveK);
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
        if (Random.value < 0.35f) corruptTitle.Text = Scramble("CORRUPTING...", Mathf.Lerp(0.6f, 0.1f, Smooth01(t / 0.5f)) + 0.45f * intensity);
        corruptTitle.color = Color.Lerp(new Color(1f, 0.35f, 0.45f), Color.white, Random.value < 0.15f ? 0.8f : 0f);

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
        backgroundGroup.alpha = 0.55f * (1f - k);
        shake = 4f;

        if (k >= 1f)
        {
            // Hard cut to black: nothing but a fading dot where the card was.
            collapsed = true;
            card.Group.alpha = 0f;
            ClearParticles();
            SetOverlays(0f, 0f, 0f, 0f, 1f);
            backgroundGroup.alpha = 0f;
            dimTint = Color.black;
            SetDim(1f);
            corruptTitle.Text = "";
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

        DestroyCopies();
        ClearStage();
        int stacks = stackLookup != null ? stackLookup(outcome) : 0;
        resultCard = BuildCard(0, outcome, stacks, cardWidth, 0f, stageRoot);
        resultCard.Clicked = null;
        resultCard.Appear = 1f;
        resultCard.SelectT = 1f;
        corruptCard = resultCard;
        MakeCopies(resultCard);

        Vector2 center = StageCenter;
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
        Vector2 center = StageCenter;
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
            revealButton.gameObject.SetActive(true);
            confirmAppear = 0f;
        }
    }

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
        revealButton.Interactable = ready;
        float pulse = ready ? 1f + 0.03f * Mathf.Sin(time * 6f) : 1f;
        float press = revealButton.IsPressed ? 0.94f : 1f;
        revealButton.transform.localScale = Vector3.one * (EaseOutBack(confirmAppear) * pulse * press);
        revealImage.color = corruptResult.IsExquisite ? Color.Lerp(CorruptRed, Color.white, 0.15f) : new Color(0.42f, 0.44f, 0.52f);
        revealLabel.color = corruptResult.IsExquisite ? new Color(0.2f, 0f, 0.04f) : new Color(0.06f, 0.06f, 0.08f);
    }

    // ---------------------------------------------------------------- return to the card screen

    // CONFIRM after the reveal: the outcome joins the run, then everything goes back to the same offer.
    private void ConfirmOutcome()
    {
        if (phase != Phase.Revealed) return;
        corruption.Grant?.Invoke(corruptResult.Outcome);
        Burst(resultCard, 45, 1.6f);
        SoundManager.Play(Sfx.Confirm);
        corruptActive = false;
        phase = Phase.Returning;
        returnTime = 0f;
        returnFrom = resultCard.Rect.anchoredPosition;
        HideCopies();
        foreach (Image block in blocks) block.enabled = false;
        ringA.enabled = ringB.enabled = false;

        // The offer is rebuilt out of sight, so the levels it shows include the level just lost.
        int keep = selected;
        BuildChoices(choices);
        selected = keep;
        foreach (CardView card in cards)
        {
            card.Appear = 1f;
            card.SelectT = card.Index == selected ? 1f : 0f;
        }
        if (selected >= 0 && selected < cards.Count) cards[selected].transform.SetAsLastSibling();
    }

    // The outcome card is sucked up out of the screen, the reveal fades, then the card screen slides back down.
    private void AnimateReturn(float dt, float time)
    {
        returnTime += dt;
        float k = Smooth01(returnTime / 0.4f);
        CardView card = resultCard;
        if (card != null)
        {
            float s = Mathf.Lerp(CorruptScale, 0.15f, k);
            card.Rect.localScale = new Vector3(s * Mathf.Lerp(1f, 0.6f, k), s * Mathf.Lerp(1f, 1.8f, k), 1f);
            card.Rect.anchoredPosition = returnFrom + new Vector2(0f, k * k * 1100f);
            card.Group.alpha = 1f - Smooth01((returnTime - 0.2f) / 0.2f);
            if (returnTime < 0.4f && Random.value < 0.8f)
                Spawn(card.Rect.anchoredPosition + new Vector2(Random.Range(-40f, 40f), 0f), new Vector2(Random.Range(-40f, 40f), Random.Range(-260f, -60f)),
                    Random.value < 0.5f ? card.RarityColor : Color.white, Random.Range(8f, 16f), Random.Range(0.3f, 0.6f), 2f);
        }

        float fade = 1f - Smooth01(returnTime / 0.25f);
        stamp.color = WithAlpha(stamp.color, Mathf.Min(stamp.color.a, fade));
        capNote.color = WithAlpha(capNote.color, fade);
        revealButton.transform.localScale = Vector3.one * Mathf.Max(0f, fade);
        SetOverlays(0f, 0f, 0f, 0f, 1f);
        stageRoot.anchoredPosition = particlesRoot.anchoredPosition = Vector2.zero;

        dimTint = Color.Lerp(dimTint, Color.black, Smooth01(returnTime / 0.4f));
        SetDim(Mathf.Lerp(0.94f, dimAmount, Smooth01(returnTime / 0.5f)));

        if (returnTime >= ReturnSlideDelay && slideTarget > 0f)
        {
            slideTarget = 0f;
            SoundManager.Play(Sfx.CardsAppear, 0.9f, 0.8f);
        }

        // Back home: the same offer, the same rerolls, the pick still to make.
        if (slideTarget <= 0f && slideProgress <= 0f)
        {
            ResetCorruption();
            phase = Phase.Visible;
        }
    }

    // ---------------------------------------------------------------- particles

    // A card bursts into a grid of square shards in its colors, infected with red. After a short flight they are
    // sucked into the corrupted card.
    private void Shatter(CardView card)
    {
        float scale = card.Rect.localScale.x;
        bool small = scale < 0.95f; // inventory cards: fewer shards, up to 9 cards may break at once
        int columns = small ? 4 : 5;
        int rows = small ? 7 : 9;
        Vector2 center = card.Rect.anchoredPosition;
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
            Spawn(center + offset, velocity, color, Random.Range(18f, 30f) * Mathf.Max(0.8f, scale), 2.2f, 2.8f, 0f, Random.Range(2600f, 3600f), Random.Range(0.18f, 0.42f));
        }
        Burst(card, small ? 8 : 12, 1.4f);
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

    // inParticleSpace: the card is not on the stage (inventory), so its position is converted first.
    private void EmitGlitchSpark(CardView card, Color? tint = null, bool inParticleSpace = false)
    {
        float scale = card.Rect.localScale.x;
        Vector2 center = card.Rect.anchoredPosition;
        if (inParticleSpace) center = ParticleSpace(card.Rect, out scale);
        Vector2 half = new Vector2(card.Width, cardHeight) * 0.5f * scale;
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
            RectTransform ghost = CopyCard(source, $"Ghost{i}", stageRoot);
            foreach (Graphic graphic in ghost.GetComponentsInChildren<Graphic>(true))
                graphic.color = WithAlpha(tints[i], graphic.color.a);
            ghost.SetSiblingIndex(source.transform.GetSiblingIndex()); // behind the card
            ghosts.Add(ghost);
            ghostOffsets[i] = Vector2.zero;
        }

        for (int i = 0; i < TearCount; i++)
        {
            RectTransform mask = NewRect($"Tear{i}", stageRoot);
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
        foreach (string part in new[] { "Halo", "Tag", "Brackets", "Lock" })
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

    private void ClearStage()
    {
        foreach (CardView card in seqCards)
            if (card != null) Destroy(card.gameObject);
        seqCards.Clear();
        seqHome.Clear();
        if (resultCard != null) Destroy(resultCard.gameObject);
        resultCard = null;
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
        ring.rectTransform.anchoredPosition = StageCenter;
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

    // The corruption screen (built first, so it is drawn behind the card screen and the sequence).
    private void BuildCorruptionScreen()
    {
        corruptRoot = NewRect("CorruptionScreen", root);
        Stretch(corruptRoot);

        // Background: field, band, scanlines, noise, tears. Never catches taps.
        RectTransform background = NewRect("Background", corruptRoot);
        Stretch(background);
        backgroundGroup = background.gameObject.AddComponent<CanvasGroup>();
        backgroundGroup.blocksRaycasts = false;
        field = NewRawIn("Field", background, BuildFieldTexture());
        field.enabled = true;
        glitchLayer = NewRect("Glitch", background); // the Glitch backdrop (see PowerUpScreen.CorruptionBackdrop.cs)
        Stretch(glitchLayer);
        bgBand = NewImage("Band", glitchLayer, Color.clear, false);
        bgBand.sprite = null;
        bgBand.type = Image.Type.Simple;
        bgBand.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        bgBand.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        bgBand.rectTransform.sizeDelta = new Vector2(0f, 110f);
        bgScan = NewRawIn("Scanlines", glitchLayer, BuildScanlineTexture());
        bgScan.enabled = true;
        bgNoiseTexture = new Texture2D(54, 96, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        bgNoisePixels = new Color32[bgNoiseTexture.width * bgNoiseTexture.height];
        bgNoise = NewRawIn("Noise", glitchLayer, bgNoiseTexture);
        bgNoise.enabled = true;
        for (int i = 0; i < bgTears.Length; i++)
        {
            Image tear = NewImage("Tear", glitchLayer, Color.clear, false);
            tear.sprite = null;
            tear.type = Image.Type.Simple;
            Place(tear.rectTransform, Vector2.zero, Vector2.zero, new Vector2(1200f, 8f));
            tear.enabled = false;
            bgTears[i] = tear;
        }
        BuildVeinsLayer(background);
        ApplyBackdrop();

        // Title, outside the fading inventory group: it turns into CORRUPTING... during the sequence.
        corruptTitle = NewText("Title", corruptRoot, "CORRUPT", 9f, CorruptRed, PixelText.Align.Center);
        PlaceTop(corruptTitle.rectTransform, -140f, new Vector2(1000f, 90f));
        corruptTitleCyan = corruptTitle.gameObject.AddComponent<Shadow>();
        corruptTitleCyan.effectColor = WithAlpha(CorruptCyan, 0.75f);
        corruptTitleMagenta = corruptTitle.gameObject.AddComponent<Shadow>();
        corruptTitleMagenta.effectColor = WithAlpha(CorruptMagenta, 0.75f);

        RectTransform ui = NewRect("Inventory", corruptRoot);
        Stretch(ui);
        inventoryGroup = ui.gameObject.AddComponent<CanvasGroup>();

        PixelText subtitle = NewText("Subtitle", ui, "SACRIFICE ONE LEVEL OF AN UPGRADE", 3f, new Color(0.9f, 0.62f, 0.68f), PixelText.Align.Center);
        PlaceTop(subtitle.rectTransform, -215f, new Vector2(1000f, 30f));
        chargesText = NewText("Charges", ui, "", 4f, CorruptRed, PixelText.Align.Center);
        PlaceTop(chargesText.rectTransform, -262f, new Vector2(1000f, 40f));

        // Scrolling list, same gesture as the pause screen: drag, momentum, a tap only selects when it did not drag.
        Image viewportImage = NewImage("Viewport", ui, Color.clear, true);
        viewportImage.sprite = null;
        viewportImage.type = Image.Type.Simple;
        viewport = viewportImage.rectTransform;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(0f, InvBottom);
        viewport.offsetMax = new Vector2(0f, -InvTop);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        inventoryScroll = viewport.gameObject.AddComponent<ScrollRect>();
        inventoryScroll.viewport = viewport;
        inventoryScroll.content = content;
        inventoryScroll.horizontal = false;
        inventoryScroll.movementType = ScrollRect.MovementType.Elastic;
        inventoryScroll.scrollSensitivity = 40f;

        emptyText = NewText("Empty", viewport, "NOTHING TO CORRUPT", 4f, LockedGrey, PixelText.Align.Center);
        Place(emptyText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(1000f, 60f));

        brackets = NewRect("Brackets", content);
        brackets.anchorMin = Vector2.zero;
        brackets.anchorMax = Vector2.one;
        for (int i = 0; i < 4; i++)
        {
            Vector2 corner = new(i % 2, i / 2);
            for (int j = 0; j < 2; j++)
            {
                Image bar = NewImage("Bracket", brackets, CorruptRed, false);
                bar.sprite = null;
                bar.type = Image.Type.Simple;
                RectTransform r = bar.rectTransform;
                r.anchorMin = r.anchorMax = corner;
                r.pivot = new Vector2(corner.x, corner.y);
                r.sizeDelta = j == 0 ? new Vector2(56f, 12f) : new Vector2(12f, 56f);
                r.anchoredPosition = Vector2.zero;
                bracketBars[i * 2 + j] = bar;
            }
        }
        brackets.gameObject.SetActive(false);

        // Bottom bar: odds as a split bar, the cost, BACK / CORRUPT.
        Image shade = NewImage("BarShade", ui, new Color(0.07f, 0f, 0.02f, 0.85f), false);
        shade.sprite = null;
        shade.type = Image.Type.Simple;
        PlaceBottom(shade.rectTransform, InvBottom * 0.5f, new Vector2(2000f, InvBottom));
        Image shadeEdge = NewImage("BarEdge", ui, WithAlpha(CorruptRed, 0.55f), false);
        shadeEdge.sprite = null;
        shadeEdge.type = Image.Type.Simple;
        PlaceBottom(shadeEdge.rectTransform, InvBottom, new Vector2(2000f, 4f));

        oddsGood = NewText("OddsGood", ui, "", 4f, CorruptRed, PixelText.Align.Left);
        PlaceBottom(oddsGood.rectTransform, 392f, new Vector2(OddsBarWidth, 36f));
        oddsBad = NewText("OddsBad", ui, "", 4f, RarityStyle.CursedColor, PixelText.Align.Right);
        PlaceBottom(oddsBad.rectTransform, 392f, new Vector2(OddsBarWidth, 36f));
        oddsTrack = BarPiece(ui, "OddsTrack", new Color(0.12f, 0.04f, 0.07f), new Vector2(OddsBarWidth + 12f, 34f), 0.5f);
        oddsFillGood = BarPiece(ui, "OddsExquisite", CorruptRed, new Vector2(0f, 22f), 0f);
        oddsFillBad = BarPiece(ui, "OddsCursed", RarityStyle.CursedColor * 0.8f, new Vector2(0f, 22f), 1f);
        oddsTick = BarPiece(ui, "OddsNeedle", Color.white, new Vector2(6f, 46f), 0.5f);

        costText = NewText("Cost", ui, "", 3f, new Color(0.88f, 0.6f, 0.66f), PixelText.Align.Center);
        costText.Wrap = true;
        PlaceBottom(costText.rectTransform, 270f, new Vector2(980f, 80f));

        BuildButton(ui, "BackButton", "BACK", new Vector2(-195f, 0f), out backButton, out backImage, out backLabel);
        MoveToBottom(backImage.rectTransform, 120f);
        backButton.Clicked += CloseCorruption;

        goBorder = NewImage("CorruptGo", ui, CorruptRed, true);
        Place(goBorder.rectTransform, Vector2.zero, new Vector2(195f, 0f), new Vector2(340f, 120f));
        MoveToBottom(goBorder.rectTransform, 120f);
        goButton = goBorder.gameObject.AddComponent<UiButton>();
        goButton.Clicked += StartCorruption;
        goButton.DeniedClick += () => SoundManager.Play(Sfx.Denied);
        goFill = NewImage("Fill", goBorder.rectTransform, Color.black, false);
        Stretch(goFill.rectTransform);
        goFill.rectTransform.offsetMin = new Vector2(7f, 7f);
        goFill.rectTransform.offsetMax = new Vector2(-7f, -7f);
        goLabel = NewText("Label", goBorder.rectTransform, "CORRUPT", 5f, CorruptRed, PixelText.Align.Center);
        Stretch(goLabel.rectTransform);

        corruptRoot.anchoredPosition = new Vector2(0f, -4000f); // off screen until opened
    }

    // A piece of the odds bar, anchored to the bottom; pivotX 0 grows from the left end, 1 from the right end.
    private Image BarPiece(RectTransform parent, string name, Color color, Vector2 size, float pivotX)
    {
        Image image = NewImage(name, parent, color, false);
        image.sprite = null;
        image.type = Image.Type.Simple;
        RectTransform r = image.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(pivotX, 0.5f);
        r.sizeDelta = size;
        float x = pivotX <= 0f ? -OddsBarWidth * 0.5f : pivotX >= 1f ? OddsBarWidth * 0.5f : 0f;
        r.anchoredPosition = new Vector2(x, 345f);
        image.enabled = false;
        return image;
    }

    // The CORRUPT button on the card screen, the stage and the sequence overlays.
    private void BuildCorruptionUi(float buttonY)
    {
        // CORRUPT button: a blood-red frame around a dark fill, under Reroll / Confirm.
        RectTransform container = NewRect("Corruption", offerRoot);
        Stretch(container);
        corruptGroup = container.gameObject.AddComponent<CanvasGroup>();

        corruptBorder = NewImage("CorruptButton", container, CorruptRed, true);
        Place(corruptBorder.rectTransform, Vector2.zero, new Vector2(0f, buttonY - 150f), new Vector2(440f, 110f));
        corruptButton = corruptBorder.gameObject.AddComponent<UiButton>();
        corruptButton.Clicked += OpenCorruption;
        corruptButton.DeniedClick += () => SoundManager.Play(Sfx.Denied);
        corruptFill = NewImage("Fill", corruptBorder.rectTransform, Color.black, false);
        Stretch(corruptFill.rectTransform);
        corruptFill.rectTransform.offsetMin = new Vector2(7f, 7f);
        corruptFill.rectTransform.offsetMax = new Vector2(-7f, -7f);
        RectTransform labelHolder = NewRect("LabelHolder", corruptBorder.rectTransform);
        Stretch(labelHolder);
        corruptLabel = NewText("Label", labelHolder, "CORRUPT", 5f, CorruptRed, PixelText.Align.Center);
        Stretch(corruptLabel.rectTransform);

        corruptHint = NewText("Hint", container, "", 3f, new Color(0.85f, 0.5f, 0.56f), PixelText.Align.Center);
        corruptHint.Wrap = true;
        Place(corruptHint.rectTransform, Vector2.zero, new Vector2(0f, buttonY - 232f), new Vector2(960f, 60f));
        container.gameObject.SetActive(false);

        // Stage: where the sequence plays, above both screens and never sliding.
        stageRoot = NewRect("Stage", root);
        Stretch(stageRoot);

        seam = NewImage("Seam", root, CorruptRed, false);
        seam.sprite = null;
        seam.type = Image.Type.Simple;
        Place(seam.rectTransform, Vector2.zero, Vector2.zero, new Vector2(1400f, 8f));
        seam.enabled = false;

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
            Image block = NewImage("Block", stageRoot, Color.clear, false);
            block.sprite = null;
            block.type = Image.Type.Simple;
            block.enabled = false;
            blocks.Add(block);
        }

        ringA = NewRing("RingA");
        ringB = NewRing("RingB");
        // Behind the stage, so a shockwave never crosses the card's text.
        ringA.transform.SetSiblingIndex(stageRoot.GetSiblingIndex());
        ringB.transform.SetSiblingIndex(stageRoot.GetSiblingIndex());

        stamp = NewText("Stamp", root, "", 10f, CorruptRed, PixelText.Align.Center);
        Place(stamp.rectTransform, Vector2.zero, new Vector2(0f, StampY), new Vector2(1060f, 90f));
        stampCyan = stamp.gameObject.AddComponent<Shadow>();
        stampCyan.effectColor = WithAlpha(CorruptCyan, 0.8f);
        stampMagenta = stamp.gameObject.AddComponent<Shadow>();
        stampMagenta.effectColor = WithAlpha(CorruptMagenta, 0.8f);
        stamp.enabled = false;

        capNote = NewText("CapNote", root, "", 3f, Color.white, PixelText.Align.Center);
        Place(capNote.rectTransform, Vector2.zero, new Vector2(0f, cardCenterY + 10f - (cardHeight * 0.5f) * CorruptScale - 62f), new Vector2(960f, 70f)); // the level lost, then a reminder of the pick
        capNote.enabled = false;

        BuildButton(root, "RevealConfirm", "CONFIRM", new Vector2(0f, buttonY - 150f), out revealButton, out revealImage, out revealLabel);
        revealButton.Clicked += ConfirmOutcome;
        revealButton.gameObject.SetActive(false);

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

    private RawImage NewRaw(string name, Texture texture) => NewRawIn(name, root, texture);

    private static RawImage NewRawIn(string name, RectTransform parent, Texture texture)
    {
        RectTransform rect = NewRect(name, parent);
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

    // Anchored to the top / bottom center of the screen (the screen height changes with the phone).
    private static void PlaceTop(RectTransform rect, float y, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = size;
    }

    private static void PlaceBottom(RectTransform rect, float y, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = size;
    }

    private static void MoveToBottom(RectTransform rect, float y)
    {
        float x = rect.anchoredPosition.x;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, y);
    }

    private void ResetCorruption()
    {
        if (root == null) return;
        corruptActive = false;
        revealed = collapsed = confirmShown = impactDone = false;
        corruptCard = null;
        DestroyCopies();
        ClearStage();
        ClearInventory();
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
        corruptTitle.Text = "CORRUPT";
        corruptTitle.rectTransform.localScale = Vector3.one;
        corruptTitle.rectTransform.localRotation = Quaternion.identity;
        stageRoot.anchoredPosition = Vector2.zero;
        particlesRoot.anchoredPosition = Vector2.zero;
        shake = punch = borderFlash = 0f;
        microGlitchTimer = 0.9f;
        microGlitchLeft = 0f;
        frameDropped = false;
        jumpOffset = Vector2.zero;
        slideProgress = slideTarget = 0f;
        offerRoot.anchoredPosition = Vector2.zero;
        corruptRoot.anchoredPosition = new Vector2(0f, -4000f);
        seam.enabled = false;
        foreach (Image tear in bgTears) tear.enabled = false;
        backgroundGroup.alpha = 1f;
        inventoryGroup.alpha = 1f;
        revealButton.gameObject.SetActive(false);
        revealButton.transform.localScale = Vector3.one;
        hintText = null;
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

    // A soft blood-red radial glow: warm in the middle, near black at the edges (bilinear, so it stays smooth on any screen).
    private static Texture2D BuildFieldTexture()
    {
        const int w = 54, h = 96;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color hot = new(0.2f, 0.015f, 0.05f), cold = new(0.025f, 0f, 0.012f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = (x / (w - 1f) - 0.5f) * 1.1f, dy = (y / (h - 1f) - 0.52f) * 1.25f;
            float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 1.5f);
            float k = Smooth01(1f - d);
            texture.SetPixel(x, y, Color.Lerp(cold, hot, k * k));
        }
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

    private static float SmootherStep(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * x * (x * (x * 6f - 15f) + 10f);
    }
}
