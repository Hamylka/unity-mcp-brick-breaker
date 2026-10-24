using UnityEngine;

/// <summary>
/// Adds temporary extra balls that fan out from the ball in play. They live for a few seconds, then fade out.
/// Losing an extra ball never costs the run; only the main ball does.
/// </summary>
[CreateAssetMenu(fileName = "MultiBallEffect", menuName = "BrickBreaker/Bonus Effects/Multi-Ball")]
public class MultiBallEffect : BonusEffect
{
    [Tooltip("Balls added each time the bonus is caught.")]
    [SerializeField, Min(1)] private int extraBalls = 2;
    [Tooltip("Seconds an extra ball stays in play.")]
    [SerializeField, Min(1f)] private float duration = 10f;
    [Tooltip("Seconds at the end of the life during which the ball fades out.")]
    [SerializeField, Min(0.1f)] private float fadeTime = 2f;
    [Tooltip("Angle between neighbouring balls, in degrees.")]
    [SerializeField, Range(5f, 60f)] private float spreadAngle = 25f;
    [Tooltip("Upper limit of extra balls alive at once (catching several items stacks up to this).")]
    [SerializeField, Min(1)] private int maxExtraBalls = 6;

    public override void Apply(BonusContext context)
    {
        // Copy from the main ball, as it carries the run's speed and damage upgrades.
        BallController source = null;
        int alive = 0;
        foreach (BallController ball in context.Balls)
        {
            if (ball.IsExtra) alive++;
            else if (ball.IsFlying) source = ball;
        }
        if (source == null) return;

        int count = Mathf.Min(extraBalls, maxExtraBalls - alive);
        for (int i = 0; i < count; i++)
        {
            // Alternate right / left of the main ball's direction: +1, -1, +2, -2...
            int step = i / 2 + 1;
            float angle = (i % 2 == 0 ? step : -step) * spreadAngle;
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * source.Direction;

            BallController extra = source.SpawnExtra(direction);
            extra.gameObject.AddComponent<TemporaryBall>().Begin(duration, fadeTime);
        }
        if (count > 0) SoundManager.Play(Sfx.OrbCollect);
    }
}
