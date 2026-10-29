using UnityEngine;

/// <summary>
/// Paddle Cannon card: every paddle bounce fires instant bolts straight ahead from lanes spread across the paddle's
/// width. One lane at the first pick, one more per pick up to <see cref="MaxLanes"/>, then +25% bolt damage per pick.
/// Each bolt deals a share of the bouncing ball's damage to the first brick in its lane.
/// </summary>
public static class PaddleCannon
{
    public const int MaxLanes = 7;
    private const float DamageShare = 0.5f;
    private const float ExtraDamagePerPick = 0.25f;
    private const float MaxRange = 40f;
    private static readonly Color BoltColor = new(0.45f, 0.95f, 1f);
    private static readonly RaycastHit[] hits = new RaycastHit[32];

    public static int LanesFor(int picks) => Mathf.Clamp(picks, 0, MaxLanes);
    public static float DamageFactorFor(int picks) => 1f + ExtraDamagePerPick * Mathf.Max(0, picks - MaxLanes);

    /// <summary>Fires the volley from the paddle's front edge. Call it when the ball bounces off the paddle.</summary>
    public static void Fire(Bounds paddle, BallController ball)
    {
        int picks = PowerUpStats.CannonPicks;
        int lanes = LanesFor(picks);
        if (lanes == 0) return;

        float damage = ball.AttackDamage * DamageShare * DamageFactorFor(picks);
        float width = paddle.size.x;
        for (int i = 0; i < lanes; i++)
        {
            float x = paddle.min.x + width * (i + 0.5f) / lanes;
            var origin = new Vector3(x, paddle.center.y, paddle.max.z + 0.05f);
            FireLane(origin, damage);
        }
        SoundManager.Play(Sfx.LaserShot, 1.3f);
    }

    private static void FireLane(Vector3 origin, float damage)
    {
        float nearest = MaxRange;
        BrickStack target = null;
        int count = Physics.RaycastNonAlloc(origin, Vector3.forward, hits, MaxRange, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.distance >= nearest) continue;
            if (hit.collider.GetComponentInParent<BallController>() != null) continue;
            if (hit.collider.GetComponentInParent<PaddleController>() != null) continue;
            nearest = hit.distance;
            target = hit.collider.GetComponentInParent<BrickStack>();
        }

        Vector3 end = origin + Vector3.forward * nearest;
        ChainBolt.Spawn(origin, end, BoltColor * 1.8f, 0.01f, 0.12f, 0.06f);
        NeonFx.Flash(origin, BoltColor, 0.25f, 0.06f);
        if (nearest < MaxRange) NeonFx.Sparks(end, Vector3.back, BoltColor, 5, 4f, 100f, 0.07f, 0.2f);

        Brick brick = target != null ? target.Bottom : null;
        if (brick == null) return;
        brick.DamageCarry += damage; // fractions pile up per brick, like the other secondary damage
        int whole = Mathf.FloorToInt(brick.DamageCarry + 0.001f);
        if (whole <= 0) return;
        brick.DamageCarry = Mathf.Max(0f, brick.DamageCarry - whole);
        using var tag = DamageTag.Begin(DamageSource.PaddleCannon);
        target.TakeDamage(whole);
    }
}
