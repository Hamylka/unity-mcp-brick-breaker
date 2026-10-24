# Project Context: 3D Brick Breaker (Micro Breaker Style)

## 1. Environment & Tech Stack
- **Game Engine:** Unity 6
- **Language:** C# (Modern, clean, optimized)
- **Input System:** Unity New Input System (preferred for cross-platform compatibility)
- **Render Pipeline:** URP (Universal Render Pipeline)
- **Target Platform:** Mobile / Smartphones (Portrait)

## 2. AI Role & Instructions
You are an expert Unity 6 game developer and architect. Your job is to help me build a 3D brick breaker game. 
- Always write production-ready, clean, and well-commented C# code.
- Avoid deprecated Unity functions; leverage Unity 6 optimization features.
- Focus on high-quality "Game Feel" (juice, satisfying physics, responsiveness).
- Keep explanations concise and separate structural architecture from quick iterations.

## 3. Core Gameplay & Physics Blueprint
- **Perspective:** Pseudo-third-person 3D view. The camera is positioned behind the paddle, tilted downward to provide a deep 3D perspective of the playfield.
- **Ball Physics:** 
  * Arcade-driven physics rather than pure realistic rigidbodies.
  * The ball must maintain a stable or controlled constant velocity (no gradual slowing down to a stop).
  * Dynamic bounce angles on the paddle based on the impact point (hitting the edge shoots the ball at a sharper angle).
  * Safeguards to prevent infinite horizontal bouncing loops.
- **Controls:** Smooth, responsive paddle movement adapted for touch-screens. Designed for fluid mobile dragging.

## 4. Systems & Features Blueprint
- **Brick System:**
  * Rectangular 2:1 bricks generated in a grid (7 columns, 4 starting rows, Cell Size ~0.5 tuned in the Inspector, `Brick Height` 0.4). Bricks have HP (1 = white, 2 = cyan, 3 = azure, 4 = blue, 5 = violet) and shift toward white as they lose HP.
  * Stacking (several layers per cell) exists in code but is set to 1 layer max: tall stacks hid the ball.
  * **Advancing wall (Space Invaders style):** the whole wall steps one row toward the paddle every 5 seconds, only while the ball is in play.
  * **Endless rows:** every step adds a new row at the back. Later rows get fewer gaps and tougher bricks (difficulty ramp per row).
  * **Wall clear bonus:** destroying every brick grants 50% of the XP needed for the current level (as XP orbs), shows a "CLEAR!" banner, immediately spawns a 4-row wave and restarts the step timer.
  * **Lose line:** a red line near the paddle. If a brick reaches it, the run ends.
- **Game Over:** the run ends when the ball falls past the paddle (no lives) or the wall reaches the lose line. The game slows to a stop, then a pixel-style Game Over screen shows the reason, level, bricks destroyed, run time and best level (saved between runs, with a "NEW BEST!" effect), and a Restart button that reloads the scene. Owned by `GameManager`.
- **Ball damage:** the ball has a Damage stat (starts at 1). A hit removes that many HP from the bottom brick of a stack.
- **Progression / RPG Elements:**
  * Endless experience (XP) system starting at Level 1.
  * **XP source:** every ball hit grants 1 XP per HP actually removed from the brick (not on destruction). XP travels as small orbs (one per XP point, max 8 per hit) from the brick to the XP bar, and is only added when the orb lands, with retro pixel particle bursts.
  * **Level curve:** `Required XP = Base XP x (Level ^ Power)`, rounded. Defaults: Base XP = 10 (integer), Power = 1.2. Both editable in the Inspector at runtime for balancing.
  * **HUD:** level badge + rounded XP progress bar (top left), "LEVEL UP!" pixel banner at ~62% of the screen height.
  * **Upgrades (Roguelite style):** on level-up the game slows down to a full stop, then a card screen offers 3 random power-ups (count can be raised later by external stats). The player picks one and confirms. A Reroll button swaps the cards for a new roll (1 per level-up by default, `Base Rerolls` on `PowerUpManager`, extendable through `BonusRerolls`).
  * **Rarities:** Common (grey), Uncommon (green), Rare (blue), Epic (purple), Legendary (gold), picked by weight (60 / 25 / 10 / 4 / 1). Each power-up is a data asset (definition + effect), some with a max stack count.
