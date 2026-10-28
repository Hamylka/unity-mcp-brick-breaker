using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The dramatic part of losing, played by <see cref="GameManager"/> before the game slows to a stop:
/// a freeze-frame as the ball poofs, the wall trembles red, then the bricks blow up in a chain that spreads
/// from where the run was lost and speeds up as it goes, ending in one big collapse with a camera shake.
/// Bricks are killed with <see cref="Brick.BreakSilently"/>, so the finale gives no XP, drops or wall-clear.
/// Runs on scaled time (the particles do too); the freeze-frame is a short dip of the time scale.
/// </summary>
public class GameOverAnimation : MonoBehaviour
{
    [Header("Ball Poof")]
    [Tooltip("Time scale during the freeze-frame when the ball poofs.")]
    [SerializeField, Range(0f, 1f)] private float hitStopScale = 0.04f;
    [Tooltip("Real seconds the freeze-frame lasts.")]
    [SerializeField, Min(0f)] private float hitStopTime = 0.18f;
    [SerializeField] private Color poofColor = new(1f, 0.5f, 0.15f, 1f);
    [SerializeField] private Color poofTipColor = new(1f, 0.2f, 0.7f, 1f);
    [SerializeField, Range(0f, 1f)] private float poofShake = 0.45f;

    [Header("Tremor")]
    [Tooltip("Pause after the poof while the wall flashes red and shakes, before the chain starts.")]
    [SerializeField, Min(0f)] private float tremorTime = 0.45f;
    [SerializeField] private Color tremorColor = new(2.2f, 0.25f, 0.3f, 1f);
    [SerializeField, Range(0f, 1f)] private float tremorShake = 0.3f;

    [Header("Chain")]
    [Tooltip("Seconds of chain per brick; the total is clamped between Min and Max Chain Time.")]
    [SerializeField, Min(0f)] private float chainTimePerBrick = 0.025f;
    [SerializeField, Min(0f)] private float minChainTime = 0.6f;
    [SerializeField, Min(0f)] private float maxChainTime = 1.7f;
    [Tooltip("Below 1 the chain starts slow and speeds up (distance^curve maps to time).")]
    [SerializeField, Range(0.3f, 1.5f)] private float chainCurve = 0.7f;
    [Tooltip("Random spread added to each brick's timing, in seconds.")]
    [SerializeField, Min(0f)] private float chainJitter = 0.05f;
    [Tooltip("Each brick flashes white this long before it blows (the fuse running along the wall).")]
    [SerializeField, Min(0f)] private float fuseTime = 0.1f;
    [SerializeField, Range(0f, 1f)] private float shakePerBrick = 0.07f;
    [Tooltip("Every Nth brick also sends out a ring, so the chain reads as a series of blasts.")]
    [SerializeField, Min(1)] private int ringEvery = 4;

    [Header("Collapse")]
    [SerializeField] private Color collapseColor = new(1f, 0.35f, 0.75f, 1f);
    [SerializeField, Range(0f, 1f)] private float collapseShake = 0.75f;
    [Tooltip("Seconds the debris keeps flying after the collapse before the game starts slowing to a stop.")]
    [SerializeField, Min(0f)] private float settleTime = 0.45f;

    private readonly List<Brick> bricks = new();

