# unity-mcp-brick-breaker

**Neon Breaker: a 3D mobile brick breaker built entirely by LLM agents, driven from a spec file.**

Claude was connected to the Unity Editor through the **Model Context Protocol (MCP)**.
From a single project brief ([`Assets/CLAUDE.md`](Assets/CLAUDE.md)), the agent generated the scene, the gameplay code,
the visuals and the audio (**45 C# scripts, ~5,400 lines**) directly inside the editor.
No line of code was written by hand: the only human input was the brief, the prompts, playtesting, and tuning
public parameters in the Inspector.

Two models were used: **Claude Sonnet 5.5** on day 1, then **Claude Opus 5.5** from day 2 onward,
partly to compare what each one brings (see [Two models, one project](#two-models-one-project)).

**Result:** a playable prototype in **4 hours** on day 1 (work estimated at about a week by hand), then polished into
a full game loop with a neon art direction, audio and an Android build.

<!-- TODO: add a gameplay GIF or screenshot, e.g. docs/gameplay.gif
![Gameplay](docs/gameplay.gif)
-->

## How it was built

```
 Project brief (CLAUDE.md)  ──►  Claude (Sonnet 5.5, then Opus 5.5)  ──MCP──►  Unity Editor
   architecture, physics,          plans, writes C#,                            scene, GameObjects,
   game feel, systems              iterates on feedback                         scripts, prefabs, assets
```

- **MCP bridge:** [MCP for Unity](https://github.com/CoplayDev/unity-mcp) (`com.coplaydev.unity-mcp`) exposes the
  editor to the agent: creating objects, writing and compiling scripts, reading console logs, capturing views.
- **Spec-driven development:** [`Assets/CLAUDE.md`](Assets/CLAUDE.md) defines the agent's role, the tech stack,
  the physics rules and the systems to build, and keeps a changelog of the project. It is the main artifact of this project.
- **Human role:** writing the brief, steering the agent with prompts, playtesting (in the editor and on an Android phone),
  and tuning values in the Inspector.
- **Workflow:** from day 2, Gitflow: `main` for stable versions, `develop` for integration, one `feature/...` branch per feature.

## Two models, one project

The project was built in two phases with two Claude models:

- **Day 1 (2026-10-21): Claude Sonnet 5.5** built the playable prototype from the brief.
- **From day 2 (2026-10-23 onward): Claude Opus 5.5** took over for polish, game flow, art direction, audio and the
  Android build.

Same setup for both: the same brief, the same MCP connection to the Unity Editor, and the same rule that no code is
written by hand.

### Claude Sonnet 5.5: the prototype (day 1)

| Area | Work |
|---|---|
| Camera | Analyzed a reference screenshot (angle, height, FOV) and wrote the pseudo-third-person camera with optional smooth follow; later searched the best portrait framing numerically after the playfield was shortened |
| Paddle | Touch/mouse drag with smoothing, playfield bounds, limited depth movement for a "slingshot" speed boost |
| Ball | Arcade physics with constant speed, exit angle based on the paddle hit point, safeguard against endless horizontal bouncing; wall architecture with zones for future effects |
| Bricks | Grid generator with HP and colors, rectangular 2:1 bricks, stacking with a collapse animation, brick events (hit, destroyed) |
| Wall pressure | Space Invaders-style advancing wall, lose line, endless rows, difficulty ramp (fewer gaps, tougher bricks), ball damage stat |
| Bonuses | Falling bonus items caught by the paddle (later disabled) |
| Progression | XP system with a polynomial level curve, XP gained per damage dealt, XP orbs flying to the bar (one per XP point) |
| UI | HUD with a runtime pixel font, rounded flat sprites, "LEVEL UP!" banner, pooled pixel particles |
| Power-ups | Roguelite level-up card screen (rarity weights, halo and sparkles, Confirm and disabled Reroll), 6 starter power-ups, slow-down before the cards appear |

### Claude Opus 5.5: polish, game flow, art, audio and mobile (day 2 onward)

| Area | Work |
|---|---|
| Workflow | Reviewed and synced `CLAUDE.md` with the real build, set up Gitflow (`develop`, feature branches, merges and pushes) |
| Game flow | Game Over (losing the ball ends the run, wall reaching the lose line), pixel-style Game Over screen with dropping letters, counted stats, best level and Restart; fixed a one-frame UI flash on the screens |
| Debugging | Diagnosed Unity editor freezes caused by crashing asset import workers |
| Game design | Wall clear bonus (XP reward, instant new wave, "CLEAR!" banner) |
| Art direction | Neon retro pixel look: low-resolution rendering with sharp upscaling, neon materials, bloom and post-processing; debugged emission being disabled on load; neon outline bricks; rounded-rectangle meshes for bricks and paddle (physics kept as boxes); Brick Height setting |
| Audio | 20 retro sound effects synthesized in code with an editor generator, `SoundManager` and gameplay hooks; a seamless synthwave music loop with a `MusicPlayer` that muffles during pauses and fades on Game Over |
| Mobile | Switched the build to Android (portrait, IL2CPP, ARM64), 60 FPS target, renamed the scene to `Game`; fixed the UI being cut on tall phone screens (18:9, 20:9) found while testing on a real phone |

### Observations

<!-- To fill in by Maxime: your own comparison of the two models (speed, autonomy, number of corrections needed,
code quality, how they handled bugs, etc.). -->

## The game

| System | What it does |
|---|---|
| Camera | Fixed pseudo-third-person view behind the paddle, tilted down over the playfield |
| Paddle | Smooth touch/mouse drag, built for mobile portrait |
| Ball | Arcade physics: constant speed, no gravity, exit angle depends on where it hits the paddle |
| Bricks | Generated rows of multi-hit bricks, colored by remaining hit points, pushed one row toward the player every few seconds; new rows keep coming, tougher over time |
| Clear bonus | Clearing every brick grants bonus XP, shows "CLEAR!" and spawns a fresh wave immediately |
| Progression | Endless XP: each hit grants XP equal to the damage dealt; XP orbs fly to the HUD bar |
| Power-ups | Roguelite level-up screen: pick one of several cards rolled by rarity (ball damage, ball speed, paddle width, wall slow, XP boost) |
| Game Over | Losing the ball or letting the wall reach the red line ends the run; results screen with best level and Restart |
| Art | Neon retro look: low-resolution pixel rendering, glowing rounded bricks and paddle, grid floor, bloom |
| Audio | 20 synthesized retro sound effects and a synthwave music loop, all generated in code |
| UI | Runtime-generated 5x7 pixel font, flat sprites and pooled pixel particles: no image or font assets |
| Bonuses | Falling bonus items exist in code but are disabled for now |

## Tech stack

- Unity 6 (6000.6) · Universal Render Pipeline 17.6 · Input System 1.20
- C# · target: Android, portrait (IL2CPP, ARM64)
- Claude Sonnet 5.5 and Claude Opus 5.5 · Model Context Protocol (MCP) · MCP for Unity

## Project layout

```
Assets/
  CLAUDE.md            the brief given to the agent, with the project changelog
  Scripts/
    Ball/  Paddle/  Bricks/  Walls/  Camera/
    Game/              run flow and Game Over
    Bonuses/           falling bonus items (disabled)
    Progression/       XP system
    PowerUps/          level-up power-ups and their effects
    UI/                HUD, card screen, Game Over screen, pixel font, particles
    Visuals/           rounded neon meshes
    Audio/             sound effects and music playback
    Editor/            sound effect and music generators
  Art/                 neon materials, textures, post-processing profile
  Audio/               generated sound effects and music loop
  Data/                ScriptableObjects: brick palette, bonuses, power-ups
  Prefabs/  Materials/  Physics/  Scenes/Game.unity
```

## Run it

1. Open the project with **Unity 6000.6.6f1** (or a later Unity 6 release).
2. Open `Assets/Scenes/Game.unity` and press **Play** (a portrait Game view such as 1080x1920 shows it best).
3. Drag with the mouse (or touch on a device) to move the paddle; release to launch the ball.
4. To play on Android: **File > Build Profiles > Android**, connect a phone with USB debugging enabled, then **Build And Run**.

The sound effects and the music can be regenerated with **Tools > Brick Breaker > Generate Sound Effects / Generate Music**.

## Status

Playable prototype with a complete run loop. Bonus item drops are disabled, the Reroll button is not active yet,
and balancing is not final.

## Author

Maxime Cauvin · [github.com/Hamylka](https://github.com/Hamylka) · [hamylka.github.io](https://hamylka.github.io/)
