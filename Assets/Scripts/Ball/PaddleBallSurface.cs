using UnityEngine;

/// <summary>
/// Paddle bounce rules: the exit angle depends on where the ball hits.
/// Center = straight ahead, edges = sharp angle toward that side.
/// A slingshot snap (see <see cref="PaddleController.SlingshotPower"/>) adds a temporary speed boost.
/// Put this on the same object as the paddle collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PaddleBallSurface : BallSurface
{
    [Tooltip("Exit angle from straight ahead when hitting the very edge, in degrees.")]
    [SerializeField, Range(10f, 80f)] private float maxBounceAngle = 60f;
    [Tooltip("Speed boost at full slingshot power (0.6 = +60%).")]
    [SerializeField, Min(0f)] private float slingshotSpeedBonus = 0.6f;
    [SerializeField] private PaddleController paddle;

    private void Awake()
    {
        if (paddle == null) paddle = GetComponent<PaddleController>();
    }

    public override void OnBallHit(BallController ball, Collision collision)
    {
        Bounds bounds = collision.collider.bounds;

        // Ignore hits from behind the paddle's center: keep the default reflection.
        if (ball.Position.z < bounds.center.z) return;

        float offset = Mathf.Clamp((ball.Position.x - bounds.center.x) / bounds.extents.x, -1f, 1f);
        float angle = offset * maxBounceAngle * Mathf.Deg2Rad;
        ball.SetDirection(new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)));

        if (paddle != null && slingshotSpeedBonus > 0f)
            ball.AddSpeedBoost(paddle.SlingshotPower * slingshotSpeedBonus);
    }
}
