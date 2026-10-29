using UnityEngine;

/// <summary>
/// Gives an extra ball (multi-ball) a limited life: it shrinks and fades its trail during the last seconds,
/// then disappears. It also disappears if it falls past the paddle, without ending the run.
/// </summary>
[RequireComponent(typeof(BallController))]
[DisallowMultipleComponent]
public class TemporaryBall : MonoBehaviour
{
    /// <summary>The ball's hot orange, used by the HUD row and the multi-ball particles.</summary>
    public static readonly Color HudColor = new(1f, 0.55f, 0.15f, 1f);

    private static int nextHudId;

    private BallController ball;
    private string hudId; // shared by the extras of one pickup, so each pickup gets its own HUD line

    /// <summary>A new id for one multi-ball pickup, to pass to <see cref="Begin"/> for each of its extras.</summary>
    public static string NewProcId() => "multiball#" + nextHudId++;
    private float totalTime;
    private BallVisual visual;
    private TrailRenderer[] trails;
    private float[] trailWidths;
    private Vector3 startScale;
    private float remaining;
    private float fadeTime;

    /// <summary>True while the ball is shrinking away in the last seconds of its life: it no longer counts.</summary>
    public bool IsFading => ball != null && remaining <= fadeTime;

    /// <summary>Starts the countdown. The ball fades out during the last <paramref name="fadeSeconds"/>.</summary>
    public void Begin(float lifetime, float fadeSeconds, string procId, float fullTime = -1f)
    {
        remaining = lifetime;
        totalTime = fullTime > 0f ? fullTime : lifetime; // a transferred life keeps its HUD ring where it was
        hudId = procId;
        fadeTime = Mathf.Max(0.01f, fadeSeconds);
        startScale = transform.localScale;

        // A ball with a BallVisual fades its trails through LifeFade (they also change width with the wall buffs).
        visual = GetComponent<BallVisual>();
        trails = visual != null ? System.Array.Empty<TrailRenderer>() : GetComponentsInChildren<TrailRenderer>();
        trailWidths = new float[trails.Length];
        for (int i = 0; i < trails.Length; i++) trailWidths[i] = trails[i].widthMultiplier;

        ball = GetComponent<BallController>();
        ball.BallLost += HandleBallLost;
    }

    /// <summary>Hands this ball's remaining life to another ball (role swap with the main ball). This ball becomes permanent.</summary>
    public void TransferTo(BallController other)
    {
        other.gameObject.AddComponent<TemporaryBall>().Begin(remaining, fadeTime, hudId, totalTime);
        Cancel();
    }

    /// <summary>Keeps the ball for good: restores its size and trail, and removes this component.</summary>
    public void Cancel()
    {
        transform.localScale = startScale;
        if (visual != null) visual.LifeFade = 1f;
        for (int i = 0; i < trails.Length; i++) trails[i].widthMultiplier = trailWidths[i];
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (ball != null) ball.BallLost -= HandleBallLost;
    }

    private void HandleBallLost(BallController lostBall) => Destroy(gameObject);

    private void Update()
    {
        remaining -= Time.deltaTime; // scaled time: the countdown pauses with the game
        if (remaining <= 0f)
        {
            // Blinks out with a small pop rather than just vanishing.
            SoundManager.Play(Sfx.BallFade);
            NeonFx.Burst(transform.position, HudColor, 10, 2.5f, 0.07f, 0.3f);
            Destroy(gameObject);
            return;
        }
        ActiveBonusTracker.Report(hudId, hudId, "MULTI-BALL", HudColor, remaining, totalTime);
        if (remaining > fadeTime) return;

        float k = remaining / fadeTime;
        float eased = k * k * (3f - 2f * k); // smoothstep
        transform.localScale = startScale * eased;
        if (visual != null) visual.LifeFade = eased;
        for (int i = 0; i < trails.Length; i++) trails[i].widthMultiplier = trailWidths[i] * eased;
    }
}
