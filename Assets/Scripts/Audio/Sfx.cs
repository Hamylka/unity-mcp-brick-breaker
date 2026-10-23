/// <summary>Every sound effect in the game. The generator saves one file per value, named after it.</summary>
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
    Restart
}
