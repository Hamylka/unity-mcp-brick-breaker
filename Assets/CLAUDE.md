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
  * Bonus items drop on brick destruction (6% per brick, no extra chance for tougher bricks, `BonusDropper` enabled) and fall toward the paddle. Three bonuses so far, each a `BonusDefinition` + `BonusEffect` asset in `Assets/Data`. The relative drop weight (Drop Weight field on each `Bonus_*` asset, editable) is Multi-Ball 2, Laser 6, Shield 3:
    * **Multi-Ball:** 2 extra balls fan out from the ball in play for 10 s, shrinking away in the last 2 s. Losing an extra never ends the run, and if the main ball is lost while an extra is flying, that extra is promoted to main (stops fading, run continues). The run only ends when the last ball is lost.
    * **Laser:** 15 s, the paddle fires an instant bolt straight ahead every second, dealing the ball's current Damage to the first brick hit.
    * **Shield:** rare, one at a time. A barrier behind the paddle bounces the ball back once, then breaks.
- **Art Direction (neon, retro, pixelated):**
  * The 3D scene renders at a fixed 640 px height and is upscaled with nearest-neighbour filtering (`RetroPixelation` on the camera). The UI stays at full resolution.
  * Near-black background, glowing purple grid floor, dark walls with magenta neon tube trims, glowing red lose line.
  * Ball (`BallVisual`): white-hot core in an orange shell (`BrickBreaker/Neon Fresnel` shader, `M_BallNeon`), halo, light pool on the floor, two-layer trail (thin white core in a wide orange-to-magenta tail), squash and stretch, sparks on every bounce.
  * Bonus items (`BonusItemVisual` on the prefab): a faceted neon crystal in the bonus color (`M_BonusCrystal`), orbit ring, halo, floor light pool and a short trail.
  * Bricks and the paddle are rounded rectangles (generated mesh, visual only, physics stay boxes) with a dark glassy body and a glowing outline in their color.
  * One-shot 3D particle effects (bursts, rings, sparks, embers) go through the static `NeonFx` helper: one shared particle system of additive squares (`Resources/FX/M_NeonFx`), no scene setup.
  * Post-processing: bloom, vignette, light chromatic aberration. Neon materials and the post-process profile live in `Assets/Art`.
  * UI: flat dark navy panels, cyan accents, and a built-in pixel font (`PixelFont` / `PixelText`) for all text. TextMeshPro is imported but not used yet.
- **Audio:**
  * 30 retro sound effects synthesized in code (**Tools > Brick Breaker > Generate Sound Effects**) and played by `SoundManager` (per-sound volume, pitch variation, rate limiting).
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
  6. More bonus item effects (each is a new `BonusEffect` subclass plus a `BonusDefinition` asset).

### [2026-10-24] - HP scale-up soft warning (by Claude)
- **Done:** new `HpScaleNotice` (`Assets/Scripts/UI`): when `BrickGrid.HpScaled` fires, a "DIFFICULTY INCREASED!" line (pixel size 5, hot amber) pops in at ~84% of the screen height (just under the XP bar, tuned by hand), holds 1.8 s, fades out while drifting up. It runs on unscaled time (plays during the level-up slow-down), is skipped on game over, and builds itself at startup (no scene setup). Together with the brick recolor pulse it explains why the wall just got harder.
- **Open issues:** not judged visually on screen (position, size and wording to check on a phone). No sound cue yet.
- **Roadmap (in order):**
  1. Playtest and tune the scaling (rows per proc, multiplier) and the notice.
  2. Dedicated sounds and visual effects for the bonus items (with Opus), plus a subtle cue sound for the HP scale-up.
  6. More bonus item effects (each is a new `BonusEffect` subclass plus a `BonusDefinition` asset).

