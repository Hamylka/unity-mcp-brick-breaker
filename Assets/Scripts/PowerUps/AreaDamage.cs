using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime home of every area-of-effect power-up. Lives on the <see cref="BrickGrid"/> object (created on first use
/// by <see cref="For"/>, so a scene reload starts a run clean) and listens to <see cref="BrickGrid.BrickHit"/>.
/// Power-up effects only raise its stats; all the geometry (neighbours, columns, rows) is computed here from brick
/// positions. Damage dealt by this class never triggers area effects again, so nothing can chain forever.
/// Splash damage still raises BrickHit, so it gives XP like any other damage (only for HP actually removed).
/// </summary>
public class AreaDamage : MonoBehaviour
{
    private static readonly Color ShockColor = new(1f, 0.35f, 0.9f);
    private static readonly Color ChainColor = new(0.55f, 0.95f, 1f);
    private static readonly Color PierceColor = new(1f, 0.85f, 0.35f);
    private static readonly Color BlastColor = new(1f, 0.5f, 0.12f);
    private static readonly Color CleaveColor = new(0.8f, 1f, 1f);
    private static readonly Color BurnColor = new(1f, 0.3f, 0.08f);

    // Seconds for the Cleave light to travel one brick along the row.
    private const float CleaveWaveDelay = 0.035f;

    [Header("Geometry (in brick sizes)")]
    [Tooltip("Reach of Shockwave and Meltdown spread: 1 = the four direct neighbours.")]
    [SerializeField, Min(0.5f)] private float neighbourRadius = 1.15f;
    [Tooltip("Reach of Volatile Bricks: 1.5 = the full 3x3 block around the brick.")]
    [SerializeField, Min(0.5f)] private float blastRadius = 1.5f;
    [Tooltip("Furthest an arc of Chain Lightning can jump, in world units.")]
    [SerializeField, Min(0.5f)] private float chainRange = 2.2f;

    [Header("Cleave")]
    [Tooltip("Damage of the row sweep, as a share of the ball's hit damage (1.5 = 150%).")]
    [SerializeField, Min(0f)] private float cleaveDamagePercent = 1.5f;

    [Header("Meltdown")]
    [Tooltip("Damage of one tick, as a share of the burning brick's maximum HP (0.2 = 20%).")]
    [SerializeField, Min(0f)] private float burnPercentMaxHp = 0.2f;
    [SerializeField, Min(1)] private int burnTicks = 3;
    [SerializeField, Min(0.1f)] private float burnTickInterval = 1f;

    private sealed class Burn
    {
        public Brick Brick;
        public int TicksLeft;
        public float Timer;
    }

    private BrickGrid grid;
    private bool applying;
    private float splashPercent;
    private int chainJumps;
    private float chainPercent;
    private int pierceDepth;
    private float blastPercent;
    private int cleaveEvery;
    private int cleavePicks;
    private int cleaveCounter;
    private bool meltdown;

    private readonly List<Brick> live = new();
    private readonly List<Brick> targets = new();
    private readonly HashSet<Brick> visited = new();
    private readonly List<Burn> burns = new();
    private readonly List<Burn> newBurns = new();

    public float SplashPercent => splashPercent;
    public int ChainJumps => chainJumps;
    public int PierceDepth => pierceDepth;
    public float BlastPercent => blastPercent;
    public float ChainPercent => chainPercent;
    public int CleaveEvery => cleaveEvery;
    /// <summary>Hits counted toward the next Cleave sweep (0 to CleaveEvery - 1).</summary>
    public int CleaveCharge => cleaveCounter;

    /// <summary>Raised when a Cleave sweep goes off (the HUD meter flashes on it).</summary>
    public event System.Action CleaveFired;
    public bool Meltdown => meltdown;

    /// <summary>The area-damage component of this grid, added on first use.</summary>
    public static AreaDamage For(BrickGrid grid)
    {
        if (grid == null) return null;
        return grid.TryGetComponent(out AreaDamage existing) ? existing : grid.gameObject.AddComponent<AreaDamage>();
    }

    public void AddSplash(float percent) => splashPercent += Mathf.Max(0f, percent);
    /// <summary>First pick: 2 arcs. Each further pick adds one. Arc damage is a fixed share of the ball's hit damage (0.2 = 20%), it does not stack.</summary>
    public void AddChain(float percent)
    {
        chainJumps = chainJumps == 0 ? 2 : chainJumps + 1;
        chainPercent = Mathf.Max(0f, percent);
    }

    public void AddPierce(int depth) => pierceDepth += Mathf.Max(0, depth);
    /// <summary>Raises the blast damage, as a share of the ball's hit damage (0.5 = 50%).</summary>
    public void AddBlast(float percent) => blastPercent += Mathf.Max(0f, percent);
    /// <summary>First pick: every 6th hit sweeps the row. Each further pick shortens that by one, down to 3.</summary>
    public void AddCleave() => cleaveEvery = CleaveEveryFor(++cleavePicks);
    /// <summary>Hits needed per Cleave sweep after this many picks (0 = no Cleave).</summary>
    public static int CleaveEveryFor(int picks) => picks <= 0 ? 0 : Mathf.Max(3, 7 - picks);
    public void EnableMeltdown() => meltdown = true;

    private void Awake() => grid = GetComponent<BrickGrid>();

    private void OnEnable()
    {
        if (grid != null) grid.BrickHit += HandleBrickHit;
    }

    private void OnDisable()
    {
        if (grid != null) grid.BrickHit -= HandleBrickHit;
    }

    private void Update()
    {
        // Fresh fires join the list here: they used to be merged only after a tick, so the first one never started.
        if (newBurns.Count > 0)
        {
            burns.AddRange(newBurns);
            newBurns.Clear();
        }
        if (burns.Count > 0) TickBurns(Time.deltaTime);
    }

    private void HandleBrickHit(Brick brick, int dealt)
    {
        if (applying || dealt <= 0 || brick == null) return;

        applying = true;
        try
        {
            Vector3 origin = brick.transform.position;
            bool killed = !brick.IsAlive;

            if (splashPercent > 0f) Splash(brick, dealt);
            if (pierceDepth > 0) Pierce(brick, Mathf.Max(1, dealt / 2));
            if (cleaveEvery > 0 && ++cleaveCounter >= cleaveEvery)
            {
                cleaveCounter = 0;
                Cleave(brick);
                CleaveFired?.Invoke();
            }

            if (chainJumps > 0) Chain(brick);

            if (killed)
            {
                if (blastPercent > 0f) Blast(origin);
                if (burns.Exists(b => b.Brick == brick)) SpreadBurn(brick);
            }
            else if (meltdown)
            {
                Ignite(brick);
            }
        }
        finally
        {
            applying = false;
        }
    }

    // Shockwave: a share of the damage dealt goes to the neighbours. Fractions pile up on each brick
    // (DamageCarry), so even 25% of a 1-damage ball removes an HP after a few hits.
    private void Splash(Brick source, int dealt)
    {
        using var tag = DamageTag.Begin(DamageSource.Shockwave);
        Collect(source.transform.position, neighbourRadius, source);
        foreach (Brick neighbour in targets)
        {
            // Every neighbour jolts and sparks on every hit, even when the fraction has not yet added up to a whole HP.
            neighbour.Pulse(ShockColor * 1.4f, 0.2f, 0.03f);
            NeonFx.Burst(neighbour.transform.position, ShockColor, 3, 1.5f, 0.06f, 0.25f);
            DealFraction(neighbour, dealt * splashPercent);
        }
        if (targets.Count == 0) return;
        NeonFx.Ring(source.transform.position, ShockColor, 18, 3f, 0.08f, 0.25f);
        SoundManager.Play(Sfx.Shockwave, Random.Range(0.95f, 1.05f));
    }

    // Piercing Ball: the same shot carries on into the next bricks behind, in the same column.
    private void Pierce(Brick source, int damage)
    {
        using var tag = DamageTag.Begin(DamageSource.Piercing);
        grid.GetLiveBricks(live);
        Vector3 origin = source.transform.position;
        float halfWidth = grid.BrickWidth * 0.5f;
        float halfDepth = grid.CellDepth * 0.5f;

        targets.Clear();
        foreach (Brick candidate in live)
        {
            if (candidate == source) continue;
            Vector3 d = candidate.transform.position - origin;
            if (Mathf.Abs(d.x) < halfWidth && d.z > halfDepth) targets.Add(candidate);
        }
        targets.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));

        int count = Mathf.Min(targets.Count, pierceDepth);
        if (count == 0) return;

        // One straight gold beam from the hit brick to the last one pierced, each pierced brick lighting up in turn.
        Vector3 end = targets[count - 1].transform.position;
        ChainBolt.Spawn(origin, end, PierceColor * 1.6f, 0.015f, 0.18f, 0.16f);
        NeonFx.Line(origin, end, Vector3.forward, PierceColor, 6 * count, 1.5f, 0.06f, 0.25f);
        for (int i = 0; i < count; i++)
        {
            Brick target = targets[i];
            target.Pulse(PierceColor * 1.5f, 0.2f, 0.03f * (i + 1));
            NeonFx.Flash(target.transform.position, PierceColor, 0.45f, 0.1f);
            target.TakeHit(damage);
        }
        SoundManager.Play(Sfx.PierceZip, 1f + 0.06f * (count - 1));
    }

    // Cleave: a sweep along the whole row of the brick that was hit.
    private void Cleave(Brick source)
    {
        using var tag = DamageTag.Begin(DamageSource.Cleave);
        grid.GetLiveBricks(live);
        float rowZ = source.transform.position.z;
        float halfDepth = grid.CellDepth * 0.5f;

        Vector3 origin = source.transform.position;
        float damage = HitDamage() * cleaveDamagePercent;
        float leftX = origin.x, rightX = origin.x;
        int swept = 0;
        foreach (Brick candidate in live)
        {
            if (candidate == source || Mathf.Abs(candidate.transform.position.z - rowZ) > halfDepth) continue;
            swept++;
            Vector3 p = candidate.transform.position;
            leftX = Mathf.Min(leftX, p.x);
            rightX = Mathf.Max(rightX, p.x);

            // The damage lands now (XP stays in this frame); the light runs outward from the hit brick like a blade.
            float bricksAway = Mathf.Abs(p.x - origin.x) / grid.BrickWidth;
            candidate.Pulse(CleaveColor * 2f, 0.25f, bricksAway * CleaveWaveDelay);
            NeonFx.Sparks(p, Vector3.up, CleaveColor, 3, 3f, 40f, 0.07f, 0.25f);
            DealFraction(candidate, damage);
        }
        if (swept == 0) return;

        // Two blades out of the hit brick to both row ends, a flash and sparks thrown sideways along the row.
        float halfWidth = grid.BrickWidth * 0.5f;
        Vector3 left = new(leftX - halfWidth, origin.y, origin.z);
        Vector3 right = new(rightX + halfWidth, origin.y, origin.z);
        if (leftX < origin.x) ChainBolt.Spawn(origin, left, CleaveColor * 1.8f, 0.02f, 0.25f, 0.22f);
        if (rightX > origin.x) ChainBolt.Spawn(origin, right, CleaveColor * 1.8f, 0.02f, 0.25f, 0.22f);
        NeonFx.Line(left, right, Vector3.up, CleaveColor, 10 + swept * 4, 2.5f, 0.07f, 0.3f);
        NeonFx.Flash(origin, Color.white, 0.9f, 0.15f);
        SoundManager.Play(Sfx.CleaveSweep);
    }

    // Volatile Bricks: a destroyed brick blows up its 3x3 block.
    private void Blast(Vector3 origin)
    {
        using var tag = DamageTag.Begin(DamageSource.Volatile);
        Collect(origin, blastRadius, null);
        // White-hot core, orange fireball, a shockwave ring and embers that hang in the air.
        NeonFx.Flash(origin, Color.white * 1.5f, 1.1f, 0.1f);
        NeonFx.Flash(origin, BlastColor * 1.5f, 1.8f, 0.22f);
        NeonFx.Ring(origin, BlastColor, 32, 5f, 0.11f, 0.35f);
        NeonFx.Burst(origin, BlastColor, 20, 3.5f, 0.1f, 0.45f);
        NeonFx.Embers(origin, BurnColor, 6, 0.08f, 0.9f);
        SoundManager.Play(Sfx.VolatileBlast, Random.Range(0.95f, 1.05f));

        float damage = HitDamage() * blastPercent;
        foreach (Brick neighbour in targets)
        {
            float bricksAway = (neighbour.transform.position - origin).magnitude / grid.BrickWidth;
            neighbour.Pulse(BlastColor * 1.8f, 0.3f, 0.02f + bricksAway * 0.03f); // the blast reaches the far corners last
            DealFraction(neighbour, damage);
        }
    }

    // Chain Lightning: every time a brick is hit, an arc leaps from it to each of the 'chainJumps' nearest bricks
    // in range, dealing a share of the ball's current damage (fractions carry per brick like the other effects).
    private void Chain(Brick source)
    {
        using var tag = DamageTag.Begin(DamageSource.Chain);
        Vector3 from = source.transform.position;
        visited.Clear();
        visited.Add(source);

        float damage = HitDamage() * chainPercent;
        bool anyArc = false;
        for (int jump = 0; jump < chainJumps; jump++)
        {
            Brick next = NearestUnvisited(from);
            if (next == null) break;

            visited.Add(next);
            Vector3 to = next.transform.position;
            ChainBolt.Spawn(from, to, ChainColor);
            next.Pulse(ChainColor * 1.8f, 0.18f);
            NeonFx.Flash(to, ChainColor, 0.5f, 0.12f);
            NeonFx.Sparks(to, Vector3.up, ChainColor, 5, 3f, 60f, 0.07f, 0.25f);
            DealFraction(next, damage);
            anyArc = true;
        }
        if (!anyArc) return;
        NeonFx.Flash(from, Color.Lerp(ChainColor, Color.white, 0.5f), 0.4f, 0.08f); // the spark the arcs leave from
        SoundManager.Play(Sfx.ChainZap);
    }

    private Brick NearestUnvisited(Vector3 from)
    {
        grid.GetLiveBricks(live);
        Brick best = null;
        float bestSqr = chainRange * chainRange;
        foreach (Brick candidate in live)
        {
            if (visited.Contains(candidate)) continue;
            float sqr = (candidate.transform.position - from).sqrMagnitude;
            if (sqr > bestSqr) continue;
            bestSqr = sqr;
            best = candidate;
        }
        return best;
    }

    // Meltdown: burning bricks lose 1 HP per tick, and a burning brick that dies sets its neighbours alight.
    private void Ignite(Brick brick)
    {
        foreach (Burn existing in burns)
        {
            if (existing.Brick != brick) continue;
            existing.TicksLeft = burnTicks;
            return;
        }
        foreach (Burn pending in newBurns)
            if (pending.Brick == brick) return;

        newBurns.Add(new Burn { Brick = brick, TicksLeft = burnTicks, Timer = burnTickInterval });
        brick.Burning = true;
        brick.Pulse(BurnColor * 2f, 0.25f);
        NeonFx.Flash(brick.transform.position, BurnColor, 0.6f, 0.15f);
        NeonFx.Embers(brick.transform.position, BurnColor, 4, 0.08f, 0.7f);
        SoundManager.Play(Sfx.BurnIgnite, Random.Range(0.95f, 1.1f));
    }

    private void SpreadBurn(Brick dead)
    {
        burns.RemoveAll(b => b.Brick == dead);
        Collect(dead.transform.position, neighbourRadius, dead);
        Vector3 at = dead.transform.position;
        NeonFx.Burst(at, BurnColor, 12, 2.5f, 0.09f, 0.45f);
        NeonFx.Ring(at, BurnColor, 14, 2.5f, 0.08f, 0.3f);
        foreach (Brick neighbour in targets) Ignite(neighbour);
        if (targets.Count > 0) SoundManager.Play(Sfx.BurnIgnite, 0.8f); // lower: the fire jumping on
    }

    private void TickBurns(float dt)
    {
        using var tag = DamageTag.Begin(DamageSource.Meltdown);
        applying = true;
        try
        {
            for (int i = burns.Count - 1; i >= 0; i--)
            {
                Burn burn = burns[i];
                if (burn.Brick == null || !burn.Brick.IsAlive)
                {
                    burns.RemoveAt(i);
                    continue;
                }

                burn.Timer -= dt;
                if (burn.Timer > 0f) continue;
                burn.Timer += burnTickInterval;
                burn.TicksLeft--;

                NeonFx.Embers(burn.Brick.transform.position, BurnColor, 3, 0.07f, 0.6f);
                SoundManager.Play(Sfx.BurnTick);
                DealFraction(burn.Brick, burn.Brick.MaxHitPoints * burnPercentMaxHp);

                if (!burn.Brick.IsAlive)
                {
                    SpreadBurn(burn.Brick);
                }
                else if (burn.TicksLeft <= 0)
                {
                    burn.Brick.Burning = false;
                    burns.RemoveAt(i);
                }
            }
        }
        finally
        {
            applying = false;
        }
        burns.AddRange(newBurns);
        newBurns.Clear();
    }

    // The damage of the ball's current hit (the strongest ball in play): percent-based effects scale from it.
    private static int HitDamage()
    {
        int best = 1;
        foreach (BallController ball in BallController.Active) best = Mathf.Max(best, ball.Damage);
        return best;
    }

    // Fractions pile up on each brick (DamageCarry) until they make a whole HP, so low percentages still count.
    private static void DealFraction(Brick target, float damage)
    {
        target.DamageCarry += damage;
        int whole = Mathf.FloorToInt(target.DamageCarry + 0.001f);
        if (whole <= 0) return;
        target.DamageCarry = Mathf.Max(0f, target.DamageCarry - whole);
        target.TakeHit(whole);
    }

    // Fills 'targets' with the live bricks within 'radius' brick sizes of a point (distance measured in bricks:
    // 1 = the neighbour to the side or in front/behind).
    private void Collect(Vector3 center, float radius, Brick exclude)
    {
        grid.GetLiveBricks(live);
        targets.Clear();
        float width = grid.BrickWidth;
        float depth = grid.CellDepth;
        foreach (Brick candidate in live)
        {
            if (candidate == exclude) continue;
            Vector3 d = candidate.transform.position - center;
            float nx = d.x / width;
            float nz = d.z / depth;
            if (nx * nx + nz * nz <= radius * radius) targets.Add(candidate);
        }
    }
}
