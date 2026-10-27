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
    [SerializeField] private float[] rarityWeights = { 60f, 25f, 10f, 4f, 1f };
    [Tooltip("Seconds the game takes to slow down to a full stop when a level is gained.")]
    [SerializeField, Min(0f)] private float slowDownTime = 0.8f;
    [Tooltip("Seconds of stillness between the full stop and the cards opening.")]
    [SerializeField, Min(0f)] private float openDelay = 0.3f;

    /// <summary>Extra cards on top of the base count (meta-progression, perks...).</summary>
    public int BonusChoices { get; set; }
    public int ChoiceCount => Mathf.Max(1, baseChoiceCount + BonusChoices);
    /// <summary>Extra rerolls per level-up on top of the base count (meta-progression, perks...).</summary>
    public int BonusRerolls { get; set; }
    public int RerollCount => Mathf.Max(0, baseRerolls + BonusRerolls);
    public bool IsChoosing { get; private set; }

    /// <summary>Raised after a power-up was applied: the power-up and how many times it has now been taken.</summary>
    public event Action<PowerUpDefinition, int> PowerUpChosen;

    private readonly Dictionary<PowerUpDefinition, int> stacks = new();
    private int pending;
    private float savedTimeScale = 1f;

    private void OnEnable()
    {
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

    private void HandleLevelUp(int newLevel)
    {
        pending++;
        if (!IsChoosing) StartCoroutine(ChoiceLoop());
    }

    [ContextMenu("Debug: Trigger Level Up Choice")]
    private void DebugTrigger() => HandleLevelUp(0);

    private IEnumerator ChoiceLoop()
    {
        IsChoosing = true;
        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;

        // Ease into slow motion, then a full stop. The HUD banner and the choice screen run on unscaled time.
        float elapsed = 0f;
        while (elapsed < slowDownTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / slowDownTime);
            Time.timeScale = Mathf.Lerp(savedTimeScale, 0f, k * k * (3f - 2f * k)); // smoothstep
            yield return null;
        }
        Time.timeScale = 0f;

        while (pending > 0)
        {
            yield return new WaitForSecondsRealtime(openDelay);

            pending--;
            List<PowerUpDefinition> choices = Roll(ChoiceCount);
            if (choices.Count == 0) continue;

            bool closed = false;
            screen.Show(choices, GetStacks, Apply, () => closed = true, RerollCount, () => Roll(ChoiceCount));
            while (!closed) yield return null;
        }

        // One extra frame so the confirming tap is not read as a ball launch.
        yield return null;
        Time.timeScale = savedTimeScale;
        IsChoosing = false;
    }

    private void Apply(PowerUpDefinition definition)
    {
        stacks[definition] = GetStacks(definition) + 1;

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
    private List<PowerUpDefinition> Roll(int count)
    {
        var pool = new List<PowerUpDefinition>();
        foreach (PowerUpDefinition definition in powerUps)
        {
            if (definition == null) continue;
            if (definition.MaxStacks > 0 && GetStacks(definition) >= definition.MaxStacks) continue;
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
        return result;
    }

    private static bool HasRarity(List<PowerUpDefinition> pool, PowerUpRarity rarity)
    {
        foreach (PowerUpDefinition p in pool)
            if (p.Rarity == rarity) return true;
        return false;
    }
}