### [2026-10-24] - Bonus and HP scale-up sounds and effects (by Claude)
- **Done:** 10 new synthesized sounds (`Sfx` values appended at the end: BonusPickup, BonusExpire, LaserCharge, LaserShot, ShieldUp, ShieldBreak, MultiBallSplit, BallFade, BallPromote, HpScaleUp). The generator now adds any missing `Sfx` to the scene's `SoundManager` with default mixing values (existing entries untouched; save the scene after running it). New `NeonFx` static particle helper (`Burst`, `Ring`, `Sparks`, `Flash`, `Embers`, `Line`). Wired in:
  * **Pickup** (all bonuses): chime + flash, ring and burst in the bonus color at the paddle.
  * **Laser:** charge-up sweep and sparks on a fresh cannon, "pew" per shot, the bolt now starts fat and thins out, muzzle flash and impact sparks, power-down sound and fizzle when it runs out.
  * **Shield:** hum-up sound, the barrier unrolls from the middle with a flare, idle glow pulse and drifting sparks, glass-shatter sound and a spray of shards on break. The barrier is now tinted cyan (bonus/HUD color) instead of the magenta tube material.
  * **Multi-Ball:** split blips, flash and ring on the main ball, streaks along each new path; an expiring extra pops with a soft blip.
  * **Ball promotion:** two-note call and gold flash + ring on the promoted ball (replaces the reused Confirm sound).
  * **HP scale-up:** low "dun-dun" cue with the notice, and every living brick throws a couple of embers in its new color.
- **Open issues:** compiled and generated, not yet heard or seen in play. Mix volumes and particle sizes (vs the 640 px pixelation) are first guesses: tune volumes on the `SoundManager` and sizes in the calls. The shield's collider also grows during the 0.35 s raise, so a ball arriving in that window could slip past the edges.
- **Roadmap:** see the next entry.

### [2026-10-24] - Neon bonus crystals and ball redesign (by Claude)
- **Done:** new unlit shader `BrickBreaker/Neon Fresnel` (`Assets/Art/Shaders`): color goes from Core (facing the camera) to Rim (edges), with an optional center hotspot, all HDR for the bloom. Falling bonus items are now faceted crystals (`CrystalMesh`, flat-shaded hexagonal bipyramid) in the bonus color with a tilted orbit ring, halo, floor light pool, bob and trail (`BonusItemVisual`, built in `BonusItem.Initialize`, tunable on the prefab). The ball got `BallVisual` (see section 4); the mesh is drawn by a child so it can squash and stretch without touching the collider, and extra balls reuse the cloned children. `NeonGlow` is the shared helper for additive halo/floor quads (generated radial texture).
- Checked in play mode with a screenshot: the three crystals read clearly in their colors at the 640 px pixelation, the ball reads as a hot orange core with a magenta-tipped tail.
- **Open issues:** the crystals are big on purpose (very visible); shrink `Crystal Radius/Height` and `Ring Radius` on the prefab if they feel too large on a phone. The halo and floor light under the ball are subtle; bounce sparks and squash were not judged in motion yet.
- **Bug to fix later (on its own `fix/...` branch, reported by the user):** level-up card rolls look far too repetitive. Several level-ups in a row offered the same three cards in a different order ("Power Ball", "Quick Ball", "Wide Paddle"). Likely cause, to confirm in `PowerUpManager`: the pool is tiny, 6 power-ups with one or two per rarity (Common: Quick Ball; Uncommon: Wide Paddle, Quick Learner; Rare: Power Ball; Epic: Time Warp; Legendary: Meteor Ball). Drawing 3 distinct cards with 60/25/10/4/1 weights then lands on nearly the same trio every time. Also check the fallback when a rolled rarity has no card left, and the max-stack filtering. Possible fixes: more power-ups per rarity, avoid repeating the previous offer, check the pick logic.
- **Roadmap (in order):**
  1. Playtest the new sounds, effects, crystals and ball; tune volumes and sizes.
  2. Fix the repetitive card rolls (above).
  3. Playtest and tune the HP scaling (rows per proc, multiplier) and the notice.
  6. More bonus item effects (each is a new `BonusEffect` subclass plus a `BonusDefinition` asset).