    /// <summary>Plays the whole animation; yields until it is done.</summary>
    public IEnumerator Play(GameManager.LoseReason reason, BallController lastBall, BrickGrid grid)
    {
        Time.timeScale = 1f;
        StopGameplay(grid);

        // 1. Freeze-frame: the balls poof while time almost stops, then the burst flies out as time comes back.
        Vector3 lossPoint = lastBall != null ? lastBall.Position : Vector3.zero;
        PoofBalls();
        BrickBreakerCamera.Shake(poofShake);
        Time.timeScale = hitStopScale;
        yield return new WaitForSecondsRealtime(hitStopTime);
        Time.timeScale = 1f;

        if (grid == null) yield break;
        grid.GetLiveBricks(bricks);
        if (bricks.Count == 0) yield break;

        // 2. Tremor: every brick flashes danger red and the camera rumbles.
        SoundManager.Play(Sfx.WallTremor);
        BrickBreakerCamera.Shake(tremorShake);
        foreach (Brick brick in bricks) brick.Pulse(tremorColor, tremorTime * 0.9f);
        yield return new WaitForSeconds(tremorTime);

        // 3. Chain: from the brick where the run was lost (the one that crossed the line, or the one nearest
        // the ball), outward by distance. Early blasts are spaced out, later ones crowd together.
        bricks.RemoveAll(b => b == null || !b.IsAlive);
        if (bricks.Count == 0) yield break;
        Vector3 origin = FindOrigin(reason, lossPoint);
        bricks.Sort((a, b) => (a.transform.position - origin).sqrMagnitude
            .CompareTo((b.transform.position - origin).sqrMagnitude));

        float maxDistance = Mathf.Max(0.01f, Vector3.Distance(bricks[^1].transform.position, origin));
        float chainTime = Mathf.Clamp(bricks.Count * chainTimePerBrick, minChainTime, maxChainTime);
        var times = new float[bricks.Count];
        for (int i = 0; i < bricks.Count; i++)
        {
            float k = Vector3.Distance(bricks[i].transform.position, origin) / maxDistance;
            float jitter = i == 0 ? 0f : Random.Range(0f, chainJitter);
            times[i] = fuseTime + Mathf.Pow(k, chainCurve) * chainTime + jitter;
            bricks[i].Pulse(Color.white * 2.5f, fuseTime, times[i] - fuseTime);
        }

        Vector3 center = Vector3.zero;
        int blown = 0;
        float elapsed = 0f;
        int next = 0;
        var order = new int[bricks.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        System.Array.Sort(times, order); // jitter can swap neighbours: blow them in time order

        while (next < order.Length)
        {
            elapsed += Time.deltaTime;
            while (next < order.Length && times[next] <= elapsed)
            {
                Brick brick = bricks[order[next]];
                float progress = (float)next / Mathf.Max(1, order.Length - 1);
                if (brick != null && brick.IsAlive)
                {
                    center += brick.transform.position;
                    blown++;
                    Blow(brick, progress, blown);
                }
                next++;
            }
            yield return null;
        }

        // 4. Collapse: one big blast over the middle of what was the wall.
        if (blown > 0) Collapse(center / blown);
        yield return new WaitForSeconds(settleTime);
    }

    // Nothing may keep playing or score during the finale: area effects (burns), falling bonus items.
    private static void StopGameplay(BrickGrid grid)
    {
        if (grid != null && grid.TryGetComponent(out AreaDamage area)) area.enabled = false;

        foreach (BonusItem item in FindObjectsByType<BonusItem>())
        {
            Color color = item.Definition != null ? item.Definition.Color : Color.white;
            NeonFx.Burst(item.transform.position, color, 10, 2.5f, 0.08f, 0.4f);
            Destroy(item.gameObject);
        }
    }

    // Every ball still around (the lost one parked under the paddle, or the ones in play when the wall arrived)
    // bursts into a white flash and an orange-to-magenta puff, then disappears.
    private void PoofBalls()
    {
        bool any = false;
        foreach (BallController ball in FindObjectsByType<BallController>())
        {
            Vector3 p = ball.Position;
            // Square flashes only at pixel size (a hot core); the big light is a soft round glow.
            NeonFx.Flash(p, Color.white * 2f, 0.45f, 0.15f);
            SoftFlash.Spawn(p, Color.white * 1.5f, 1.6f, 0.3f);
            SoftFlash.Spawn(p, poofColor * 2f, 3.2f, 0.5f);
            NeonFx.Ring(p, poofColor * 1.5f, 32, 6f, 0.11f, 0.45f);
            NeonFx.Burst(p, poofColor * 1.5f, 26, 5f, 0.12f, 0.6f);
            NeonFx.Burst(p, poofTipColor * 1.5f, 14, 3f, 0.09f, 0.8f);
            NeonFx.Embers(p, poofColor, 6, 0.08f, 1.2f);
            ball.gameObject.SetActive(false);
            any = true;
        }
        if (any) SoundManager.Play(Sfx.BallPoof);
    }

    private Vector3 FindOrigin(GameManager.LoseReason reason, Vector3 lossPoint)
    {
        Brick best = bricks[0];
        float bestScore = float.MaxValue;
        foreach (Brick brick in bricks)
        {
            Vector3 p = brick.transform.position;
            // Wall reached: the brick closest to the paddle (lowest z); ball lost: the brick nearest the ball.
            float score = reason == GameManager.LoseReason.WallReached
                ? p.z + Mathf.Abs(p.x) * 0.01f
                : (p - lossPoint).sqrMagnitude;
            if (score < bestScore)
            {
                bestScore = score;
                best = brick;
            }
        }
        return best.transform.position;
    }

    // One brick blows: flash, burst and embers in its color, a ring every few bricks, a blast that rises in pitch
    // as the chain goes on, and a kick of camera shake.
    private void Blow(Brick brick, float progress, int index)
    {
        Color color = brick.HpColor;
        Vector3 p = brick.transform.position;
        float width = brick.Size.x;

        NeonFx.Flash(p, Color.Lerp(color, Color.white, 0.5f) * 2f, 0.35f, 0.12f);
        SoftFlash.Spawn(p, color * 1.6f, width * 2.4f, 0.32f);
        NeonFx.Burst(p, color * 1.6f, 12, 4.5f, 0.11f, 0.65f);
        NeonFx.Embers(p, color, 2, 0.07f, 1f);
        if (index % ringEvery == 1 || ringEvery == 1) NeonFx.Ring(p, color * 1.4f, 20, 4f, 0.09f, 0.35f);

        brick.BreakSilently();
        SoundManager.Play(Sfx.BrickBlast, Mathf.Lerp(0.8f, 1.3f, progress));
        BrickBreakerCamera.Shake(shakePerBrick);
    }

    private void Collapse(Vector3 center)
    {
        NeonFx.Flash(center, Color.white * 2f, 0.6f, 0.18f);
        SoftFlash.Spawn(center, Color.white * 1.4f, 3.5f, 0.4f);
        SoftFlash.Spawn(center, collapseColor * 1.8f, 8f, 0.7f);
        NeonFx.Ring(center, collapseColor * 1.6f, 48, 9f, 0.14f, 0.6f);
        NeonFx.Ring(center, poofColor * 1.6f, 36, 5.5f, 0.12f, 0.7f);
        NeonFx.Burst(center, collapseColor * 1.5f, 40, 7f, 0.13f, 0.9f);
        NeonFx.Embers(center, poofColor, 14, 0.09f, 1.6f);
        SoundManager.Play(Sfx.WallCollapse);
        BrickBreakerCamera.Shake(collapseShake);
    }
}
