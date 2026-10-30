using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the run: tracks a few stats and ends the run when the ball is lost or the wall reaches the lose line.
/// On game over the <see cref="GameOverAnimation"/> plays (ball poof, the wall blowing up in a chain), then the game
/// slows to a full stop and the Game Over screen opens; Restart reloads the scene.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum LoseReason
    {
        BallLost,
        WallReached
    }

    private const string BestLevelKey = "BrickBreaker.BestLevel";

    [Header("References")]
    [SerializeField] private BallController ball;
    [SerializeField] private BrickGrid grid;
    [SerializeField] private ExperienceSystem experience;
    [SerializeField] private PowerUpManager powerUps;
    [SerializeField] private GameOverScreen gameOverScreen;
    [Tooltip("Optional: added at startup if missing.")]
    [SerializeField] private GameOverAnimation gameOverAnimation;

    [Header("Game Over")]
    [Tooltip("Seconds the game takes to slow down to a full stop before the Game Over screen opens.")]
    [SerializeField, Min(0f)] private float slowDownTime = 0.7f;

    [Header("Ball speed ramp")]
    [Tooltip("Seconds of play between two ball speed steps.")]
    [SerializeField, Min(1f)] private float speedStepInterval = 60f;
    [Tooltip("Speed added by each step, as a fraction of the start speed (0.2 = +20%). No cap for now.")]
    [SerializeField, Min(0f)] private float speedStepAmount = 0.075f;

    // Read by the run profiler (settings of the run).
    public float SpeedStepInterval => speedStepInterval;
    public float SpeedStepAmount => speedStepAmount;

    /// <summary>Raised once when the run ends.</summary>
    public event Action<LoseReason> GameOver;

    /// <summary>Raised when the ball speed ramp steps up: the step number (1, 2...) and the new speed factor (1.2, 1.4...).</summary>
    public static event Action<int, float> BallSpeedStepped;

    public bool IsGameOver { get; private set; }
    /// <summary>Seconds of actual play (stops while paused).</summary>
    public float RunTime { get; private set; }
    private bool runStarted;
    private int speedStep;
    public int BricksDestroyed { get; private set; }

    private void Awake()
    {
        // Phones run at 30 FPS by default; the ball and the animations are tuned for 60.
        Application.targetFrameRate = 60;
        // The player only touches the screen while dragging, so stop it from dimming mid-run.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        BallController.RunSpeedFactor = 1f; // the ramp restarts with the run

        // A lost ball ends the run instead of going back to the paddle.
        if (ball != null) ball.AutoReset = false;

        if (gameOverAnimation == null && !TryGetComponent(out gameOverAnimation))
            gameOverAnimation = gameObject.AddComponent<GameOverAnimation>();
    }

    private void OnEnable()
    {
        if (ball != null) ball.BallLost += HandleBallLost;
        BallController.MainChanged += HandleMainChanged;
        if (grid != null)
        {
            grid.BricksReachedPaddle += HandleWallReached;
            grid.BrickDestroyed += HandleBrickDestroyed;
        }
    }

    private void OnDisable()
    {
        if (ball != null) ball.BallLost -= HandleBallLost;
        BallController.MainChanged -= HandleMainChanged;
        if (grid != null)
        {
            grid.BricksReachedPaddle -= HandleWallReached;
            grid.BrickDestroyed -= HandleBrickDestroyed;
        }
    }

    private void Update()
    {
        // The clock starts when the ball first launches (not while it waits on the paddle).
        if (!runStarted)
        {
            foreach (BallController b in BallController.Active)
                if (b.IsFlying) { runStarted = true; break; }
        }

        if (runStarted && !IsGameOver) RunTime += Time.deltaTime; // deltaTime is 0 while paused

        // Ball speed ramp: +speedStepAmount of the start speed every speedStepInterval seconds of play, no cap.
        int step = Mathf.FloorToInt(RunTime / speedStepInterval);
        if (step > speedStep && !IsGameOver)
        {
            speedStep = step;
            float factor = 1f + speedStepAmount * step;
            BallController.RunSpeedFactor = factor;
            RunProfiler.Log("ball_speed", "step", factor, step.ToString());
            BallSpeedStepped?.Invoke(step, factor);
        }
    }

    private void HandleBallLost(BallController lostBall)
    {
        // Only losing the last ball ends the run: an extra still in play takes over as the main ball.
        BallController successor = FindSuccessor(lostBall);
        if (successor == null)
        {
            if (PowerUpStats.PhoenixReady && !PowerUpStats.BrittlePact)
            {
                RevivePhoenix(lostBall);
                return;
            }
            EndRun(LoseReason.BallLost);
            return;
        }

        lostBall.gameObject.SetActive(false); // the lost ball stays parked below the paddle otherwise

        successor.PromoteToMain();
        SetMainBall(successor);
        PlayPromoteCue(successor);
    }

    // Phoenix Ball: once per run the last ball comes back on the paddle instead of ending the run.
    private void RevivePhoenix(BallController lostBall)
    {
        PowerUpStats.PhoenixReady = false;
        lostBall.ResetToPaddle();
        foreach (TrailRenderer trail in lostBall.GetComponentsInChildren<TrailRenderer>()) trail.Clear();

        Color fire = new(1f, 0.55f, 0.15f, 1f);
        Vector3 at = lostBall.Position;
        SoundManager.Play(Sfx.BallPromote, 0.8f);
        SoftFlash.Spawn(at, fire, 2.2f, 0.4f);
        NeonFx.Ring(at, fire, 32, 6f, 0.1f, 0.45f);
        NeonFx.Burst(at, Color.Lerp(fire, Color.white, 0.3f), 24, 3.5f, 0.08f, 0.6f);
        NeonFx.Embers(at, fire, 10, 0.07f, 1.0f);
    }

    // The ball that takes a wall buff zone becomes the main ball; the former main is now an extra.
    private void HandleMainChanged(BallController previous, BallController newMain)
    {
        if (IsGameOver) return;
        SetMainBall(newMain);
        PlayPromoteCue(newMain);
    }

    private void SetMainBall(BallController newMain)
    {
        if (ball != null) ball.BallLost -= HandleBallLost;
        newMain.AutoReset = false;
        newMain.BallLost += HandleBallLost;
        ball = newMain;
        if (grid != null) grid.SetBall(newMain);
    }

    private void PlayPromoteCue(BallController successor)
    {
        // Promotion cue: the new main ball flashes gold and sends out a ring, so the player sees which one it is.
        SoundManager.Play(Sfx.BallPromote);
        Color gold = new(1f, 0.85f, 0.3f, 1f);
        NeonFx.Flash(successor.Position, Color.white, 0.9f, 0.15f);
        NeonFx.Ring(successor.Position, gold, 28, 5f, 0.09f, 0.4f);
        NeonFx.Burst(successor.Position, gold, 14, 2.5f, 0.07f, 0.45f);
    }

    private static BallController FindSuccessor(BallController lostBall)
    {
        foreach (BallController candidate in BallController.Active)
            if (candidate != lostBall && candidate.IsFlying) return candidate;
        return null;
    }
    private void HandleWallReached() => EndRun(LoseReason.WallReached);
    private void HandleBrickDestroyed(Brick brick) => BricksDestroyed++;

    /// <summary>Ends the run (also callable by debug tools or future modes).</summary>
    public void EndRun(LoseReason reason)
    {
        if (IsGameOver) return;
        IsGameOver = true;

        // Nothing else should interrupt the ending: no more level-up cards, no more wall steps.
        if (powerUps != null) powerUps.enabled = false;
        if (grid != null) grid.AdvanceEnabled = false;

        if (reason == LoseReason.BallLost) SoundManager.Play(Sfx.BallLost);

        GameOver?.Invoke(reason);
        StartCoroutine(GameOverSequence(reason));
    }

    [ContextMenu("Debug: End Run")]
    private void DebugEndRun() => EndRun(LoseReason.BallLost);

    [ContextMenu("Debug: End Run (Wall Reached)")]
    private void DebugEndRunWall() => EndRun(LoseReason.WallReached);

    private IEnumerator GameOverSequence(LoseReason reason)
    {
        if (gameOverAnimation != null) yield return gameOverAnimation.Play(reason, ball, grid);

        // The sting plays as the game winds down, after the wall's collapse.
        SoundManager.Play(Sfx.GameOver);
        float startScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        float elapsed = 0f;
        while (elapsed < slowDownTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / slowDownTime);
            Time.timeScale = Mathf.Lerp(startScale, 0f, k * k * (3f - 2f * k)); // smoothstep
            yield return null;
        }
        Time.timeScale = 0f;

        int level = experience != null ? experience.Level : 1;
        int best = PlayerPrefs.GetInt(BestLevelKey, 0);
        bool newBest = level > best;
        if (newBest)
        {
            best = level;
            PlayerPrefs.SetInt(BestLevelKey, best);
            PlayerPrefs.Save();
        }

        if (gameOverScreen != null)
            gameOverScreen.Show(reason, level, BricksDestroyed, RunTime, best, newBest, Restart);
        else
            Restart();
    }

    /// <summary>Starts a fresh run by reloading the current scene.</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