### [2026-10-24] - HUD cleanup and crystal brightness (by Claude)
- **Done:** the active bonus rings no longer show the seconds left (too tiny to read on a phone, the draining ring is enough); `ActiveBonusHud.ShowSeconds` brings them back. The XP numbers ("12/34") now sit centered inside the XP bar with a dark outline, instead of under it. The user toned the bonus crystals down by hand by 25% on the `BonusItem` prefab: `Core Brightness` 0.45, `Ring Intensity` 2.25, `Halo Intensity` 1.5, `Floor Glow Intensity` 1.2 (`Rim Intensity` unchanged at 5). The code defaults in `BonusItemVisual` still hold the older, brighter values; the prefab is the reference.
- Checked with a play-mode screenshot (HUD with all three bonuses active).
- **Drop rate (tuned by the user):** bonus drop chance raised from 4% to 6% per brick, still no extra chance per brick HP (`BonusDropper.Drop Chance` 0.06, `Extra Chance Per Hit Point` 0).
- **HP scaling (tuned by the user):** `BrickGrid > HP Scaling` is now `Rows Per Scaling` 5 and `Hp Multiplier` 1.75 (was 10 and 1.25), so the wall toughens faster. Also committed: the Android scripting define symbols in ProjectSettings.
- **Roadmap:** unchanged from the previous entry.

### [2026-10-24] - Release v0.1 (by Claude)
- **Done:** first tagged version, cut from `develop` via `release/0.1`, merged into `main` with `--no-ff` and tagged `v0.1` (`PlayerSettings.bundleVersion` was already 0.1.0, Android version code 1). Contents: advancing endless wall with HP scaling, XP/level-up roguelite cards with reroll, Game Over and restart, three bonus items (Multi-Ball, Laser, Shield) with active-bonus HUD, neon retro art direction, synthesized sound effects and music loop, Android build target.
- **Open issues (carried over):** repetitive level-up card rolls (small power-up pool, fix on a `fix/...` branch); HP scaling, bonus visuals/sounds and HUD still to be playtested and tuned on a phone.
- **Roadmap (in order):**
  1. Fix the repetitive card rolls.
  2. Playtest and tune HP scaling, notice, bonus effects on a real device.
  3. More bonus item effects.

### [2026-10-26] - README status and roadmap update (by Claude)
- **Done:** README "Status" now matches v0.1 (bonus drops and Reroll are active) and gains a roadmap.
- **Roadmap additions (after the existing items):**
  1. Design a logo for the game.
  2. Windows version in landscape that does not break the current mobile (portrait) camera setup: keep the portrait camera as is, add a per-platform camera/layout path.
  3. Special effects on the walls (ideas still to be thought out).

### [2026-10-26] - Windows landscape layout, first pass (by Claude)
- **Done (branch `feature/windows-landscape`):** "centered column" layout. New `ScreenColumn` (`Assets/Scripts/UI`): the playfield column is a centered 9:16 strip (`PixelWidth = min(Screen.width, Screen.height * 9/16)`), so on portrait phones it is the full width and nothing changes. Every canvas now goes through `ScreenColumn.ConfigureScaler` (1080x1920 reference, `Expand` match, i.e. the smaller scale: width on phones, height on wide screens, follows window resizes; `Shrink` is the opposite and blew the UI up 3x in landscape). The XP bar/badge and the active bonus rows sit in a `ScreenColumn` rect (column ∩ safe area; the bonus rows now also respect the notch, like the XP bar). Centered screens (level-up cards, Game Over, banners, notice) fit the column on their own; their dims still cover the whole screen. `BrickBreakerCamera` locks its horizontal FOV on the column aspect, so the framing in landscape is the phone one and the extra width shows the scene on the sides. `PaddleController` drag sensitivity is measured on the column width (click-and-drag kept for the mouse, by choice). Player settings: resizable window, 1600x900 default windowed size (fullscreen window at native resolution stays the default).
- **Also fixed:** the white bars at the bottom-left in landscape were a leftover `DiagCanvas` (world-space canvas with a giant `PixelText` at the origin) saved in `Game.unity`, invisible in the portrait framing. Removed from the scene.
- **Checked:** portrait in play mode (canvas scale unchanged, 330/1080); landscape HUD and menus checked by the user in a 1920x1080 Game view.
- **Open issues:** not yet tried in a Windows build (window resize, fullscreen). The side areas are plain black for now.
- **Roadmap (in order):**
  1. Make a Windows build and try it (resize the window, fullscreen toggle).
  2. Fill the side areas (neon decoration, later stats / logo).
  3. Then the existing items: logo, wall effects.