- **Power-ups & Items:**
  * Bonus items drop on brick destruction (4% per brick, no extra chance for tougher bricks, `BonusDropper` enabled) and fall toward the paddle. Three bonuses so far, each a `BonusDefinition` + `BonusEffect` asset in `Assets/Data`. The relative drop weight (Drop Weight field on each `Bonus_*` asset, editable) is Multi-Ball 2, Laser 6, Shield 3:
    * **Multi-Ball:** 2 extra balls fan out from the ball in play for 10 s, shrinking away in the last 2 s. Losing an extra never ends the run, and if the main ball is lost while an extra is flying, that extra is promoted to main (stops fading, run continues). The run only ends when the last ball is lost.
    * **Laser:** 15 s, the paddle fires an instant bolt straight ahead every second, dealing the ball's current Damage to the first brick hit.
    * **Shield:** rare, one at a time. A barrier behind the paddle bounces the ball back once, then breaks.
- **Art Direction (neon, retro, pixelated):**
  * The 3D scene renders at a fixed 640 px height and is upscaled with nearest-neighbour filtering (`RetroPixelation` on the camera). The UI stays at full resolution.
  * Near-black background, glowing purple grid floor, dark walls with magenta neon tube trims, hot orange ball with a trail, glowing red lose line.
  * Bricks and the paddle are rounded rectangles (generated mesh, visual only, physics stay boxes) with a dark glassy body and a glowing outline in their color.
  * Post-processing: bloom, vignette, light chromatic aberration. Neon materials and the post-process profile live in `Assets/Art`.
  * UI: flat dark navy panels, cyan accents, and a built-in pixel font (`PixelFont` / `PixelText`) for all text. TextMeshPro is imported but not used yet.
- **Audio:**
  * 20 retro sound effects synthesized in code (**Tools > Brick Breaker > Generate Sound Effects**) and played by `SoundManager` (per-sound volume, pitch variation, rate limiting).
  * A 34-second synthwave loop (A minor, 112 BPM), generated with **Tools > Brick Breaker > Generate Music** and played by `MusicPlayer`. It fades in, is muffled while the game is slowed or paused, and fades out with a pitch drop on Game Over.
  * Any generated `.wav` in `Assets/Audio` can be replaced by a hand-made file with the same name.

## 5. Project Changelog & Architecture Status
### [2026-10-21] - Project Initialization
- Define base architecture and project scope.
- Ready to build the first feature.

### [2026-10-21] - First concept prototype version finished
- First working prototype is now working and ready to be built.

### [2026-10-23] - New workflow and polishing
- Git has been configured on the project, so starting from now, we should push new features with some sort of Gitflow workflow so we can track better what's being implemented.
- Target platform is still Windows, one of the next step is to make sure the game works well on an Android phone and to change the platform to Android.
- We have a way to trigger a Game Over but it's not really implemented yet (ball returns on the paddle as if nothing happened).
- Also, if we Game Over, create some sort of simple menu to restart a new game.
- Let's also make sure we get an art direction that both the UI and the 3D fits well together.

### [2026-10-23] - Documentation sync (by Claude)
- Section 4 updated to match the current build: advancing wall, endless rows with difficulty ramp, lose line, ball damage stat, XP gained per HP removed and delivered by orbs, level curve formula (Base XP 10, Power 1.2), HUD, level-up slow-down and card screen, rarities and weights.
- Bonus item drops marked as implemented but disabled, decision pending.
- Gitflow set up: `main` holds stable versions, `develop` is the integration branch, each feature goes on its own `feature/...` branch from `develop`.
- **Roadmap (in order):**
  1. Game Over and restart menu (needs a decision: what does losing the ball cost?).
  2. Art direction: make the 3D scene match the UI style.
  3. Switch to Android and test rendering and controls on a real device.

