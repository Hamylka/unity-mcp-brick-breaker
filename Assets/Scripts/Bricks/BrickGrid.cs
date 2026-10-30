using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Generates the brick wall (rectangular bricks, optionally stacked) and slowly pushes it toward the player,
/// Space Invaders style: one step every few seconds, no sideways movement.
/// In endless mode every step adds a fresh row at the back, and clearing the whole wall spawns a new wave,
/// so a run only ends when the bricks reach the lose line.
/// Also the single place to listen to brick events (<see cref="BrickDestroyed"/>, <see cref="BricksCleared"/>)
/// and to the lose condition (<see cref="BricksReachedPaddle"/>).
/// The wall is moved through one kinematic Rigidbody on this object, so all brick colliders move as a
/// single compound body (cheap and physically correct for the ball).
/// Keep this object at the world origin. Use the context menu to preview the layout in the editor.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BrickGrid : MonoBehaviour
{
    [SerializeField] private Brick brickPrefab;
    [SerializeField] private BrickPalette palette;
    [SerializeField] private bool generateOnStart = true;

    [Header("Layout (world units)")]
    [Tooltip("Bricks per row.")]
    [SerializeField, Min(1)] private int columns = 6;
    [Tooltip("Depth of the starting wall (and of every new wave), in rows.")]
    [SerializeField, Min(1)] private int rows = 14;
    [Tooltip("Depth of one brick (Z). Brick width = Length In Cells x this.")]
    [SerializeField, Min(0.1f)] private float cellSize = 0.6f;
    [Tooltip("2 = classic 2:1 brick.")]
    [SerializeField, Min(1)] private int lengthInCells = 2;
    [Tooltip("Gap between neighbouring bricks.")]
    [SerializeField, Min(0f)] private float spacing = 0.08f;
    [Tooltip("Height of a brick (Y). With stacking, also the height of one layer.")]
    [FormerlySerializedAs("layerHeight")]
    [SerializeField, Min(0.05f)] private float brickHeight = 0.4f;
    [Tooltip("World Z of the first (closest) row of a fresh wall.")]
    [SerializeField] private float firstRowZ = 12f;

    [Header("Stacking")]
    [SerializeField, Min(1)] private int minLayers = 1;
    [SerializeField, Min(1)] private int maxLayers = 3;

    [Header("Variation")]
    [Tooltip("Relative chance of each hit point value. Element 0 = 1 HP (white) ... element 4 = 5 HP.")]
    [SerializeField] private float[] hitPointWeights = { 45f, 25f, 15f, 10f, 5f };
    [Tooltip("Chance that a cell is left empty in the starting rows.")]
    [SerializeField, Range(0f, 0.8f)] private float emptyChance = 0.3f;
    [Tooltip("Gaps never get rarer than this.")]
    [SerializeField, Range(0f, 0.8f)] private float minEmptyChance = 0.03f;
    [Tooltip("How much the gap chance drops for each row spawned after the starting wall.")]
    [SerializeField, Min(0f)] private float emptyChanceDropPerRow = 0.02f;
    [Tooltip("Mirror the left half onto the right half.")]
    [SerializeField] private bool mirror = true;
    [Tooltip("0 = a different layout every run, otherwise a fixed seed.")]
    [SerializeField] private int seed;

    [Header("Advance (Space Invaders style)")]
    [Tooltip("Master switch for the advancing wall.")]
    [SerializeField] private bool advance = true;
    [Tooltip("The wall only advances while this ball is in play (not while it waits on the paddle). Leave empty to always advance.")]
    [SerializeField] private BallController ball;
    [Tooltip("Seconds between steps (counted only while the ball is in play).")]
    [SerializeField, Min(0.1f)] private float stepInterval = 8f;
    [Tooltip("Distance of one step, in brick rows.")]
    [SerializeField, Min(0.1f)] private float stepInRows = 1f;
    [Tooltip("Seconds the slide of one step takes.")]
    [SerializeField, Min(0.01f)] private float stepDuration = 0.5f;

    [Header("Endless Mode")]
    [Tooltip("Add a new row at the back on every step, and a new wave when the wall is cleared.")]
    [SerializeField] private bool endless = true;
    [Tooltip("Seconds between clearing the last brick and the next wave appearing. 0 = immediately.")]
    [SerializeField, Min(0f)] private float waveDelay;
    [Tooltip("Rows in the wave that appears when the whole wall has been cleared.")]
    [SerializeField, Min(1)] private int clearWaveRows = 4;
    [Tooltip("Each row spawned after the starting wall makes tougher bricks more likely. 0 = no ramp.")]
    [SerializeField, Min(0f)] private float difficultyPerRow = 0.03f;
    [Header("HP Window (follows ball damage)")]
    [Tooltip("Hits the toughest new brick takes at the start of a run.")]
    [SerializeField, Min(1f)] private float targetHitsStart = 5f;
    [Tooltip("Hits added to the toughest brick's target per minute of play (5 at the start, 12 at 5 minutes with 1.4).")]
    [SerializeField, Min(0f)] private float targetHitsPerMinute = 1.4f;
    [Tooltip("The weakest brick has this fraction of the toughest one's HP (0.2 = 1:5).")]
    [SerializeField, Range(0.01f, 1f)] private float minHpRatio = 0.2f;
    [Tooltip("Time constant (s) of the average of the ball's base damage. A fresh damage upgrade makes the existing wall easy until new rows catch up.")]
    [SerializeField, Min(0.1f)] private float damageSmoothing = 20f;
    [Tooltip("The toughest HP can grow by at most this factor per row (avoids spikes after a streak of level-ups).")]
    [SerializeField, Min(1f)] private float maxGrowthPerRow = 2f;
    [Tooltip("\"Difficulty increased\" fires when the window max has grown by this factor since the last notice.")]
    [SerializeField, Min(1.01f)] private float noticeGrowth = 1.75f;

    [Header("HP Window (follows the kill rate)")]
    [Tooltip("Bricks the player should destroy per second (about the rate rows are added). Faster than this means the player has more power than the ball damage shows (splash, cleave, orbs), so new bricks get tougher.")]
    [SerializeField, Min(0.1f)] private float targetKillsPerSecond = 1.5f;
    [Tooltip("Time constant (s) of the kill rate average.")]
    [SerializeField, Min(0.5f)] private float killRateSmoothing = 25f;

    [Header("Label damage (hits left shown on bricks)")]
    [Tooltip("Seconds over which total damage and ball-hit damage are averaged to get the damage multiplier of the labels.")]
    [SerializeField, Min(1f)] private float labelDamageWindow = 15f;
    [Tooltip("Highest multiplier applied to the ball damage for the labels.")]
    [SerializeField, Min(1f)] private float labelDamageMaxRatio = 25f;
    [Tooltip("Seconds to follow a rising multiplier (falls 3x slower).")]
    [SerializeField, Min(0.1f)] private float labelRiseTime = 1.5f;
    [Tooltip("When the wanted window falls (pace relaxed), the toughest HP shrinks to at most this share of itself per row (0.9 = -10% per row).")]
    [SerializeField, Range(0.5f, 1f)] private float shrinkPerRow = 0.9f;
    [Tooltip("The pace factor multiplying the window grows by at most this factor per row while the kill rate is over target.")]
    [SerializeField, Min(1f)] private float maxPaceGrowthPerRow = 1.3f;
    [Tooltip("The pace factor never goes above this. Also, when the toughest brick already needs more than (smoothed damage x target hits x this) HP, it stops growing and relaxes.")]
    [SerializeField, Min(1f)] private float maxPaceFactor = 4f;
    [Tooltip("Below this share of the target kill rate the pace factor relaxes back toward 1 (x0.9 per update, every 2 s). It only grows above 1.25x the target.")]
    [SerializeField, Range(0.1f, 1f)] private float relaxBelow = 0.6f;

    [Header("Endless Spawn Timing")]
    [Tooltip("Pop-in delay between neighbouring bricks of a new row (ripple from the center).")]
    [SerializeField, Min(0f)] private float spawnRipple = 0.04f;
    [Tooltip("Pop-in delay between rows when a whole wall or wave appears.")]
    [SerializeField, Min(0f)] private float waveRowDelay = 0.06f;

    [Header("Lose Line")]
    [Tooltip("When any brick's front edge reaches this world Z, the player loses.")]
    [SerializeField] private float dangerZ = 1.8f;
    [Tooltip("Optional marker object, kept at the lose line's Z.")]
    [SerializeField] private Transform dangerLine;

    /// <summary>Raised on every ball hit: the brick and how many hit points it lost.</summary>
    public event Action<Brick, int> BrickHit;
    /// <summary>Raised for every brick that gets destroyed (after it is removed from the count).</summary>
    public event Action<Brick> BrickDestroyed;
    /// <summary>Raised when the last brick is destroyed (in endless mode a new wave follows right after).</summary>
    public event Action BricksCleared;
    /// <summary>Raised when a whole new wave of rows appears after the wall was cleared.</summary>
    public event Action WaveSpawned;
    /// <summary>Raised once when the wall reaches the lose line. Game over.</summary>
    public event Action BricksReachedPaddle;
    /// <summary>Raised when a step starts. Hook up camera shake or sound here.</summary>
    public event Action StepStarted;
    /// <summary>Raised when the HP window has grown by <c>Notice Growth</c> since the last time (min, max). Hook up the soft "it got harder" warning here.</summary>
    public event Action<int, int> HpScaled;
    /// <summary>Raised whenever the HP window grows (min, max), at most once per row.</summary>
    public event Action<int, int> HpWindowGrew;

    /// <summary>Weakest brick HP that currently spawns.</summary>
    public int CurrentMinHp { get; private set; } = 1;
    /// <summary>Toughest brick HP that currently spawns.</summary>
    public int CurrentMaxHp { get; private set; } = 5;
    /// <summary>Seconds of play this run (only counts while the ball is in play, scaled time).</summary>
    public float RunTime => runTime;
    /// <summary>Smoothed base damage of the ball, the input of the HP window.</summary>
    public float SmoothedDamage => smoothedDamage;
    /// <summary>Hits the toughest brick spawning now is aimed at, <c>5 + 1.4 x minutes</c>.</summary>
    public float TargetHits => targetHitsStart + targetHitsPerMinute * runTime / 60f;
    /// <summary>Bricks destroyed per second, averaged over the last few seconds of play.</summary>
    public float KillRate => killAccum / killRateSmoothing;
    /// <summary>Multiplier (1 or more) on the HP window that tracks the damage the ball hit does not show; rises while the player kills faster than the target.</summary>
    public float PaceFactor => paceFactor;

    /// <summary>Multiplies the time between steps (upgrades). 1.25 = the wall is 25% slower.</summary>
    public void MultiplyStepInterval(float factor)
    {
        // Cards can shorten the interval (Rushing Wall), but never under MinWallStepFactor of the run's start value.
        if (startStepInterval < 0f) startStepInterval = stepInterval;
        stepInterval = Mathf.Max(startStepInterval * PowerUpStats.MinWallStepFactor, stepInterval * Mathf.Max(0.1f, factor));
    }

    private float startStepInterval = -1f;

    /// <summary>Width of one brick cell (X), for area-of-effect distances.</summary>
    public float BrickWidth => lengthInCells * cellSize;
    /// <summary>Depth of one brick cell (Z).</summary>
    public float CellDepth => cellSize;

    /// <summary>Fills the list with the bottom brick of every stack that still has one alive (the bricks the ball can hit).</summary>
    public void GetLiveBricks(System.Collections.Generic.List<Brick> result)
    {
        result.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            if (!transform.GetChild(i).TryGetComponent(out BrickStack stack)) continue;
            Brick bottom = stack.Bottom;
            if (bottom != null && bottom.IsAlive) result.Add(bottom);
        }
    }

    public int RemainingBricks { get; private set; }
    public bool IsGameOver { get; private set; }
    /// <summary>Total rows spawned this run, including the starting wall.</summary>
    public int RowsSpawned => rowsSpawned;

    /// <summary>Changes the ball whose flight gates the wall advance (when an extra ball becomes the main one).</summary>
    public void SetBall(BallController newBall) => ball = newBall;

    /// <summary>Pause or resume the advance (for pause menus, cutscenes, freeze bonuses).</summary>
    public bool AdvanceEnabled
    {
        get => advance;
        set => advance = value;
    }

    private Rigidbody body;
    private System.Random rng;
    private int rowsSpawned;
    private float offset;
    private float stepTimer;
    private float waveTimer;
    private float stepT;
    private float stepFrom;
    private float stepTo;
    private bool stepping;
    private float runTime;
    private float smoothedDamage = 1f;
    private float killAccum;    // decaying count of recent kills (kill rate = killAccum / killRateSmoothing)
    private float paceFactor = 1f;
    private float lastPaceTime;
    private bool windowReady;
    private int lastNoticeMax;

    // Effective damage for the labels: recent total damage / recent ball-hit damage (Phase excluded from both).
    private float ballDealtAccum;
    private float totalDealtAccum;
    private float damageRatio = 1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        if (generateOnStart) Generate();
    }

    // Brick colors show the hits left with the permanent ball damage (level-ups and cards): recolor them when it
    // changes. Temporary wall buffs (2x, Phase) are ignored so the wall does not flicker through colors every
    // few seconds, and with no ball flying the last value is kept instead of dropping to 1 (which recolored everything).
    private void Update()
    {
        if (BallController.Active.Count > 0) UpdateLabelDamage();

        // The HP window input: run clock (ball in play only, like the wall advance) and the smoothed base damage.
        if (IsGameOver || (ball != null && !ball.IsFlying)) return;
        float dt = Time.deltaTime;
        runTime += dt;
        smoothedDamage += (BallController.MainBaseDamage - smoothedDamage) * (1f - Mathf.Exp(-dt / damageSmoothing));
        killAccum *= Mathf.Exp(-dt / killRateSmoothing);
        float decay = Mathf.Exp(-dt / labelDamageWindow);
        ballDealtAccum *= decay;
        totalDealtAccum *= decay;
    }

    private void OnValidate()
    {
        if (maxLayers < minLayers) maxLayers = minLayers;
        PlaceDangerLine();
    }

    private void FixedUpdate()
    {
        if (!advance || IsGameOver) return;
        float dt = Time.fixedDeltaTime;

        if (stepping)
        {
            stepT = Mathf.Min(1f, stepT + dt / stepDuration);
            float k = stepT * stepT * (3f - 2f * stepT); // smoothstep: a soft slide, not a snap
            offset = Mathf.Lerp(stepFrom, stepTo, k);
            body.MovePosition(new Vector3(0f, 0f, offset));

            if (stepT >= 1f)
            {
                stepping = false;
                CheckLoseLine();
            }
            return;
        }

        if (RemainingBricks <= 0)
        {
            if (!endless) return;

            // Wall cleared with a delay set (or the immediate wave came out empty): spawn the wave now.
            waveTimer += dt;
            if (waveTimer >= waveDelay)
            {
                waveTimer = 0f;
                SpawnWave();
            }
            return;
        }

        if (ball != null && !ball.IsFlying) return;

        stepTimer += dt;
        if (stepTimer >= stepInterval * PowerUpStats.WallStepFactor)
        {
            stepTimer = 0f;
            stepFrom = offset;
            stepTo = offset - stepInRows * cellSize;
            stepT = 0f;
            stepping = true;

            // New back row, placed so that it ends the slide exactly where the old back row started.
            if (endless)
            {
                float finalWorldZ = firstRowZ + (rows - 1) * cellSize;
                SpawnRow(finalWorldZ - stepTo, 0f, true);
            }

            SoundManager.Play(Sfx.WallStep);
            StepStarted?.Invoke();
        }
    }

    [ContextMenu("Generate / Preview")]
    public void Generate()
    {
        Clear();

        if (brickPrefab == null)
        {
            Debug.LogWarning("BrickGrid: no brick prefab assigned.", this);
            return;
        }

        rng = seed == 0 ? new System.Random() : new System.Random(seed);

        // Animate only in play mode; the editor preview appears instantly.
        bool animate = Application.isPlaying;
        for (int row = 0; row < rows; row++)
            SpawnRow(firstRowZ + row * cellSize, row * waveRowDelay, animate);

        PlaceDangerLine();
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        RemainingBricks = 0;
        rowsSpawned = 0;
        runTime = 0f;
        smoothedDamage = BallController.MainBaseDamage;
        windowReady = false;
        UpdateHpWindow();
        Brick.ResetColorScale(CurrentMaxHp);
        Brick.ResetHitDamage(BallController.MainBaseDamage);
        ballDealtAccum = totalDealtAccum = 0f;
        damageRatio = 1f;

        // Back to the starting position and fresh timers.
        offset = 0f;
        stepTimer = 0f;
        waveTimer = 0f;
        stepT = 0f;
        stepping = false;
        IsGameOver = false;
        transform.position = Vector3.zero;
        if (body != null) body.position = Vector3.zero;
    }

    // A fresh wave at the starting position, in the wall's current (advanced) frame of reference.
    // The step timer restarts, so the new wave gets a full interval before it moves.
    private void SpawnWave()
    {
        // If the wall is mid-step, place the rows relative to where the step will end.
        float reference = stepping ? stepTo : offset;
        for (int row = 0; row < clearWaveRows; row++)
            SpawnRow(firstRowZ + row * cellSize - reference, row * waveRowDelay, true);

        stepTimer = 0f;
        waveTimer = 0f;
        WaveSpawned?.Invoke();
    }

    // localZ is relative to the grid (the grid moves, so world Z = localZ + offset).
    private void SpawnRow(float localZ, float baseDelay, bool animate)
    {
        rng ??= seed == 0 ? new System.Random() : new System.Random(seed);

        int rowNumber = rowsSpawned++;

        // Update the window before the row spawns: bricks already on the wall recolor, the new row uses the new window.
        UpdateHpWindow();

        int buildColumns = mirror ? (columns + 1) / 2 : columns;

        for (int col = 0; col < buildColumns; col++)
        {
            if (rng.NextDouble() < GetEmptyChance(rowNumber)) continue;

            int layers = rng.Next(minLayers, maxLayers + 1);
            int[] hitPoints = new int[layers];
            for (int i = 0; i < layers; i++) hitPoints[i] = PickHitPoints(rowNumber);

            SpawnStack(col, rowNumber, localZ, hitPoints, animate, baseDelay);

            int mirrorCol = columns - 1 - col;
            if (mirror && mirrorCol != col)
                SpawnStack(mirrorCol, rowNumber, localZ, hitPoints, animate, baseDelay);
        }
    }

    private void SpawnStack(int col, int rowNumber, float localZ, int[] layerHitPoints, bool animate, float baseDelay)
    {
        float brickWidth = lengthInCells * cellSize;
        float x = (col - (columns - 1) * 0.5f) * brickWidth;

        var stackObject = new GameObject($"Stack_{col}_{rowNumber}");
        stackObject.transform.SetParent(transform, false);
        stackObject.transform.localPosition = new Vector3(x, 0f, localZ);

        var stack = stackObject.AddComponent<BrickStack>();
        Vector3 size = new(brickWidth - spacing, brickHeight, cellSize - spacing);
        stack.Setup(size.x, size.z, brickHeight);

        // Ripple outward from the center column.
        float delay = baseDelay + Mathf.Abs(col - (columns - 1) * 0.5f) * spawnRipple;

        for (int layer = 0; layer < layerHitPoints.Length; layer++)
        {
            Brick brick = Instantiate(brickPrefab, stackObject.transform);
            brick.name = $"Brick_{layer}";
            brick.ApplySize(size);
            brick.Initialize(layerHitPoints[layer], palette);
            stack.AddBrick(brick);
            brick.Destroyed += HandleBrickDestroyed;
            brick.Hit += HandleBrickHit;
            if (animate) brick.PlaySpawn(delay + layer * 0.05f);
            RemainingBricks++;
        }
    }

    // Phase is left out of both sums: its malus is not in MainBaseDamage.
    private void RecordDamage(int dealt)
    {
        DamageSource source = DamageTag.Current;
        if (source == DamageSource.Phase) return;
        totalDealtAccum += dealt;
        if (source == DamageSource.Ball) ballDealtAccum += dealt;
    }

    // Labels and colors count what a ball hit really does with the area effects: base damage x recent
    // (total damage / ball-hit damage). The ratio is smoothed (quick up, slow down) and the value is only
    // pushed when it moved by 8% or more, so labels do not flicker. With too few ball hits it keeps its value.
    private void UpdateLabelDamage()
    {
        float baseDamage = BallController.MainBaseDamage;
        if (ballDealtAccum >= 3f)
        {
            float target = Mathf.Clamp(totalDealtAccum / ballDealtAccum, 1f, labelDamageMaxRatio);
            float time = target > damageRatio ? labelRiseTime : labelRiseTime * 3f;
            damageRatio += (target - damageRatio) * (1f - Mathf.Exp(-Time.deltaTime / time));
        }

        int wanted = Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageRatio));
        int current = Brick.HitDamage;
        if (Mathf.Abs(wanted - current) >= Mathf.Max(1f, current * 0.08f)) Brick.SetHitDamage(wanted);
    }

    private void HandleBrickHit(Brick brick, int damage)
    {
        RecordDamage(damage);
        BrickHit?.Invoke(brick, damage);
    }

    private void HandleBrickDestroyed(Brick brick)
    {
        RemainingBricks--;
        killAccum += 1f;
        BrickDestroyed?.Invoke(brick);
        if (RemainingBricks > 0) return;

        BricksCleared?.Invoke();

        // Endless mode: the board refills right away so it never feels empty.
        if (endless && !IsGameOver && waveDelay <= 0f) SpawnWave();
    }

    private void CheckLoseLine()
    {
        float front = float.MaxValue;
        foreach (BrickStack stack in GetComponentsInChildren<BrickStack>())
        {
            if (stack.Count == 0) continue;
            front = Mathf.Min(front, stack.LocalFrontZ + offset);
        }

        if (front > dangerZ) return;

        IsGameOver = true;
        Debug.Log("Game over: the bricks reached the paddle line.", this);
        BricksReachedPaddle?.Invoke();
    }

    private void PlaceDangerLine()
    {
        if (dangerLine == null) return;
        Vector3 p = dangerLine.position;
        p.z = dangerZ;
        dangerLine.position = p;
    }

    // Tougher bricks become more likely the deeper into the run we are.
    // Rolls inside the current [min, max] HP window. The weights curve is stretched across the window.
    private int PickHitPoints(int rowNumber)
    {
        int min = CurrentMinHp;
        int max = CurrentMaxHp;
        if (max <= min) return min;

        int extraRows = Mathf.Max(0, rowNumber - (rows - 1));

        float total = 0f;
        for (int hp = min; hp <= max; hp++) total += Weight(hp, extraRows);
        if (total <= 0f) return min;

        double roll = rng.NextDouble() * total;
        for (int hp = min; hp <= max; hp++)
        {
            roll -= Weight(hp, extraRows);
            if (roll <= 0.0) return hp;
        }
        return max;
    }

    // HP window for the next row: max = ceil(smoothed ball damage x target hits), min = max x ratio.
    // It grows at most x maxGrowthPerRow per row and shrinks at most to shrinkPerRow of itself per row.
    private void UpdateHpWindow()
    {
        // Pace factor: the ball's damage misses splash, cleave, orbs and bolts, so the window also follows how fast
        // bricks actually die. Over the target kill rate it grows (at most maxPaceGrowthPerRow per row); well under, it relaxes.
        // Updated at most once every 2 s of play, so a 4-row wave does not compound it four times.
        if (windowReady && runTime - lastPaceTime >= 2f)
        {
            lastPaceTime = runTime;
            float ratio = KillRate / targetKillsPerSecond;
            // Already far tougher than the ball can handle (e.g. a percent-HP burn does the killing): stop pushing and relax.
            bool tooTough = CurrentMaxHp > smoothedDamage * TargetHits * maxPaceFactor;
            if (ratio > 1.25f && !tooTough) paceFactor = Mathf.Min(maxPaceFactor, paceFactor * Mathf.Min(ratio, maxPaceGrowthPerRow));
            else if (ratio < relaxBelow || tooTough) paceFactor = Mathf.Max(1f, paceFactor * 0.9f);
        }

        int wanted = Mathf.Max(1, Mathf.CeilToInt(smoothedDamage * TargetHits * paceFactor));
        int max = wanted;
        if (windowReady)
        {
            // Grows toward the wanted value (at most x maxGrowthPerRow per row); when the wanted value falls (pace relaxed)
            // it shrinks back too, by at most shrinkPerRow per row, so a short kill burst does not harden the wall for good.
            if (wanted >= CurrentMaxHp)
                max = Mathf.Min(wanted, Mathf.Max(CurrentMaxHp, Mathf.FloorToInt(CurrentMaxHp * maxGrowthPerRow)));
            else
                max = Mathf.Max(wanted, Mathf.CeilToInt(CurrentMaxHp * shrinkPerRow));
        }

        int min = Mathf.Clamp(Mathf.RoundToInt(max * minHpRatio), 1, max);
        bool grew = windowReady && max > CurrentMaxHp;
        bool shrank = windowReady && max < CurrentMaxHp;
        CurrentMinHp = min;
        CurrentMaxHp = max;

        if (!windowReady)
        {
            windowReady = true;
            lastNoticeMax = max;
            return;
        }
        if (shrank)
        {
            Brick.SetColorScale(max, false);
            lastNoticeMax = Mathf.Min(lastNoticeMax, max); // a later regrowth can warn again
            return;
        }
        if (!grew) return;

        bool notice = max >= lastNoticeMax * noticeGrowth;
        if (notice) lastNoticeMax = max;
        Brick.SetColorScale(max, notice);
        HpWindowGrew?.Invoke(min, max);
        if (notice) HpScaled?.Invoke(min, max);
    }

    // Fewer gaps the deeper into the run we are.
    private float GetEmptyChance(int rowNumber)
    {
        int extraRows = Mathf.Max(0, rowNumber - (rows - 1));
        return Mathf.Max(minEmptyChance, emptyChance - emptyChanceDropPerRow * extraRows);
    }

    // Weight of one HP value: the weights curve sampled at its position in the window, then the per-row ramp.
    private float Weight(int hp, int extraRows)
    {
        float u = CurrentMaxHp > CurrentMinHp ? (hp - CurrentMinHp) / (float)(CurrentMaxHp - CurrentMinHp) : 0f;
        float position = u * (hitPointWeights.Length - 1);
        int lower = Mathf.FloorToInt(position);
        int upper = Mathf.Min(lower + 1, hitPointWeights.Length - 1);
        float curve = Mathf.Lerp(hitPointWeights[lower], hitPointWeights[upper], position - lower);
        return Mathf.Max(0f, curve) * (1f + position * difficultyPerRow * extraRows);
    }
}
