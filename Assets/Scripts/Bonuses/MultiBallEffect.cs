using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adds temporary extra balls that fan out from the ball in play. They live for a few seconds, then fade out.
/// Losing an extra ball never costs the run; only the main ball does.
/// The split is telegraphed: a short countdown runs on the main ball (<see cref="MultiBallCharge"/>), then the
/// extras spawn the first time the ball is heading toward the top of the screen.
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

    [Header("Telegraph")]
    [Tooltip("Countdown ticks on the ball before the split.")]
    [SerializeField, Range(1, 6)] private int countdownTicks = 3;
    [Tooltip("Seconds between two countdown ticks.")]
    [SerializeField, Range(0.1f, 1f)] private float tickInterval = 0.33f;
    [Tooltip("After the countdown, the split waits until the ball's direction points this much toward the top " +
             "of the screen (Z of the direction: 0 = sideways, 1 = straight up).")]
    [SerializeField, Range(0f, 0.95f)] private float minUpward = 0.35f;

    public int CountdownTicks => countdownTicks;
    public float TickInterval => tickInterval;
    public float MinUpward => minUpward;

    public override void Apply(BonusContext context) => MultiBallCharge.Queue(this, TemporaryBall.NewProcId());

    /// <summary>Extra balls a split of <paramref name="pickups"/> queued pickups would add now (cap included).</summary>
    public int ExtrasFor(int pickups)
    {
        int alive = 0;
        foreach (BallController ball in BallController.Active)
            if (ball != null && ball.IsExtra) alive++;
        return Mathf.Clamp(ExtrasPerPickup * pickups, 0, MaxExtras - alive);
    }

    // Twin Fission adds balls to every pickup and raises the cap by the same amount per pickup slot.
    private int ExtrasPerPickup => extraBalls + PowerUpStats.ExtraMultiBalls;
    private int MaxExtras => maxExtraBalls + PowerUpStats.ExtraMultiBalls * 3;

    /// <summary>
    /// Splits <paramref name="source"/>: spawns the extras of every queued pickup, fanned out around its direction.
    /// Each pickup keeps its own HUD line (its proc id).
    /// </summary>
    public void Split(BallController source, IReadOnlyList<string> procIds)
    {
        int count = ExtrasFor(procIds.Count);
        for (int i = 0; i < count; i++)
        {
            // Alternate right / left of the main ball's direction: +1, -1, +2, -2...
            int step = i / 2 + 1;
            float angle = (i % 2 == 0 ? step : -step) * spreadAngle;
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * source.Direction;

            string procId = procIds[Mathf.Min(i / ExtrasPerPickup, procIds.Count - 1)];
            BallController extra = source.SpawnExtra(direction);
            extra.gameObject.AddComponent<TemporaryBall>().Begin(duration * PowerUpStats.BonusDuration, fadeTime, procId);
            BallBuffAura.Inherit(source, extra); // a wall buff on the source (2x damage, Phase) follows, same time left
            NeonFx.Sparks(source.Position, direction, TemporaryBall.HudColor, 6, 6f, 25f, 0.08f, 0.25f); // a streak along each new path
        }
        if (count == 0) return;

        // The split: the main ball flashes and throws off a ring, the new balls streak out of it.
        SoundManager.Play(Sfx.MultiBallSplit);
        NeonFx.Flash(source.Position, Color.white, 0.7f, 0.12f);
        NeonFx.Ring(source.Position, TemporaryBall.HudColor, 20, 4f, 0.08f, 0.3f);
    }
}