### [2026-10-24] - Game Over, clear bonus, art direction and audio (by Claude)
- **Done:** Game Over and restart screen (losing the ball ends the run), wall clear bonus with "CLEAR!" banner, neon retro art direction with rounded neon bricks and paddle, 20 sound effects and a synthwave music loop. Wall step interval set to 5 seconds.
- Each feature was built on its own `feature/...` branch, merged into `develop` and pushed.
- **Known issue:** the Unity editor occasionally froze because its asset import workers crashed while reading `Library/`. Rebuilding `Library/` was the planned fix; watch for it coming back.
- **Roadmap (in order):**
  1. Switch the build target to Android and test rendering, controls and performance on a real device.
  2. Decide whether to keep the bonus item drops (currently disabled).
  3. More power-ups (e.g. Laser, Multi-ball) and enabling the Reroll button.

### [2026-10-24] - Android build confirmed, Reroll enabled (by Claude)
- **Done:** the Android build target was tested successfully by another Claude worker (roadmap item 1 is done). Reroll button enabled: it re-rolls the cards with the same rarity weights, shows the remaining count ("REROLL x1"), keeps the dim and slides the new cards in. `PowerUpManager.Base Rerolls` (default 1 per level-up) and `BonusRerolls` for future stats.
- **Open issues:** reroll not yet playtested in the editor (use the `Debug: Trigger Level Up Choice` context menu on `PowerUpManager`). A reroll can show the same card again, since each roll is independent.
- **Roadmap (in order):**
  1. Decide whether to keep the bonus item drops (currently disabled).
  2. More power-ups (e.g. Laser, Multi-ball).

### [2026-10-24] - Bonus drops: Multi-Ball, Laser, Shield (by Claude)
- **Done:** `BonusDropper` re-enabled (`dropChance` 0.08, +0.01 per extra HP) with three bonuses (see section 4). `BallController` now has a static `Active` list, `IsExtra` and `SpawnExtra`; `BrickStack.TakeDamage` is the shared way to damage a stack. `BonusEffect.CanDrop` lets a bonus opt out of the roll (Shield, while one is up). Context menu on `BonusDropper`: "Debug: Drop Every Bonus".
- **Open issues:** not playtested yet. Placeholder sounds (reuses existing Sfx), no HUD timer for the Laser, extra balls fade by shrinking (no alpha). The Shield barrier sits at world Z -2.6 (`ShieldEffect`), check it against the paddle pull-back range.
- **Roadmap (in order):**
  1. Playtest the three bonuses and decide whether to keep the feature.
  2. If kept: dedicated sounds and visual effects (Opus), drop-rate balancing.

### [2026-10-24] - Bonus drop balancing and last-ball rule (by Claude)
- **Done:** playtesting showed Multi-Ball appeared too often. Drop chance is now 4% per brick with no extra chance per brick HP (the wall density does not change, so it was not needed). Weights are now Multi-Ball 2, Laser 6, Shield 3 (Multi-Ball and Shield swapped by hand after testing). The bonus item feature is kept. Weights are the `Drop Weight` field on each `Assets/Data/Bonus_*.asset`, so they can be tuned without code. Also: losing the main ball while an extra is flying promotes that extra to main, the run only ends when the last ball is lost.
- **Open issues:** Laser and Shield not yet judged in playtests. Placeholder sounds, no Laser timer on the HUD.
- **Roadmap (in order):**
  1. **Active bonus HUD (new, not coded yet):** show which bonus items are currently active and how much time they have left (Multi-Ball 10 s, Laser 15 s, Shield until it breaks), so the player can read the state of the run at a glance.
  2. **Brick HP scaling for the endless run (new, not coded yet):** every X rows, raise the HP of newly spawned bricks so the wall keeps getting tougher the longer a run lasts. X and the amount should be Inspector fields on `BrickGrid`, to be tuned with the existing per-row difficulty ramp.
  3. Dedicated sounds and visual effects for the bonus items (still to do, with Opus): pickup, Laser bolt and shots, Shield barrier and break, Multi-Ball spawn and fade-out, ball promotion cue (the Laser timer now belongs to item 1).
  4. More bonus item effects, probably added in the future (each is a new `BonusEffect` subclass plus a `BonusDefinition` asset).

