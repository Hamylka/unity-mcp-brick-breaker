using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the run: tracks a few stats and ends the run when the ball is lost or the wall reaches the lose line.
/// On game over the game slows to a full stop, then the Game Over screen opens; Restart reloads the scene.
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
        // A lost ball ends the run instead of going back to the paddle.
        if (ball != null) ball.AutoReset = false;
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

    private void HandleBallLost(BallController lostBall) => EndRun(LoseReason.BallLost);
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
        SoundManager.Play(Sfx.GameOver);

        GameOver?.Invoke(reason);
        StartCoroutine(GameOverSequence(reason));
    }

    [ContextMenu("Debug: End Run")]
    private void DebugEndRun() => EndRun(LoseReason.BallLost);

    private IEnumerator GameOverSequence(LoseReason reason)
    {
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
