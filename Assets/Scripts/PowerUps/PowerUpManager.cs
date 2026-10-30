using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Offers power-ups on level-up. Pauses the game, rolls the choices by rarity weight, shows the card screen,
/// applies the chosen power-up, then resumes. Several level-ups at once are handled one after the other.
/// </summary>
public class PowerUpManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ExperienceSystem experience;
    [SerializeField] private PowerUpScreen screen;
    [SerializeField] private PaddleController paddle;
    [SerializeField] private BrickGrid grid;

    [Header("Pool")]
    [SerializeField] private List<PowerUpDefinition> powerUps = new();

    [Header("Choices")]
    [Tooltip("How many cards are offered on a level-up.")]
    [SerializeField, Min(1)] private int baseChoiceCount = 3;
    [Tooltip("How many times the cards can be rerolled on each level-up.")]
    [SerializeField, Min(0)] private int baseRerolls = 1;
    [Tooltip("Relative chance of each rarity: Common, Uncommon, Rare, Epic, Legendary.")]
    [SerializeField] private float[] rarityWeights = { 55f, 25f, 11f, 6f, 3f };
    [Tooltip("Base ball damage every level-up gives for free, before any card.")]
    [SerializeField, Min(0)] private int passiveDamagePerLevel = 1;
    [Tooltip("Seconds the game takes to slow down to a full stop when a level is gained.")]
    [SerializeField, Min(0f)] private float slowDownTime = 0.8f;
    [Tooltip("Seconds of stillness between the full stop and the cards opening.")]
    [SerializeField, Min(0f)] private float openDelay = 0.3f;
    [Tooltip("Seconds the game takes to ease from the full stop back to normal speed after the last card.")]
    [SerializeField, Min(0f)] private float resumeTime = 1f;

    [Header("Corruption")]
    [Tooltip("Cards a corruption can turn into when it goes well. Their rarity sets the tier: Exquisite (Minor), Exquisite+ (Major), Exquisite++ (Grand). Never rolled on a level-up.")]
    [SerializeField] private List<PowerUpDefinition> exquisitePool = new();
    [Tooltip("Cards a corruption can turn into when it goes badly. Their rarity sets the tier: Cursed, Cursed-, Cursed--. Never rolled on a level-up.")]
    [SerializeField] private List<PowerUpDefinition> cursedPool = new();
    [Tooltip("Chance of an Exquisite outcome instead of a Cursed one, by the rarity of the corrupted card: Common, Uncommon, Rare, Epic, Legendary. The tier of the outcome follows it too: Common and Uncommon Minor, Rare and Epic Major, Legendary Grand.")]
    [SerializeField] private float[] exquisiteChances = { 0.30f, 0.40f, 0.55f, 0.70f, 0.80f };

    /// <summary>Extra cards on top of the base count (meta-progression, perks...).</summary>
    public int BonusChoices { get; set; }
    public int ChoiceCount => Mathf.Max(1, baseChoiceCount + BonusChoices);
    /// <summary>Extra rerolls per level-up on top of the base count (meta-progression, perks...).</summary>
    public int BonusRerolls { get; set; }
    public int RerollCount => Mathf.Max(0, baseRerolls + BonusRerolls);
    /// <summary>Base ball damage gained on each level-up (cards multiply the total instead, see <see cref="DamageMultiplierEffect"/>).</summary>
    public int DamagePerLevel => passiveDamagePerLevel;
    public bool IsChoosing { get; private set; }

    /// <summary>Raised after a power-up was applied: the power-up and how many times it has now been taken.</summary>
    public event Action<PowerUpDefinition, int> PowerUpChosen;

    /// <summary>Raised when cards are shown: the cards, and whether this is a reroll of the previous ones (run profiler).</summary>
    public event Action<IReadOnlyList<PowerUpDefinition>, bool> Offered;

    /// <summary>Raised when a card is corrupted (before the outcome is confirmed and applied).</summary>
    public event Action<CorruptionResult> Corrupted;

    private readonly Dictionary<PowerUpDefinition, int> stacks = new();
    private readonly Dictionary<PowerUpDefinition, int> capPenalty = new(); // levels a card lost to corruptions this run
    private CorruptionHooks corruptionHooks;
    private CorruptionDowry pendingDowry;  // paid when the Cursed outcome of the current corruption is confirmed
    private int nextRerollBonus;           // dowry: extra rerolls on the next level-up
    private int guaranteeRank = -1;        // dowry: the next offer holds a card of at least this rarity (-1 = none)
    private int pending;
    private readonly Queue<int> pendingDamage = new(); // damage gain of each level-up still waiting for its pick
    private float savedTimeScale = 1f;
    private bool keepSavedTimeScale;

    private void OnEnable()
    {
        PowerUpStats.Reset(); // a new run starts with no card bonuses
        pendingDowry = CorruptionDowry.None;
        nextRerollBonus = 0;
        guaranteeRank = -1;
        if (experience != null) experience.LevelUp += HandleLevelUp;
    }

    private void OnDisable()
    {
        if (experience != null) experience.LevelUp -= HandleLevelUp;
        if (IsChoosing)
        {
            Time.timeScale = savedTimeScale;
            IsChoosing = false;
        }
    }

    public int GetStacks(PowerUpDefinition definition)
    {
        return stacks.TryGetValue(definition, out int count) ? count : 0;
    }

    /// <summary>Every power-up picked this run, with how many times it was taken.</summary>
    public IReadOnlyDictionary<PowerUpDefinition, int> Owned => stacks;

    /// <summary>A card's max level this run: its cap minus the levels corruptions took from it. 0 = unlimited.</summary>
    public int GetMaxStacks(PowerUpDefinition definition)
    {
        if (definition.MaxStacks <= 0) return 0;
        return Mathf.Max(0, definition.MaxStacks - (capPenalty.TryGetValue(definition, out int lost) ? lost : 0));
    }

    /// <summary>True when a capped card has reached its (possibly lowered) cap and can no longer be offered.</summary>
    public bool IsMaxed(PowerUpDefinition definition) =>
        definition.MaxStacks > 0 && GetStacks(definition) >= GetMaxStacks(definition);

    /// <summary>
    /// A card may be corrupted with a charge in hand when it has a cap (even a cap of 1) and an outcome is left
    /// to give. Unlimited cards and the corruption cards themselves cannot.
    /// </summary>
    public bool CanCorrupt(PowerUpDefinition definition) =>
        definition != null && PowerUpStats.CorruptionCharges > 0 && definition.MaxStacks > 0 && !IsMaxed(definition)
        && !RarityStyle.IsCorruption(definition.Rarity) && !IsBlocked(definition)
        && (ResolveTier(exquisitePool, RarityStyle.SourceTier(definition.Rarity)) >= 0
            || ResolveTier(cursedPool, RarityStyle.SourceTier(definition.Rarity)) >= 0);

    /// <summary>
    /// What corrupting this card would do, as the roll will use it: the chance of an Exquisite outcome (by the card's
    /// rarity), the two outcome rarities (by the card's tier) and what a Cursed outcome pays. If a pool has nothing
    /// open at the card's tier the nearest tier stands in, and if one side is used up the other has 100%.
    /// </summary>
    public CorruptionOdds GetOdds(PowerUpDefinition source)
    {
        int tier = RarityStyle.SourceTier(source.Rarity);
        int exTier = ResolveTier(exquisitePool, tier);
        int cuTier = ResolveTier(cursedPool, tier);
        float chance = exTier < 0 ? 0f : cuTier < 0 ? 1f : BaseExquisiteChance(source.Rarity);
        if (exTier < 0) exTier = tier;
        if (cuTier < 0) cuTier = tier;
        return new CorruptionOdds(chance, RarityStyle.ExquisiteOfTier(exTier), RarityStyle.CursedOfTier(cuTier), CorruptionDowryInfo.ForTier(cuTier));
    }

    private float BaseExquisiteChance(PowerUpRarity rarity)
    {
        int index = (int)rarity;
        return exquisiteChances != null && index >= 0 && index < exquisiteChances.Length ? Mathf.Clamp01(exquisiteChances[index]) : 0.5f;
    }

    /// <summary>
    /// Spends a charge, takes one level off the card's cap for the rest of the run and rolls the outcome: an
    /// Exquisite card (chance by the corrupted card's rarity, see <see cref="GetOdds"/>) or a Cursed one, both of the
    /// tier that rarity gives. The outcome is applied when confirmed; a Cursed one pays its dowry then.
    /// </summary>
    public CorruptionResult Corrupt(PowerUpDefinition source)
    {
        CorruptionOdds odds = GetOdds(source);
        PowerUpStats.CorruptionCharges = Mathf.Max(0, PowerUpStats.CorruptionCharges - 1);
        int oldMax = GetMaxStacks(source);
        capPenalty[source] = (capPenalty.TryGetValue(source, out int lost) ? lost : 0) + 1;
        int newMax = GetMaxStacks(source);

        bool exquisite = UnityEngine.Random.value < odds.ExquisiteChance;
        PowerUpDefinition outcome = exquisite
            ? PickOutcome(exquisitePool, RarityStyle.OutcomeTier(odds.ExquisiteRarity))
            : PickOutcome(cursedPool, RarityStyle.OutcomeTier(odds.CursedRarity));

        CorruptionDowry dowry = !exquisite && outcome != null ? odds.Dowry : CorruptionDowry.None;
        pendingDowry = dowry;
        var result = new CorruptionResult(source, outcome, oldMax, newMax, IsMaxed(source), odds.ExquisiteChance, dowry);
        Corrupted?.Invoke(result);
        return result;
    }

    // Brittle Pact (Cursed--) takes the Shield and Phoenix cards out of the run.
    private static bool IsBlocked(PowerUpDefinition definition) =>
        PowerUpStats.BrittlePact && definition.Effect != null && (definition.Effect is ShieldPlusEffect || definition.Effect is PhoenixBallEffect);

    private bool IsOpenOutcome(PowerUpDefinition definition) => definition != null && !IsMaxed(definition) && !IsBlocked(definition);

    // The tier closest to the wanted one (the wanted one, then lower, then higher) that still has an open card in the pool; -1 if none.
    private int ResolveTier(List<PowerUpDefinition> pool, int tier)
    {
        for (int distance = 0; distance <= 2; distance++)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                if (distance == 0 && sign == 1) continue;
                int t = tier + sign * distance;
                if (t < 0 || t > 2) continue;
                if (pool.Exists(p => IsOpenOutcome(p) && RarityStyle.OutcomeTier(p.Rarity) == t)) return t;
            }
        }
        return -1;
    }

    private PowerUpDefinition PickOutcome(List<PowerUpDefinition> pool, int tier)
    {
        var open = pool.FindAll(p => IsOpenOutcome(p) && RarityStyle.OutcomeTier(p.Rarity) == tier);
        return open.Count > 0 ? open[UnityEngine.Random.Range(0, open.Count)] : null;
    }

    private void GrantDowry(CorruptionDowry dowry)
    {
        switch (dowry)
        {
            case CorruptionDowry.Reroll:
                nextRerollBonus++;
                break;
            case CorruptionDowry.RareOffer:
                guaranteeRank = Mathf.Max(guaranteeRank, (int)PowerUpRarity.Rare);
                break;
            case CorruptionDowry.LegendaryOffer:
                guaranteeRank = Mathf.Max(guaranteeRank, (int)PowerUpRarity.Legendary);
                break;
        }
    }

    [ContextMenu("Debug: Give Corruption Charge")]
    private void DebugGiveCharge() => PowerUpStats.CorruptionCharges++;

    private void HandleLevelUp(int newLevel)
    {
        // The gain is fixed now, so a card picked in this level-up only counts from the next one, but it is
        // applied after the pick: the bricks recolor with the new damage once the card screen is gone.
        pendingDamage.Enqueue(DamagePerLevel);
        pending++;
        if (!IsChoosing) StartCoroutine(ChoiceLoop());
    }

    [ContextMenu("Debug: Trigger Level Up Choice")]
    private void DebugTrigger() => HandleLevelUp(0);

    private IEnumerator ChoiceLoop()
    {
        IsChoosing = true;
        if (!keepSavedTimeScale) savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        keepSavedTimeScale = false;

        // Ease into slow motion, then a full stop. The HUD banner and the choice screen run on unscaled time.
        float elapsed = 0f;
        float from = Time.timeScale > 0f ? Time.timeScale : savedTimeScale;
        while (elapsed < slowDownTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / slowDownTime);
            Time.timeScale = Mathf.Lerp(from, 0f, k * k * (3f - 2f * k)); // smoothstep
            yield return null;
        }
        Time.timeScale = 0f;

        while (pending > 0)
        {
            yield return new WaitForSecondsRealtime(openDelay);

            pending--;
            // Dowries of earlier Cursed outcomes: this offer holds a card of at least that rarity / gets extra rerolls.
            int guarantee = guaranteeRank;
            guaranteeRank = -1;
            int rerolls = RerollCount + nextRerollBonus;
            nextRerollBonus = 0;
            List<PowerUpDefinition> choices = Roll(ChoiceCount, guarantee);
            if (choices.Count > 0)
            {
                bool closed = false;
                Offered?.Invoke(choices, false);
                corruptionHooks ??= new CorruptionHooks
                {
                    Charges = () => PowerUpStats.CorruptionCharges,
                    MaxStacks = GetMaxStacks,
                    CanCorrupt = CanCorrupt,
                    Odds = GetOdds,
                    Corrupt = Corrupt
                };
                screen.Show(choices, GetStacks, Apply, () => closed = true, rerolls, () =>
                {
                    List<PowerUpDefinition> rerolled = Roll(ChoiceCount, guarantee);
                    Offered?.Invoke(rerolled, true);
                    return rerolled;
                }, corruptionHooks);
                while (!closed) yield return null;
            }

            ApplyLevelDamage(pendingDamage.Count > 0 ? pendingDamage.Dequeue() : 0);

            // Toll (Cursed--): every level-up pushes the wall closer, once the game runs again.
            if (PowerUpStats.TollRows > 0 && grid != null) grid.QueueExtraSteps(PowerUpStats.TollRows);
        }

        // One extra frame so the confirming tap is not read as a ball launch.
        yield return null;

        // Ease back to normal speed instead of snapping to it. The paddle keeps working during the ease.
        // Stops early if another level-up arrives or if something else took the time scale (Game Over).
        float resumed = 0f;
        float lastSet = Time.timeScale;
        while (resumed < resumeTime && pending == 0)
        {
            if (!Mathf.Approximately(Time.timeScale, lastSet))
            {
                IsChoosing = false; // another system owns the time now
                yield break;
            }
            resumed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(resumed / resumeTime);
            lastSet = Mathf.Lerp(0f, savedTimeScale, k * k * (3f - 2f * k)); // smoothstep
            Time.timeScale = lastSet;
            yield return null;
        }

        if (pending > 0)
        {
            // A new level-up came in during the ease: run the whole sequence again from the current speed.
            IsChoosing = false;
            keepSavedTimeScale = true; // the current speed is mid-ease, the real one is already saved
            StartCoroutine(ChoiceLoop());
            yield break;
        }

        Time.timeScale = savedTimeScale;
        IsChoosing = false;
    }

    private static void ApplyLevelDamage(int gain)
    {
        if (gain <= 0) return;
        foreach (BallController ball in FindObjectsByType<BallController>()) ball.AddDamage(gain);
    }

    private void Apply(PowerUpDefinition definition)
    {
        stacks[definition] = GetStacks(definition) + 1;

        // A Cursed outcome pays its dowry as it is confirmed.
        if (pendingDowry != CorruptionDowry.None && RarityStyle.IsCursed(definition.Rarity))
        {
            GrantDowry(pendingDowry);
            pendingDowry = CorruptionDowry.None;
        }

        if (definition.Effect != null)
        {
            var context = new PowerUpContext
            {
                Manager = this,
                Paddle = paddle,
                Grid = grid,
                Experience = experience,
                Balls = FindObjectsByType<BallController>()
            };
            definition.Effect.Apply(context);
        }

        PowerUpChosen?.Invoke(definition, stacks[definition]);
    }

    // Picks a rarity by weight (among rarities that still have something to offer), then a random power-up of it.
    // minRank >= 0 (a Cursed dowry): the offer holds at least one card of that rarity or better when the pool has one.
    private List<PowerUpDefinition> Roll(int count, int minRank = -1)
    {
        var pool = new List<PowerUpDefinition>();
        foreach (PowerUpDefinition definition in powerUps)
        {
            if (definition == null || RarityStyle.IsCorruption(definition.Rarity)) continue;
            if (IsMaxed(definition) || IsBlocked(definition)) continue;
            pool.Add(definition);
        }

        var result = new List<PowerUpDefinition>();
        while (result.Count < count && pool.Count > 0)
        {
            float total = 0f;
            for (int r = 0; r < rarityWeights.Length; r++)
                if (HasRarity(pool, (PowerUpRarity)r)) total += Mathf.Max(0f, rarityWeights[r]);

            PowerUpRarity chosenRarity = pool[0].Rarity;
            if (total > 0f)
            {
                float roll = UnityEngine.Random.value * total;
                for (int r = 0; r < rarityWeights.Length; r++)
                {
                    if (!HasRarity(pool, (PowerUpRarity)r)) continue;
                    roll -= Mathf.Max(0f, rarityWeights[r]);
                    chosenRarity = (PowerUpRarity)r;
                    if (roll <= 0f) break;
                }
            }

            var candidates = pool.FindAll(p => p.Rarity == chosenRarity);
            PowerUpDefinition pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            result.Add(pick);
            pool.Remove(pick);
        }

        // Soft rescue for casual players: an offer made only of utility cards swaps one for a random damage card.
        if (result.Count > 1 && !result.Exists(IsOffensive))
        {
            var offensive = pool.FindAll(IsOffensive);
            if (offensive.Count > 0)
                result[UnityEngine.Random.Range(0, result.Count)] = offensive[UnityEngine.Random.Range(0, offensive.Count)];
        }

        if (minRank >= 0 && result.Count > 0 && !result.Exists(p => (int)p.Rarity >= minRank))
            GuaranteeRarity(result, minRank);
        return result;
    }

    // Swaps one card of the offer for a random open card of at least the given rarity, without losing the offer's only damage card.
    private void GuaranteeRarity(List<PowerUpDefinition> result, int minRank)
    {
        var candidates = new List<PowerUpDefinition>();
        foreach (PowerUpDefinition definition in powerUps)
        {
            if (definition == null || RarityStyle.IsCorruption(definition.Rarity)) continue;
            if (IsMaxed(definition) || IsBlocked(definition) || (int)definition.Rarity < minRank || result.Contains(definition)) continue;
            candidates.Add(definition);
        }
        if (candidates.Count == 0) return;

        PowerUpDefinition pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        int offensiveCount = result.FindAll(IsOffensive).Count;
        var slots = new List<int>();
        for (int i = 0; i < result.Count; i++)
            if (offensiveCount != 1 || IsOffensive(pick) || !IsOffensive(result[i])) slots.Add(i);
        if (slots.Count == 0)
            for (int i = 0; i < result.Count; i++) slots.Add(i);
        result[slots[UnityEngine.Random.Range(0, slots.Count)]] = pick;
    }

    private static bool IsOffensive(PowerUpDefinition definition) => definition.Effect != null && definition.Effect.IsOffensive;

    private static bool HasRarity(List<PowerUpDefinition> pool, PowerUpRarity rarity)
    {
        foreach (PowerUpDefinition p in pool)
            if (p.Rarity == rarity) return true;
        return false;
    }
}
