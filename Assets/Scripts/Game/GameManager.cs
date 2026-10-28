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

    /// <summary>Raised once when the run ends.</summary>
    public event Action<LoseReason> GameOver;

    public bool IsGameOver { get; private set; }
    /// <summary>Seconds of actual play (stops while paused).</summary>
    public float RunTime { get; private set; }
    public int BricksDestroyed { get; private set; }

    private void Awake()
    {
        // Phones run at 30 FPS by default; the ball and the animations are tuned for 60.
        Application.targetFrameRate = 60;
        // The player only touches the screen while dragging, so stop it from dimming mid-run.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // A lost ball ends the run instead of going back to the paddle.
        if (ball != null) ball.AutoReset = false;

        if (gameOverAnimation == null && !TryGetComponent(out gameOverAnimation))
            gameOverAnimation = gameObject.AddComponent<GameOverAnimation>();
    }

    private void OnEnable()
    {
        if (ball != null) ball.BallLost += HandleBallLost;
        if (grid != null)
        {
            grid.BricksReachedPaddle += HandleWallReached;
            grid.BrickDestroyed += HandleBrickDestroyed;
        }
    }

    private void OnDisable()
    {
        if (ball != null) ball.BallLost -= HandleBallLost;
        if (grid != null)
        {
            grid.BricksReachedPaddle -= HandleWallReached;
            grid.BrickDestroyed -= HandleBrickDestroyed;
        }
    }

    private void Update()
    {
        if (!IsGameOver) RunTime += Time.deltaTime; // deltaTime is 0 while paused
    }

    private void HandleBallLost(BallController lostBall)
    {
        // Only losing the last ball ends the run: an extra still in play takes over as the main ball.
        BallController successor = FindSuccessor(lostBall);
        if (successor == null)
        {
            EndRun(LoseReason.BallLost);
            return;
        }

        ball.BallLost -= HandleBallLost;
        lostBall.gameObject.SetActive(false); // the lost ball stays parked below the paddle otherwise

        successor.PromoteToMain();
        successor.AutoReset = false;
        successor.BallLost += HandleBallLost;
        ball = successor;
        if (grid != null) grid.SetBall(successor);

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