### [2026-10-24] - Active bonus HUD (by Claude)
- **Done:** top-right stack of rows, one per active bonus: its name, then one circular timer ring per running instance (ring drains clockwise in the bonus color with the seconds left inside, blinks in the last 2 s, fades out when it ends). Each Multi-Ball pickup gets its own line with one ring (its extras share the countdown), so several procs stack downward instead of sideways; the XP counter under the bar is now left-aligned so it stays clear of the bonus rows; Laser is a single ring; Shield is a full ring with no timer. New `ActiveBonusTracker` (static registry: a running timer calls `Report` every frame with a unique id and a group, one that stops reporting drops off) fed by `LaserCannon`, `TemporaryBall` and `ShieldBarrier`. `UiSprites.Ring` is the new ring sprite. `ActiveBonusHud` builds itself at startup, so there is no scene setup. A new bonus only needs to call `ActiveBonusTracker.Report` to appear.
- **Open issues:** compiles clean but not yet judged visually or playtested (position vs the XP bar, chip size on a phone, blink readability). Colors are hard-coded in the reporters rather than read from `BonusDefinition`.
- **Roadmap (in order):**
  1. **Brick HP scaling for the endless run (not coded yet):** every X rows, raise the HP of newly spawned bricks so the wall keeps getting tougher the longer a run lasts. X and the amount should be Inspector fields on `BrickGrid`, to be tuned with the existing per-row difficulty ramp.
  2. Dedicated sounds and visual effects for the bonus items (with Opus): pickup, Laser bolt and shots, Shield barrier and break, Multi-Ball spawn and fade-out, ball promotion cue.
  3. More bonus item effects, probably added in the future (each is a new `BonusEffect` subclass plus a `BonusDefinition` asset).

### [2026-10-24] - Brick HP scaling and color grading (by Claude)
- **Done:** brick HP now spawns inside a `[min, max]` window (`BrickGrid > HP Scaling`: `Base Min Hp` 1, `Base Max Hp` 5, `Rows Per Scaling` 10, `Hp Multiplier` 1.25). Every `Rows Per Scaling` rows after the starting wall, both bounds are multiplied (compounding, rounded) and the old 45/25/15/10/5 weights curve is stretched across the new window (the `Difficulty Per Row` ramp still applies). Brick color is now a ratio: `BrickPalette` is a gradient, a brick's color is `Evaluate((hp-1)/(currentMax-1))`, white = 1 HP, dark blue = the toughest brick spawning now. When the window scales up, `Brick.SetColorScale` recolors every living brick with a small pulse. `BrickGrid` exposes `HpScaled(min, max)`, `CurrentMinHp`, `CurrentMaxHp`, `HpScaleTier`.
- **Open issues:** not playtested; 1.25 every 10 rows is a first guess (min stays 1 for the first two procs because of rounding). `BrickPalette.asset` falls back to the default gradient (the old color array was dropped). The soft "it got harder" warning came right after (next entry).
- **Roadmap (in order):**
  1. ~~Soft warning on HP scale-up~~ done, see the next entry.
  2. Playtest and tune the scaling (rows per proc, multiplier).
  3. Dedicated sounds and visual effects for the bonus items (with Opus).
  4. More bonus item effects (each is a new `BonusEffect` subclass plus a `BonusDefinition` asset).

### [2026-10-24] - HP scale-up soft warning (by Claude)
- **Done:** new `HpScaleNotice` (`Assets/Scripts/UI`): when `BrickGrid.HpScaled` fires, a "DIFFICULTY INCREASING!" line (pixel size 5, hot amber) pops in at ~88% of the screen height (just under the XP bar), holds 1.8 s, fades out while drifting up. It runs on unscaled time (plays during the level-up slow-down), is skipped on game over, and builds itself at startup (no scene setup). Together with the brick recolor pulse it explains why the wall just got harder.
- **Open issues:** not judged visually on screen (position, size and wording to check on a phone). No sound cue yet.
- **Roadmap (in order):**
  1. Playtest and tune the scaling (rows per proc, multiplier) and the notice.
  2. Dedicated sounds and visual effects for the bonus items (with Opus), plus a subtle cue sound for the HP scale-up.
  3. More bonus item effects (each is a new `BonusEffect` subclass plus a `BonusDefinition` asset).
