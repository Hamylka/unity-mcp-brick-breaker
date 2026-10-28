using UnityEngine;

/// <summary>
/// Runs a <see cref="WallBuff"/> on a ball: applies it, shows a ring in the active bonus HUD, then removes it when the
/// time is up. Adding the same buff again refreshes the timer. The ball's own look (<see cref="BallVisual"/>) shows
/// the buff and is told how long is left so it can flicker before the end; a ball without one gets embers instead.
/// </summary>
[DisallowMultipleComponent]
public class BallBuffAura : MonoBehaviour
{
    private const float EmberInterval = 0.05f;

    private static int liveCount;
    private static int nextId;

    /// <summary>True while any ball carries a buff (the zone spawner waits for this to end).</summary>
    public static bool AnyActive => liveCount > 0;

    private BallController ball;
    private BallVisual visual;
    private WallBuff buff;
    private float remaining;
    private float nextEmber;
    private string hudId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => liveCount = 0;

    /// <summary>Puts <paramref name="buff"/> on <paramref name="target"/>, or refreshes it if already there.</summary>
    public static void Give(BallController target, WallBuff buff)
    {
        BallBuffAura aura = target.GetComponent<BallBuffAura>();
        if (aura == null) aura = target.gameObject.AddComponent<BallBuffAura>();
        aura.Begin(target, buff);
    }

    private void Begin(BallController target, WallBuff newBuff)
    {
        if (buff != null && buff != newBuff) buff.Remove(ball);
        else if (buff == null) liveCount++;

        ball = target;
        visual = target.GetComponent<BallVisual>();
        buff = newBuff;
        remaining = newBuff.duration;
        hudId = "wallbuff#" + nextId++;
        buff.Apply(ball);
    }

    private void Update()
    {
        // A clone of a buffed ball (multi-ball) carries this component without a buff: drop it.
        if (buff == null) { Destroy(this); return; }

        remaining -= Time.deltaTime;
        if (remaining <= 0f) { Destroy(this); return; }

        ActiveBonusTracker.Report(hudId, buff.label, buff.label, buff.color, remaining, buff.duration);

        if (visual != null)
        {
            visual.BuffTimeLeft = remaining;
        }
        else if (Time.time >= nextEmber && Time.timeScale > 0f)
        {
            nextEmber = Time.time + EmberInterval;
            NeonFx.Embers(ball.Position + Vector3.up * 0.1f, buff.color, 1, 0.08f, 0.45f);
        }
    }

    private void OnDestroy()
    {
        if (buff == null) return;
        liveCount--;
        if (ball != null) buff.Remove(ball);
        if (visual != null) visual.BuffTimeLeft = float.PositiveInfinity;

        // No effects while the scene is closing (they would spawn objects during teardown).
        if (!gameObject.scene.isLoaded) return;
        if (ball != null)
        {
            // The power leaving the ball: a burst and a ring that falls away from it.
            NeonFx.Burst(ball.Position, buff.color, 14, 2f, 0.08f, 0.35f);
            NeonFx.Ring(ball.Position, NeonGlow.Hdr(buff.color, 1.3f), 18, 2.2f, 0.07f, 0.3f);
        }
        SoundManager.Play(Sfx.BuffFade);
    }
}
