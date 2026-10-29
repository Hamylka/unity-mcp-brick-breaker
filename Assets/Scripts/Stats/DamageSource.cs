/// <summary>Where a point of brick damage came from. The order is stored in the run data: only append.</summary>
public enum DamageSource
{
    Ball,
    Phase,
    Laser,
    Shockwave,
    Piercing,
    Chain,
    Volatile,
    Cleave,
    Meltdown,
    Echo,
    OverkillSpill,
    Satellite,
    PaddleCannon
}

/// <summary>
/// Ambient tag read by the <see cref="RunProfiler"/> when a brick is hit. Whatever deals damage sets
/// <see cref="Current"/> before calling <c>Brick.TakeHit</c> and puts it back to <see cref="DamageSource.Ball"/>
/// afterwards (a plain ball hit never sets it). Hits are raised synchronously, so the tag is always the right one.
/// </summary>
public static class DamageTag
{
    public static DamageSource Current = DamageSource.Ball;

    /// <summary>Tags a block of damage; dispose (or use a using) to restore the default.</summary>
    public static Scope Begin(DamageSource source)
    {
        DamageSource previous = Current;
        Current = source;
        return new Scope(previous);
    }

    public readonly struct Scope : System.IDisposable
    {
        private readonly DamageSource previous;
        public Scope(DamageSource previous) => this.previous = previous;
        public void Dispose() => Current = previous;
    }
}
