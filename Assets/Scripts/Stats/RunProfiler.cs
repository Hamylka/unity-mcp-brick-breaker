using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Records a run in the background for balancing: damage by source, XP, level timing, the cards offered and picked,
/// bonuses, wall zones, HP scale-ups, the cause of death and a snapshot every few seconds. When the run ends the
/// <see cref="RunRecord"/> is written as JSON to <see cref="Folder"/> (kept by default; the Game Over screen can
/// discard it or share it, see <see cref="SetKept"/> and <see cref="Share"/>). Builds itself at startup, no scene setup.
/// Other systems add notes with <see cref="Log"/>; damage is attributed with <see cref="DamageTag"/>.
/// </summary>
public class RunProfiler : MonoBehaviour
{
    [Tooltip("Seconds between two snapshots of the run.")]
    [SerializeField, Min(0.5f)] private float sampleInterval = 2f;
    [Tooltip("Turn off to stop recording and saving runs.")]
    [SerializeField] private bool recording = true;

    // Above this, sharing as text may exceed what an Android intent can carry: copy to the clipboard instead.
    private const int MaxShareChars = 400_000;

    private static readonly int SourceCount = Enum.GetValues(typeof(DamageSource)).Length;

    /// <summary>Folder the run files are written to (on a phone: Android/data/&lt;package&gt;/files/RunData).</summary>
    public static string Folder => Path.Combine(Application.persistentDataPath, "RunData");

    /// <summary>The recorder of the current scene, null if none.</summary>
    public static RunProfiler Instance { get; private set; }

    /// <summary>True once the run ended and its record was written.</summary>
    public bool HasSavedRun => savedPath != null;
    /// <summary>Whether the finished run is currently kept on disk.</summary>
    public bool IsKept { get; private set; }
    public string SavedPath => savedPath;

    private readonly RunRecord record = new();
    private readonly int[] hits = new int[SourceCount];
    private readonly int[] damage = new int[SourceCount];
    private readonly int[] kills = new int[SourceCount];
    private readonly float[] xp = new float[SourceCount];
    private readonly List<Brick> scratch = new();

    private GameManager game;
    private BrickGrid grid;
    private ExperienceSystem experience;
    private PowerUpManager powerUps;
    private BonusDropper bonuses;