### [2026-10-26] - Logo and app icon (by Claude)
- **Done (branch `feature/logo`):** game logo and app icon, picked from two rounds of concepts. **Wordmark:** NEON in the game's glowing magenta pixel font, BREAKER in rounded neon HP bricks (violet at the top to white at the bottom), and one shot that tells the game: the ball leaves the cyan paddle, smashes the A and becomes the O of NEON. **Icon:** a magenta neon tube frame around a three-row wall, one brick smashed, the ball coming in at the same angle as in the wordmark. The art is SVG drawn by code in `Design/Logo` (`logo-lib.js` helpers, `logo-final.js` final designs, concept pages kept for reference) and rendered to PNG with `Design/Logo/export-logo.ps1` (headless Edge): `Assets/Art/Logo` holds `Icon_App` (1024), the Android adaptive layers `Icon_Adaptive_Foreground/Background` (432, the frame is left out because the launcher mask draws the outline) and `Logo_Wordmark` (transparent, 2400x1200, for a future title screen); `Design/Logo/export` holds the 512 store icon and the key art with background. Player Settings: default icon = `Icon_App`, Android adaptive icon = the two layers. Splash screen: the wordmark (imported as a sprite) for 2.5 s on the game's near-black (#05040C, no overlay), Unity logo below it (light on dark, dolly animation).
- **Open issues:** none for the icon wiring: the default icon and the Android adaptive layers are saved in `ProjectSettings.asset` (only the icon lines were committed; the keystore and window settings in that file are not part of this branch). The splash screen was not yet seen in a build. The icon is not yet checked on a real launcher.
- **Roadmap:** check the icon on the phone; use the wordmark on a title screen or in the landscape side areas.

### [2026-10-26] - Area-of-effect power-ups (by Claude)
- **Done (branch `feature/aoe-powerups`):** 6 new level-up power-ups, the pool goes from 6 to 12 cards (this is also the fix for the repetitive card rolls: every rarity now has at least two cards). All are one `AreaDamageEffect` asset (kind + amount) in `Assets/Data/PowerUps`, run by the new `AreaDamage` component, which `AreaDamageEffect` adds to the `BrickGrid` object on first pick (fresh every run) and which listens to `BrickGrid.BrickHit`. Damage dealt by `AreaDamage` never triggers area effects again (no chain reactions), and it still gives XP for HP actually removed.
  * **Shockwave** (Common, max 4): 25% of the damage dealt splashes onto the 4 neighbours (checked in play mode: exactly the bricks to the sides, in front and behind, and only those). It only reaches bricks that exist, so gaps and the wall edges mean fewer than 4 targets, and fractions pile up per brick (`Brick.DamageCarry`): 25% of a 1-damage ball removes an HP every 4 hits. Every neighbour now sparks on every hit so the reach is visible.
  * **Piercing Ball** (Uncommon, max 2): the hit also goes through the next brick(s) behind in the same column (half the damage, min 1).
  * **Chain Lightning** (Rare, max 3): a destroyed brick arcs to the nearest bricks (+1 arc per pick, range 2.2) for 20% of the ball's hit damage per pick (20 / 40 / 60%).
  * **Volatile Bricks** (Epic, max 2): a destroyed brick blasts its 3x3 block for 50% of the ball's hit damage per pick (50 / 100%). Both use the strongest ball in play and the same fraction carry as Shockwave.
  * **Cleave** (Epic, max 3): every 6th hit sweeps its whole row for 150% of the ball's hit damage (every 5th, then 4th with more picks). The new `CleaveMeter` (`Assets/Scripts/UI`, builds itself) shows a segmented bar near the bottom of the screen, one segment per hit needed, the last one pulses before the sweep and the bar flashes when it fires; it only appears once Cleave is picked.
  * **Loaded Dice** (Legendary, max 2): +2 free rerolls on every level-up per pick (`RerollEffect` raises `PowerUpManager.BonusRerolls`; `PowerUpContext` now carries the `Manager`). The pool is now 13 cards.
  * **Meltdown** (Legendary, max 1): hit bricks burn for 20% of their maximum HP per second for 3 s (60% in total, fractions carry over like the other effects, so a 1 HP brick can survive a burn); a burning brick that dies sets its neighbours alight.
    * Fixed after the first playtest: a fresh fire was only added to the burn list after a tick, and ticks only ran with a non-empty list, so the first fire never started. Checked in play mode: a 3 HP brick hit once burns down by itself in 3 s.
  * `BrickGrid` gained `GetLiveBricks`, `BrickWidth` and `CellDepth` (area geometry). Geometry, ranges and burn timing are Inspector fields on `AreaDamage`; amounts and stacks are on each asset.
- **Open issues:** compiles clean and assets are in the scene pool, but not playtested (balance of the percentages, burn cascades in low-HP walls, sound volume when many bricks are hit at once). Effects reuse existing sounds and simple `NeonFx` particles; dedicated sounds and visuals are still to do (Opus). Card descriptions are short fixed texts (they do not show the current stack value).
- **Roadmap (in order):**
  1. **Show the max level on the power-up cards** (next priority): display the current and maximum stack on a card, like "Lv2/3" (`PowerUpDefinition.MaxStacks` and `PowerUpManager.GetStacks` already exist; unlimited power-ups show only their level).
  2. **Rebalance how the player XP scales** (user feedback: level-ups come too often and feel spammy): slow the level curve (`ExperienceSystem` Base XP / Power, currently 10 and 1.2) and/or the XP gained per brick, then check it against the new power-ups that deal extra damage (splash HP also gives XP).
  3. **Rework the XP bubbles (orbs) and color-code them by value** (idea from the user): change how the bubbles behave and give each size of bubble its own color, for example 5 XP = green, 25 XP = blue. The exact colors and thresholds are still to be defined and tested; it goes together with the XP rebalance above (today a hit sends one orb per XP point, max 8).
  4. Playtest the six power-ups (use `Debug: Trigger Level Up Choice` on `PowerUpManager`) and tune percentages, stack limits and rarities.
  5. Dedicated sounds and visual effects for the AoE power-ups (with Opus).
  6. More bonus item effects (each is a new `BonusEffect` subclass plus a `BonusDefinition` asset).

### [2026-10-27] - Power-up card levels (by Claude)
- **Done (branch `feature/card-max-level`):** every level-up card now shows the level the pick grants and the cap: "LV 2/3" under the rarity, plus a row of pips (one per level: owned filled, the one this pick grants blinking, the rest empty). Unlimited power-ups show "LV n" and no pips. When the pick reaches the cap, the label and pips turn gold and a pulsing "MAX!" tag sits in the card's top-right corner; a first pick gets a green "NEW!" tag there instead (a 1-level power-up like Meltdown shows NEW!, with a gold "LV 1/1"). Colors are `Level Color` / `Max Level Color` on `PowerUpScreen`; the code is `PowerUpScreen.BuildLevelDisplay`, animated through `CardView.NextPip` / `Tag`.
- **Checked:** play-mode screenshots with forced stack counts (1/4 NEW, 2/4, 3/3 MAX, unlimited LV 4).
- **Open issues:** not yet judged on a phone (pip and tag size). Card descriptions still show fixed text, not the value at the next level.
- **Roadmap (in order):**
  1. **Rebalance how the player XP scales** (level-ups too frequent): `ExperienceSystem` Base XP / Power and/or XP per brick, checked against the AoE power-ups.
  2. **Rework the XP bubbles and color-code them by value** (e.g. 5 XP = green, 25 XP = blue), together with the rebalance.
  3. Playtest the AoE power-ups and tune percentages, stack limits and rarities.
  4. Dedicated sounds and visual effects for the AoE power-ups (with Opus).
  5. More bonus item effects.
