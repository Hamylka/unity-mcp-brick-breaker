# unity-mcp-brick-breaker

**A 3D mobile brick breaker built entirely by an LLM agent, driven from a spec file.**

Claude (Sonnet 5.5) was connected to the Unity Editor through the **Model Context Protocol (MCP)**.
From a single project brief ([`Assets/CLAUDE.md`](Assets/CLAUDE.md)), the agent generated the scene and
all of the gameplay code (**35 C# scripts, ~3,600 lines**) directly inside the editor.
No line of code was written by hand: the only human input was the brief, the prompts, and tuning
public parameters in the Inspector.

**Result:** a playable prototype in **4 hours**, for work estimated at about a week by hand.

<!-- TODO: add a gameplay GIF or screenshot, e.g. docs/gameplay.gif
![Gameplay](docs/gameplay.gif)
-->

## How it was built

```
 Project brief (CLAUDE.md)  ──►  Claude Sonnet 5.5  ──MCP──►  Unity Editor
   architecture, physics,          plans, writes C#,          scene, GameObjects,
   game feel, systems              iterates on feedback       scripts, prefabs
```

- **MCP bridge:** [MCP for Unity](https://github.com/CoplayDev/unity-mcp) (`com.coplaydev.unity-mcp`) exposes the
  editor to the agent: creating objects, writing and compiling scripts, reading console logs, capturing views.
- **Spec-driven development:** [`Assets/CLAUDE.md`](Assets/CLAUDE.md) defines the agent's role, the tech stack,
  the physics rules and the systems to build. It is the main artifact of this project.
- **Human role:** writing the brief, steering the agent with prompts, playtesting, and tuning values
  in the Inspector.

## The game

| System | What it does |
|---|---|
| Camera | Fixed pseudo-third-person view behind the paddle, tilted down over the playfield |
| Paddle | Smooth touch/mouse drag, built for mobile portrait |
| Ball | Arcade physics: constant speed, no gravity, exit angle depends on where it hits the paddle |
| Bricks | Generated grid of stacked multi-hit bricks, colored by remaining hit points, slowly pushed toward the player |
| Progression | Endless XP: each hit grants XP equal to the damage dealt; XP orbs fly to the HUD bar |
| Power-ups | Roguelite level-up screen: pick one of several cards rolled by rarity (ball damage, ball speed, paddle width, wall slow, XP boost) |
| Bonuses | Bricks can drop items that fall toward the paddle (catch effects are placeholders for now) |
| UI | Runtime-generated 5x7 pixel font, flat sprites and pooled pixel particles: no image or font assets |

## Tech stack

- Unity 6 (6000.6) · Universal Render Pipeline 17.6 · Input System 1.20
- C# · target: mobile, portrait
- Claude Sonnet 5.5 · Model Context Protocol (MCP) · MCP for Unity

## Project layout

```
Assets/
  CLAUDE.md            the brief given to the agent
  Scripts/
    Ball/  Paddle/  Bricks/  Walls/  Camera/
    Bonuses/           falling bonus items
    Progression/       XP system
    PowerUps/          level-up power-ups and their effects
    UI/                HUD, card screen, pixel font, particles
  Data/                ScriptableObjects: brick palette, bonuses, power-ups
  Prefabs/  Materials/  Physics/  Scenes/SampleScene.unity
```

## Run it

1. Open the project with **Unity 6000.6.6f1** (or a later Unity 6 release).
2. Open `Assets/Scenes/SampleScene.unity` and press **Play**.
3. Drag with the mouse (or touch on a device) to move the paddle.

## Status

Prototype. Bonus item effects are placeholders, and level thresholds and balancing are not final.

## Author

Maxime Cauvin · [github.com/Hamylka](https://github.com/Hamylka) · [hamylka.github.io](https://hamylka.github.io/)