    private bool finished;
    private float nextSample;
    private string savedPath;
    private string json;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Create();
        // The scene is reloaded by the Restart button: a new run gets a new recorder.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => Create();
    }

    /// <summary>Run recording and sharing are developer tools: only the editor and development builds have them.</summary>
    public static bool Available => Application.isEditor || Debug.isDebugBuild;

    private static void Create()
    {
        if (!Available) return;
        if (FindAnyObjectByType<RunProfiler>() == null)
            new GameObject("RunProfiler").AddComponent<RunProfiler>();
    }

    /// <summary>Adds a note to the current run (no-op when nothing is recording).</summary>
    public static void Log(string type, string name = "", float value = 0f, string detail = "")
    {
        if (Instance != null) Instance.AddEvent(type, name, value, detail);
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        BallController.BrickHitRegistered -= HandleBrickHitRegistered;
        BallController.AnyBallLost -= HandleAnyBallLost;
    }

    private void Start()
    {
        game = FindAnyObjectByType<GameManager>();
        grid = FindAnyObjectByType<BrickGrid>();
        experience = FindAnyObjectByType<ExperienceSystem>();
        powerUps = FindAnyObjectByType<PowerUpManager>();
        bonuses = FindAnyObjectByType<BonusDropper>();
        if (!recording || game == null || grid == null)
        {
            enabled = false;
            return;
        }

        record.runId = Guid.NewGuid().ToString("N").Substring(0, 8);
        record.startedUtc = DateTime.UtcNow.ToString("o");
        record.appVersion = Application.version;
        record.platform = Application.platform.ToString();
        record.device = SystemInfo.deviceModel;
        record.sources = Enum.GetNames(typeof(DamageSource));
        record.settings.baseXp = experience != null ? experience.BaseXp : 0;
        record.settings.xpPower = experience != null ? Mathf.Round(experience.Power * 1000f) / 1000f : 0f;
        record.settings.damagePerLevel = powerUps != null ? powerUps.DamagePerLevel : 0;
        record.settings.rerolls = powerUps != null ? powerUps.RerollCount : 0;
        if (experience != null)
        {
            record.settings.baseBricksPerLevel = experience.BaseBricksPerLevel;
            record.settings.bricksPerLevelGrowth = experience.BricksPerLevelGrowth;
        }
        record.settings.targetHitsPerMinute = grid.TargetHitsPerMinute;
        record.settings.targetKillsPerSecond = grid.TargetKillsPerSecond;
        record.settings.maxPaceFactor = grid.MaxPaceFactor;
        record.settings.labelDamageWindow = grid.LabelDamageWindow;
        record.settings.labelDamageMaxRatio = grid.LabelDamageMaxRatio;
        record.settings.wallStepIntervalStart = Round(grid.StepInterval);
        record.settings.speedStepInterval = game.SpeedStepInterval;
        record.settings.speedStepAmount = game.SpeedStepAmount;
        BallController anyBall = FindAnyObjectByType<BallController>();
        record.settings.minSpeedFactor = anyBall != null ? anyBall.MinSpeedFactor : 0f;

        BallController.BrickHitRegistered += HandleBrickHitRegistered;
        BallController.AnyBallLost += HandleAnyBallLost;
        grid.BrickHit += HandleBrickHit;
        grid.HpWindowGrew += HandleHpWindowGrew;
        game.GameOver += HandleGameOver;
        if (experience != null)
        {
            experience.LevelUp += HandleLevelUp;
            experience.ClearBonusGranted += HandleClearBonus;
        }
        if (powerUps != null)
        {
            powerUps.Offered += HandleOffered;
            powerUps.PowerUpChosen += HandlePowerUpChosen;
            powerUps.Corrupted += HandleCorrupted;
        }
        if (bonuses != null)
        {
            bonuses.BonusDropped += HandleBonusDropped;
            bonuses.BonusCollected += HandleBonusCollected;
        }
    }

    private void Update()
    {
        if (finished || game == null || game.RunTime < nextSample) return;
        nextSample = game.RunTime + sampleInterval;
        record.samples.Add(TakeSample());
    }

    // ---------------------------------------------------------------- recording

    private float Now => game != null ? game.RunTime : 0f;

    private void AddEvent(string type, string name, float value, string detail)
    {
        if (finished || !enabled) return;
        record.events.Add(new RunRecord.EventEntry { t = Round(Now), type = type, name = name, value = value, detail = detail });

        RunRecord.Summary s = record.summary;
        switch (type)
        {
            case "zone_spawn": s.zonesSpawned++; break;
            case "zone_taken": s.zonesTaken++; break;
        }
    }

    private void HandleBrickHit(Brick brick, int dealt)
    {
        if (finished || dealt <= 0) return;

        int i = (int)DamageTag.Current;
        hits[i]++;
        damage[i] += dealt;
        if (experience != null) xp[i] += experience.XpForHit(brick, dealt);
        if (!brick.IsAlive) kills[i]++;
    }

    private void HandleLevelUp(int level)
    {
        if (finished) return;
        WallStats(out int bricks, out int hp);
        record.levels.Add(new RunRecord.LevelEntry
        {
            level = level,
            t = Round(Now),
            totalXp = experience.TotalXp,
            xpToNext = experience.XpToNextLevel,
            ballDamage = BallController.MainDamage,
            bricksDestroyed = game.BricksDestroyed,
            wallBricks = bricks,
            wallHp = hp,
            dmg = (int[])damage.Clone()
        });
    }

    private void HandleOffered(IReadOnlyList<PowerUpDefinition> cards, bool isReroll)
    {
        if (finished) return;
        var entry = new RunRecord.OfferEntry { t = Round(Now), level = experience != null ? experience.Level : 0, reroll = isReroll };
        foreach (PowerUpDefinition card in cards)
        {
            entry.offered.Add(card.DisplayName);
            entry.rarities.Add(card.Rarity.ToString());
        }
        record.offers.Add(entry);
        if (isReroll) record.summary.rerollsUsed++;
    }

    private void HandlePowerUpChosen(PowerUpDefinition definition, int stacks)
    {
        if (finished || record.offers.Count == 0) return;
        RunRecord.OfferEntry entry = record.offers[^1];
        entry.chosen = $"{definition.DisplayName} LV{stacks}";
        entry.chosenRarity = definition.Rarity.ToString();
    }

    private void HandleCorrupted(CorruptionResult result)
    {
        if (finished) return;
        string outcome = result.Outcome != null ? $"{result.Outcome.DisplayName} ({result.Outcome.Rarity})" : "none";
        AddEvent("corrupt", result.Source.DisplayName, result.NewMax, $"{outcome}, {Mathf.RoundToInt(result.ExquisiteChance * 100f)}% exquisite");
    }

    private void HandleBonusDropped(BonusDefinition definition)
    {
        record.summary.bonusesDropped++;
        AddEvent("bonus_drop", definition.DisplayName, 0f, "");
    }

    private void HandleBonusCollected(BonusDefinition definition, PaddleController paddle)
    {
        record.summary.bonusesCollected++;
        AddEvent("bonus_collected", definition.DisplayName, 0f, "");
    }

    // Logged every time the window grows (at most once per row): value = target hits, detail = min-max HP.
    private void HandleHpWindowGrew(int min, int max) =>
        AddEvent("hp_window", "", Round(grid.TargetHits), $"{min}-{max}");

    private void HandleClearBonus(int xpAmount)
    {
        record.summary.wallClears++;
        AddEvent("wall_clear", "", xpAmount, "");
    }

    private RunRecord.Sample TakeSample()
    {
        WallStats(out int bricks, out int hp);
        int total = 0, flying = 0;
        foreach (int d in damage) total += d;
        foreach (BallController ball in BallController.Active)
            if (ball != null && ball.IsFlying) flying++;

        return new RunRecord.Sample
        {
            t = Round(Now),
            level = experience != null ? experience.Level : 0,
            totalXp = experience != null ? experience.TotalXp : 0,
            ballDamage = BallController.MainDamage,
            wallBricks = bricks,
            wallHp = hp,
            hpMax = grid.CurrentMaxHp,
            totalDamage = total,
            flyingBalls = flying,
            baseDamage = BallController.MainBaseDamage,
            cardMultiplier = Round(BallController.MainCardMultiplier),
            smoothedDamage = Round(grid.SmoothedDamage),
            targetHits = Round(grid.TargetHits),
            paceFactor = Round(grid.PaceFactor),
            killRate = Round(grid.KillRate),
            labelDamage = Brick.HitDamage,
            speedFactor = Round(BallController.RunSpeedFactor),
            wallStepInterval = Round(grid.StepInterval),
            ballSize = Round(PowerUpStats.BallSize),
            corruptionCharges = PowerUpStats.CorruptionCharges,
            xpBrake = experience != null ? Round(experience.KillRateBrake) : 1f,
            dangerLevel = Round(DangerMeter.Level),
            stepsLeft = Round((grid.RestingFrontZ - grid.DangerZ) / Mathf.Max(0.01f, grid.StepDistance)),
            frontGap = Round(grid.RestingFrontZ - grid.DangerZ),
            killRatePerBall = Round(grid.KillRate / Mathf.Max(1, flying)),
            rampHits = Round(grid.TargetHitsPerMinute * grid.RunTime / 60f)
        };
    }

    // Brick hit by a ball: how many side / back wall bounces came since its previous brick hit.
    private void HandleBrickHitRegistered(BallController ball, int side, int back)
    {
        if (finished) return;
        RunRecord.Summary s = record.summary;
        s.brickHitsCounted++;
        s.sideBounceHist[Mathf.Min(side, 6)]++;
        s.backBounceHist[Mathf.Min(back, 6)]++;
        s.sideBounceTotal += side;
        s.backBounceTotal += back;
    }

    // A ball fell past the paddle: what the player was doing. detail = key=value pairs.
    private void HandleAnyBallLost(BallController lost)
    {
        if (finished) return;
        int others = 0;
        foreach (BallController ball in BallController.Active)
            if (ball != null && ball != lost && ball.IsFlying) others++;
        record.summary.ballsLost++;
        AddEvent("ball_lost", lost.IsExtra ? "extra" : "main", Round(lost.TimeSincePaddleHit),
            $"others={others};speed={Round(lost.CurrentSpeed)};paddleOffset={Round(lost.PaddleOffset)};sincePaddle={Round(lost.TimeSincePaddleHit)};phoenix={PowerUpStats.PhoenixReady}");
    }

    // Bricks on the wall and their total HP (the bottom brick of each stack, the only ones the ball can reach).
    private void WallStats(out int bricks, out int hp)
    {
        grid.GetLiveBricks(scratch);
        bricks = scratch.Count;
        hp = 0;
        foreach (Brick brick in scratch) hp += brick.HitPoints;
    }

    private static float Round(float value) => Mathf.Round(value * 100f) / 100f;

    // ---------------------------------------------------------------- end of run

    private void HandleGameOver(GameManager.LoseReason reason)
    {
        if (finished) return;
        record.samples.Add(TakeSample()); // the very last state
        finished = true;

        RunRecord.Summary s = record.summary;
        s.endReason = reason.ToString();
        s.runTime = Round(game.RunTime);
        s.level = experience != null ? experience.Level : 1;
        s.totalXp = experience != null ? experience.TotalXp : 0;
        s.bricksDestroyed = game.BricksDestroyed;
        s.rowsSpawned = grid.RowsSpawned;
        s.echoProcs = PowerUpStats.EchoProcs;
        s.echoRepeats = PowerUpStats.EchoRepeats;
        s.echoBallKills = PowerUpStats.EchoBallKills;
        s.echoJumps = PowerUpStats.EchoJumps;
        s.echoNoTarget = PowerUpStats.EchoNoTarget;
        s.echoCarriedDamage = PowerUpStats.EchoCarried;
        s.targetHits = Round(grid.TargetHits);
        s.smoothedDamage = Round(grid.SmoothedDamage);
        s.paceFactor = Round(grid.PaceFactor);
        s.killRate = Round(grid.KillRate);
        s.hpWindowMin = grid.CurrentMinHp;
        s.hpWindowMax = grid.CurrentMaxHp;
        s.ballDamage = BallController.MainDamage;
        foreach (int d in damage) s.totalDamage += d;

        for (int i = 0; i < SourceCount; i++)
        {
            record.bySource.Add(new RunRecord.SourceTotal
            {
                source = ((DamageSource)i).ToString(),
                hits = hits[i],
                damage = damage[i],
                kills = kills[i],
                xp = Mathf.Round(xp[i] * 10f) / 10f
            });
        }

        json = JsonUtility.ToJson(record);
        Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            savedPath = Path.Combine(Folder, $"run_{stamp}_{record.runId}.json");
            File.WriteAllText(savedPath, json);
            IsKept = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"RunProfiler: could not save the run ({e.Message}).");
            savedPath = null;
            IsKept = false;
        }
    }

    /// <summary>Keeps (writes) or discards (deletes) the file of the finished run. Used by the Game Over screen.</summary>
    public void SetKept(bool keep)
    {
        if (!finished || keep == IsKept) return;

        if (keep)
        {
            Save();
            return;
        }

        try
        {
            if (savedPath != null && File.Exists(savedPath)) File.Delete(savedPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"RunProfiler: could not delete the run file ({e.Message}).");
        }
        IsKept = false;
    }

    /// <summary>
    /// Sends the finished run out of the device. On Android it opens the share sheet with the JSON as text (mail,
    /// notes, a chat with yourself...). Elsewhere it copies the JSON to the clipboard. Returns what happened.
    /// </summary>
    public string Share()
    {
        if (!finished || string.IsNullOrEmpty(json)) return "NO DATA";

#if UNITY_ANDROID && !UNITY_EDITOR
        if (json.Length <= MaxShareChars)
        {
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), $"Neon Breaker run {record.runId}");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), json);
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share run data");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("startActivity", chooser);
                return "SHARED";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RunProfiler: share failed ({e.Message}), copying instead.");
            }
        }
#endif
        GUIUtility.systemCopyBuffer = json;
        Debug.Log($"RunProfiler: run data copied to the clipboard ({json.Length} chars). File: {savedPath}");
        return "COPIED";
    }
}
