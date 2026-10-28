/// <summary>
/// Every sound effect in the game. The generator saves one file per value, named after it.
/// Values are stored as numbers in the scene's SoundManager list: add new ones at the end, never in between.
/// </summary>
public enum Sfx
{
    // Ball
    Launch,
    PaddleHit,
    WallBounce,
    BallLost,

    // Bricks and wall
    BrickHit,
    BrickBreak,
    WallStep,

    // XP and progression
    OrbLaunch,
    OrbCollect,
    LevelUp,
    ClearFanfare,

    // Level-up cards
    CardsAppear,
    CardSelect,
    Confirm,
    Denied,

    // Game over
    GameOver,
    LetterThud,
    CountTick,
    NewBest,
    Restart,

    // Bonus items
    BonusPickup,
    BonusExpire,
    LaserCharge,
    LaserShot,
    ShieldUp,
    ShieldBreak,
    MultiBallSplit,
    BallFade,
    BallPromote,

    // Difficulty
    HpScaleUp,

    // Wall buffs
    PhasePass,

    // Area power-ups
    Shockwave,
    PierceZip,
    ChainZap,
    VolatileBlast,
    CleaveSweep,
    BurnIgnite,
    BurnTick,

    // Wall buff zones and buffed ball
    ZoneSpawn,
    ZoneTake,
    ZoneExpire,
    BuffFade,
    PowerHit
}
