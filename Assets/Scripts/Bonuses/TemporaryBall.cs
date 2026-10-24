using UnityEngine;

/// <summary>
/// Gives an extra ball (multi-ball) a limited life: it shrinks and fades its trail during the last seconds,
/// then disappears. It also disappears if it falls past the paddle, without ending the run.
/// </summary>
[RequireComponent(typeof(BallController))]
[DisallowMultipleComponent]
public class TemporaryBall : MonoBehaviour
{
    private BallController ball;
    private TrailRenderer[] trails;
    private float[] trailWidths;
    private Vector3 startScale;
    private float remaining;
    private float fadeTime;

    /// <summary>Starts the countdown. The ball fades out during the last <paramref name="fadeSeconds"/>.</summary>
    public void Begin(float lifetime, float fadeSeconds)
    {
        remaining = lifetime;
        fadeTime = Mathf.Max(0.01f, fadeSeconds);
        startScale = transform.localScale;

        trails = GetComponentsInChildren<TrailRenderer>();
        trailWidths = new float[trails.Length];
        for (int i = 0; i < trails.Length; i++) trailWidths[i] = trails[i].widthMultiplier;

        ball = GetComponent<BallController>();
        ball.BallLost += HandleBallLost;
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
            Destroy(gameObject);
            return;
        }
        if (remaining > fadeTime) return;

        float k = remaining / fadeTime;
        float eased = k * k * (3f - 2f * k); // smoothstep
        transform.localScale = startScale * eased;
        for (int i = 0; i < trails.Length; i++) trails[i].widthMultiplier = trailWidths[i] * eased;
    }
}
