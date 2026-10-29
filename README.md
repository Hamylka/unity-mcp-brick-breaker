# unity-mcp-brick-breaker

**Neon Breaker: a 3D mobile brick breaker built entirely by LLM agents, driven from a spec file.**

Claude was connected to the Unity Editor through the **Model Context Protocol (MCP)**.
From a single project brief ([`Assets/CLAUDE.md`](Assets/CLAUDE.md)), the agent generated the scene, the gameplay code,
the visuals and the audio (**82 C# scripts, ~12,700 lines**) directly inside the editor.
No line of code was written by hand: the only human input was the brief, the prompts, playtesting, and tuning
public parameters in the Inspector.

Two models were used: **Claude Sonnet 5.5** on day 1, then **Claude Opus 5.5** from day 2 onward,
partly to compare what each one brings (see [Two models, one project](#two-models-one-project)).

**Result:** a playable prototype in **4 hours** on day 1 (work estimated at about a week by hand), then polished into
a full game loop with a neon art direction, audio and an Android build. Two tagged releases so far (**v0.1** on
2026-10-26, **v0.2** on 2026-10-29), the second adding area-of-effect power-ups, wall buff zones, a pause screen,
a logo and a run profiler.

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
- **Workflow:** from day 2, Gitflow: `main` for stable versions (tagged `v0.1`, `v0.2`), `develop` for integration,
  one `feature/...` or `fix/...` branch per task and a `release/...` branch to cut each version. Every session starts
  from `CLAUDE.md` alone, and ends with a changelog entry so the next one picks up the latest state.
- **Balancing data:** every run is recorded by a built-in profiler (damage per source, level-ups, card picks, snapshots)
  so tuning is done from real numbers instead of impressions.

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

**Overall:** on a project of this scale (a prototype), I haven't found that Opus 5.5 makes a big difference compared
to Sonnet 5.5. Opus also uses more of the token budget, so as a Claude Pro user, it is probably smarter to stay on
Sonnet for most features.

**Where Opus stood out:**

- **Art and sound:** the art direction and the sound design were very convincing for a first version. I will probably
  use Opus for this kind of task when prototyping in the future.
- **Debugging:** Opus was much stronger here. When Unity froze, it went straight to the Unity logs, explained what
  was going on and suggested fixes, whereas Sonnet mostly waited for Unity to become responsive again, which felt
  rougher and more frustrating.
- **Autonomy:** as a co-worker, Sonnet does its job well, but Opus is more independent: it takes more initiative,
  for example by asking for permission to read the Unity logs on its own.
- **Speed:** Opus's reasoning felt faster than Sonnet's, which makes sense since it uses more of the token budget.
  The time Sonnet spent waiting for Unity probably slowed its side of the project down as well.

**What both did well:** I never had to explain something twice with either model.

**Since then:** the model is picked per task to save tokens. Sonnet for gameplay features, small fixes, refactors and
docs; Opus for art direction, visual effects, audio, hard debugging and planning large systems (the table is in
[`CLAUDE.md`](CLAUDE.md)).


## The game

| System | What it does |
|---|---|
| Camera | Fixed pseudo-third-person view behind the paddle, tilted down over the playfield; on wide screens the horizontal field of view stays locked on a centered 9:16 column |
| Paddle | Smooth touch/mouse drag, built for mobile portrait |
| Ball | Arcade physics: constant speed, no gravity, exit angle depends on where it hits the paddle; damage stat that grows with every level-up |
| Bricks | Rows of multi-hit bricks (7 columns), pushed one row toward the player every 5 seconds; new rows keep coming, tougher over time. HP window scales up with the rows, and the color is a heat ramp of the hits left |
| Brick HP display | Pause-screen setting, saved between runs: PIPS (default), COLOR or NUMBERS, always showing the hits left to break the brick |
| Clear bonus | Clearing every brick grants 25% of the current level's XP, shows "CLEAR!" and spawns a fresh wave immediately |
| Progression | Endless XP: each hit grants XP equal to the damage dealt; XP orbs, color-coded by value, fly to the HUD bar |
| Power-ups | Roguelite level-up screen: pick one of three cards rolled by rarity (Common to Legendary), with a Reroll button and level pips on each card. 13 cards: ball stats, paddle width, time slow, XP boost, and area-of-effect damage (Shockwave, Piercing Ball, Chain Lightning, Volatile Bricks, Cleave, Meltdown) |
| Bonuses | Falling bonus items (6% per brick): Multi-Ball (telegraphed split), Laser (with an aim guide) and Shield, with a timer HUD |
| Wall buff zones | Glowing zones appear on the side walls; a ball bouncing in one gets a timed buff: 2x damage or Phase (passes through bricks) |
| Pause | Pause button, Escape / P, or automatic when the app goes to the background; shows run stats and every power-up picked, with a 3-2-1 resume and a two-tap restart |
| Game Over | Losing the last ball or letting the wall reach the red line ends the run, with a short blow-up animation, then a results screen with best level, Restart and run data sharing |
| Art | Neon retro look: low-resolution pixel rendering, glowing rounded bricks and paddle, grid floor, bloom, camera shake |
| Audio | 52 synthesized retro sound effects and a synthwave music loop, all generated in code |
| UI | Runtime-generated 5x7 pixel font, flat sprites and pooled pixel particles: no image or font assets |
| Run profiler | Every run is saved as JSON (damage per source, level-ups, card offers and picks, events, snapshots) for balancing |

## Tech stack

- Unity 6 (6000.6) · Universal Render Pipeline 17.6 · Input System 1.20
- C# · targets: Android, portrait (IL2CPP, ARM64), and Windows, landscape (first pass)
- Claude Sonnet 5.5 and Claude Opus 5.5 · Model Context Protocol (MCP) · MCP for Unity

## Project layout

```
CLAUDE.md              session guide for the agent (imports Assets/CLAUDE.md)
Assets/
  CLAUDE.md            the brief given to the agent, with the roadmap and the project changelog
  Scripts/
    Ball/  Paddle/  Bricks/  Camera/
    Walls/             side walls, buff zones and the buffs they give
    Game/              run flow, Game Over and its animation
    Bonuses/           falling bonus items and their effects
    Progression/       XP system
    PowerUps/          level-up power-ups and their effects
    Stats/             run profiler and damage attribution
    UI/                HUD, card screen, pause and Game Over screens, pixel font, particles
    Visuals/           rounded neon meshes, glows and particle effects
    Audio/             sound effects and music playback
    Editor/            sound effect and music generators, run data menu
  Art/                 neon materials and shaders, textures, post-processing profile, logo and app icon
  Audio/               generated sound effects and music loop
  Data/                ScriptableObjects: brick palette, bonuses, power-ups
  Resources/           wall buffs and shared effect assets loaded at runtime
  Prefabs/  Materials/  Physics/  Scenes/Game.unity
Design/Logo/           logo source (SVG drawn by code) and the PNG export script
```

## Run it

1. Open the project with **Unity 6000.6.6f1** (or a later Unity 6 release).
2. Open `Assets/Scenes/Game.unity` and press **Play** (a portrait Game view such as 1080x1920 shows it best; a wide Game view also works).
3. Drag with the mouse (or touch on a device) to move the paddle; release to launch the ball. Escape or P pauses.
4. To play on Android: **File > Build Profiles > Android**, connect a phone with USB debugging enabled, then **Build And Run**.

The **Tools > Brick Breaker** menu regenerates the sound effects and the music, and opens or clears the saved run data.
Run files are written to the persistent data path (`RunData` folder); on a phone, the Game Over screen can share them.

## Status

Version 0.2: playable build with a complete run loop, 13 power-up cards (including area-of-effect damage), three bonus
items (Multi-Ball with a telegraphed split, Laser with an aim guide, Shield), wall buff zones (2x damage, Phase), a pause
screen, a readable brick HP display, a Game Over animation, a logo and app icon, a first Windows landscape layout and a
run profiler for balancing. Balancing is not final: the late game levels up too fast and the card pool runs dry.

### Roadmap

1. Progression rework (v0.3): bigger power-up pool with percent-based damage cards, XP per brick tied to the level cost,
   brick HP that follows the ball damage.
2. Windows build in landscape (resize, fullscreen) and neon decoration for the side areas.
3. Title screen using the wordmark.
4. More bonus items and wall effects.
5. Playtest on a real phone (HUD sizes, pause screen, sounds).

## Author

Maxime Cauvin · [github.com/Hamylka](https://github.com/Hamylka) · [hamylka.github.io](https://hamylka.github.io/)
