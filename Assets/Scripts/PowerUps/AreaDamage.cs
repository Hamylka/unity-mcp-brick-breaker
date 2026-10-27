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
    private int cleaveCounter;
    private bool meltdown;

    private readonly List<Brick> live = new();
    private readonly List<Brick> targets = new();
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
    /// <summary>Adds arcs and raises the arc damage, as a share of the ball's hit damage (0.2 = 20%).</summary>
    public void AddChain(int jumps, float percent)
    {
        chainJumps += Mathf.Max(0, jumps);
        chainPercent += Mathf.Max(0f, percent);
    }

    public void AddPierce(int depth) => pierceDepth += Mathf.Max(0, depth);
    /// <summary>Raises the blast damage, as a share of the ball's hit damage (0.5 = 50%).</summary>
    public void AddBlast(float percent) => blastPercent += Mathf.Max(0f, percent);
    /// <summary>First pick: every 6th hit sweeps the row. Each further pick shortens that by one, down to 3.</summary>
    public void AddCleave() => cleaveEvery = cleaveEvery == 0 ? 6 : Mathf.Max(3, cleaveEvery - 1);
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

            if (killed)
            {
                if (blastPercent > 0f) Blast(origin);
                if (chainJumps > 0) Chain(brick);
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
        Collect(source.transform.position, neighbourRadius, source);
        foreach (Brick neighbour in targets)
        {
            // Every neighbour sparks on every hit, even when the fraction has not yet added up to a whole HP.
            NeonFx.Burst(neighbour.transform.position, ShockColor, 3, 1.5f, 0.06f, 0.25f);
            DealFraction(neighbour, dealt * splashPercent);
        }
        if (targets.Count > 0) NeonFx.Ring(source.transform.position, ShockColor, 14, 2.5f, 0.07f, 0.25f);
    }

    // Piercing Ball: the same shot carries on into the next bricks behind, in the same column.
    private void Pierce(Brick source, int damage)
    {
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

        Vector3 previous = origin;
        for (int i = 0; i < targets.Count && i < pierceDepth; i++)
        {
            Vector3 position = targets[i].transform.position;
            NeonFx.Line(previous, position, Vector3.forward, PierceColor, 8, 2f, 0.07f, 0.3f);
            previous = position;
            targets[i].TakeHit(damage);
        }
    }

    // Cleave: a sweep along the whole row of the brick that was hit.
    private void Cleave(Brick source)
    {
        grid.GetLiveBricks(live);
        float rowZ = source.transform.position.z;
        float halfDepth = grid.CellDepth * 0.5f;

        float damage = HitDamage() * cleaveDamagePercent;
        int swept = 0;
        foreach (Brick candidate in live)
        {
            if (candidate == source || Mathf.Abs(candidate.transform.position.z - rowZ) > halfDepth) continue;
            swept++;
            NeonFx.Sparks(candidate.transform.position, Vector3.up, CleaveColor, 4, 3f, 40f, 0.07f, 0.25f);
            DealFraction(candidate, damage);
        }
        if (swept > 0) NeonFx.Flash(source.transform.position, CleaveColor, 0.7f, 0.15f);
    }

    // Volatile Bricks: a destroyed brick blows up its 3x3 block.
    private void Blast(Vector3 origin)
    {
        Collect(origin, blastRadius, null);
        NeonFx.Ring(origin, BlastColor, 24, 4f, 0.1f, 0.35f);
        NeonFx.Burst(origin, BlastColor, 14, 3f, 0.1f, 0.4f);
        float damage = HitDamage() * blastPercent;
        foreach (Brick neighbour in targets) DealFraction(neighbour, damage);
    }

    // Chain Lightning: arcs jump from a destroyed brick to the nearest bricks still standing.
    private void Chain(Brick source)
    {
        grid.GetLiveBricks(live);
        Vector3 origin = source.transform.position;

        targets.Clear();
        foreach (Brick candidate in live)
            if (candidate != source && (candidate.transform.position - origin).sqrMagnitude <= chainRange * chainRange)
                targets.Add(candidate);
        targets.Sort((a, b) => (a.transform.position - origin).sqrMagnitude
            .CompareTo((b.transform.position - origin).sqrMagnitude));

        float damage = HitDamage() * chainPercent;
        for (int i = 0; i < targets.Count && i < chainJumps; i++)
        {
            Vector3 position = targets[i].transform.position;
            NeonFx.Line(origin, position, Vector3.up, ChainColor, 12, 1.5f, 0.07f, 0.3f);
            NeonFx.Flash(position, ChainColor, 0.4f, 0.1f);
            DealFraction(targets[i], damage);
        }
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
        NeonFx.Embers(brick.transform.position, BurnColor, 3, 0.08f, 0.7f);
    }

    private void SpreadBurn(Brick dead)
    {
        burns.RemoveAll(b => b.Brick == dead);
        Collect(dead.transform.position, neighbourRadius, dead);
        foreach (Brick neighbour in targets) Ignite(neighbour);
        NeonFx.Burst(dead.transform.position, BurnColor, 8, 2f, 0.09f, 0.4f);
    }

    private void TickBurns(float dt)
    {
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

                NeonFx.Embers(burn.Brick.transform.position, BurnColor, 2, 0.07f, 0.6f);
                burn.Brick.TakeHit(1);

                if (!burn.Brick.IsAlive) SpreadBurn(burn.Brick);
                else if (burn.TicksLeft <= 0) burns.RemoveAt(i);
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
