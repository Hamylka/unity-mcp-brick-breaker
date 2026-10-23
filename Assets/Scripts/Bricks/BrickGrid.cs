using System;
using UnityEngine;

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
    [Tooltip("Height of one layer.")]
    [SerializeField, Min(0.1f)] private float layerHeight = 0.6f;
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
    [Tooltip("Seconds between clearing the last brick and the next wave appearing.")]
    [SerializeField, Min(0f)] private float waveDelay = 1.2f;
    [Tooltip("Each row spawned after the starting wall makes tougher bricks more likely. 0 = no ramp.")]
    [SerializeField, Min(0f)] private float difficultyPerRow = 0.03f;
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
    /// <summary>Raised when the last brick is destroyed (in endless mode a new wave follows).</summary>
    public event Action BricksCleared;
    /// <summary>Raised when a whole new wave of rows appears after the wall was cleared.</summary>
    public event Action WaveSpawned;
    /// <summary>Raised once when the wall reaches the lose line. Game over.</summary>
    public event Action BricksReachedPaddle;
    /// <summary>Raised when a step starts. Hook up camera shake or sound here.</summary>
    public event Action StepStarted;

    /// <summary>Multiplies the time between steps (upgrades). 1.25 = the wall is 25% slower.</summary>
    public void MultiplyStepInterval(float factor) => stepInterval *= Mathf.Max(0.1f, factor);

    public int RemainingBricks { get; private set; }
    public bool IsGameOver { get; private set; }
    /// <summary>Total rows spawned this run, including the starting wall.</summary>
    public int RowsSpawned => rowsSpawned;

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

            // Wall cleared: after a short breather, a fresh wave rises at the usual spot.
            waveTimer += dt;
            if (waveTimer >= waveDelay)
            {
                waveTimer = 0f;
                stepTimer = 0f;
                SpawnWave();
            }
            return;
        }

        if (ball != null && !ball.IsFlying) return;

        stepTimer += dt;
        if (stepTimer >= stepInterval)
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

    // A whole new wall at the starting position, in the wall's current (advanced) frame of reference.
    private void SpawnWave()
    {
        for (int row = 0; row < rows; row++)
            SpawnRow(firstRowZ + row * cellSize - offset, row * waveRowDelay, true);
        WaveSpawned?.Invoke();
    }

    // localZ is relative to the grid (the grid moves, so world Z = localZ + offset).
    private void SpawnRow(float localZ, float baseDelay, bool animate)
    {
        rng ??= seed == 0 ? new System.Random() : new System.Random(seed);

        int rowNumber = rowsSpawned++;
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
        Vector3 size = new(brickWidth - spacing, layerHeight, cellSize - spacing);
        stack.Setup(size.x, size.z, layerHeight);

        // Ripple outward from the center column.
        float delay = baseDelay + Mathf.Abs(col - (columns - 1) * 0.5f) * spawnRipple;

        for (int layer = 0; layer < layerHitPoints.Length; layer++)
        {
            Brick brick = Instantiate(brickPrefab, stackObject.transform);
            brick.name = $"Brick_{layer}";
            brick.transform.localScale = size;
            brick.Initialize(layerHitPoints[layer], palette);
            stack.AddBrick(brick);
            brick.Destroyed += HandleBrickDestroyed;
            brick.Hit += HandleBrickHit;
            if (animate) brick.PlaySpawn(delay + layer * 0.05f);
            RemainingBricks++;
        }
    }

    private void HandleBrickHit(Brick brick, int damage) => BrickHit?.Invoke(brick, damage);

    private void HandleBrickDestroyed(Brick brick)
    {
        RemainingBricks--;
        BrickDestroyed?.Invoke(brick);
        if (RemainingBricks <= 0) BricksCleared?.Invoke();
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
    private int PickHitPoints(int rowNumber)
    {
        int count = hitPointWeights.Length;
        if (palette != null) count = Mathf.Min(count, palette.MaxHitPoints);

        int extraRows = Mathf.Max(0, rowNumber - (rows - 1));

        float total = 0f;
        for (int i = 0; i < count; i++) total += Weight(i, extraRows);
        if (total <= 0f) return 1;

        double roll = rng.NextDouble() * total;
        for (int i = 0; i < count; i++)
        {
            roll -= Weight(i, extraRows);
            if (roll <= 0.0) return i + 1;
        }
        return count;
    }

    // Fewer gaps the deeper into the run we are.
    private float GetEmptyChance(int rowNumber)
    {
        int extraRows = Mathf.Max(0, rowNumber - (rows - 1));
        return Mathf.Max(minEmptyChance, emptyChance - emptyChanceDropPerRow * extraRows);
    }

    private float Weight(int index, int extraRows)
    {
        return Mathf.Max(0f, hitPointWeights[index]) * (1f + index * difficultyPerRow * extraRows);
    }
}
