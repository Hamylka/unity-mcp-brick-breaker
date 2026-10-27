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
  * Rectangular 2:1 bricks generated in a grid (7 columns, 4 starting rows, Cell Size ~0.5 tuned in the Inspector, `Brick Height` 0.4). Bricks have HP shown as a heat ramp (starting wall: 1 = white, 2 = cyan, 3 = lime, 4 = amber, 5 = hot red; the ramp stretches as the HP window scales up). Tough bricks have a fuller body and a stronger glow, weak ones look hollow; every non-lethal hit flashes the brick white and throws chips in its old color, then it settles on the color of the HP left.
  * **HP display setting** (player choice, pause screen "BRICK HP" row, saved between runs): COLOR (the above only), NUMBERS (a pixel number over each brick) or PIPS (one square each, rows of 5, the number above 10). Both show the estimated ball hits left to break the brick, `ceil(HP / current ball damage)` (main ball, buffs included), not the raw HP. Labels are drawn by `BrickHpOverlay` on a full-resolution UI canvas that follows the bricks, not in the pixelated 3D scene.
  * Stacking (several layers per cell) exists in code but is set to 1 layer max: tall stacks hid the ball.
  * **Advancing wall (Space Invaders style):** the whole wall steps one row toward the paddle every 5 seconds, only while the ball is in play.
  * **Endless rows:** every step adds a new row at the back. Later rows get fewer gaps and tougher bricks (difficulty ramp per row).
  * **Wall clear bonus:** destroying every brick grants 50% of the XP needed for the current level (as XP orbs), shows a "CLEAR!" banner, immediately spawns a 4-row wave and restarts the step timer.
  * **Lose line:** a red line near the paddle. If a brick reaches it, the run ends.
- **Game Over:** the run ends when the ball falls past the paddle (no lives) or the wall reaches the lose line. The game slows to a stop, then a pixel-style Game Over screen shows the reason, level, bricks destroyed, run time and best level (saved between runs, with a "NEW BEST!" effect), and a Restart button that reloads the scene. Owned by `GameManager`.
- **Pause:** a pause button (top right) or Escape / P freezes the run, and the run pauses itself when the app goes to the background. The pause screen shows the run stats and every power-up picked with its level, pips and total effect (`PowerUpEffect.Describe`). Resume counts down 3-2-1; Restart needs a second tap. Owned by `PauseScreen` (builds itself).
- **Ball damage:** the ball has a Damage stat (starts at 1). A hit removes that many HP from the bottom brick of a stack.
- **Progression / RPG Elements:**
  * Endless experience (XP) system starting at Level 1.
  * **XP source:** every ball hit grants 1 XP per HP actually removed from the brick (not on destruction). All the XP of one ball hit (including splash, chain and cleave damage in the same frame) is merged into the fewest orbs, color-coded by value: 1 = white/grey, 5 = green, 20 = blue, 50 = purple, 100 = gold (bigger orbs for bigger values). The orbs leave the ball that made the hit (or the brick if no ball is within 1.5 units) and fly to the XP bar; the XP is only added when an orb lands, with retro pixel particle bursts.
  * **Level curve:** `Required XP = Base XP x (Level ^ Power)`, rounded. Defaults: Base XP = 12 (integer), Power = 1.2. Bricks spawned after an HP scale-up give less XP per HP (`XP Scaling Exponent` 0.25 on `BrickGrid`). Both editable in the Inspector at runtime for balancing.
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

## 5. Roadmap
Single source of truth for what is left. Update it at the end of every task (tick off, add, reorder); changelog entries below only describe what was done.

**Next up (in order)**
1. **Make the Phase buff readable (with Opus):** while the ball phases through bricks it is not visible enough (only violet embers). Give the ball a clear look for the duration (ghostly or translucent ball, tint, trail change, a pass-through effect on each brick entered) and check it at the 640 px pixelation.
2. **VFX and SFX enhancement pass (with Opus):** replace the placeholder and reused cues with dedicated ones. Sounds and visuals for the AoE power-ups (Shockwave, Piercing, Chain Lightning, Volatile, Cleave, Meltdown; mix when many bricks are hit at once), the wall buff zones (zone look at the camera's grazing angle, take/expire cues) and the buffed ball, the bonus items (pickup/expire sounds are still partly reused). Check every effect at the 640 px pixelation and against the bloom.
3. **Playtest the brick HP readability** (see changelog): colors at a glance in a long run (HP window scaled up), the hit flash and chips with many hits at once, the NUMBERS / PIPS display modes (hits-left values, label size, two-row pips spilling onto the row behind, labels over the ball), then on a phone.
4. **Playtest the XP rework:** orb merging, colors and origin (ball), then the XP pace (`XP Scaling Exponent` on `BrickGrid`, Base XP / Power on `ExperienceSystem`; level-ups were too frequent). Re-check the wall-clear bonus (50% of the level cost, now paid in big orbs).
5. **Playtest and tune the power-ups:** the 6 AoE cards (percentages, stack limits, rarities, burn cascades; use `Debug: Trigger Level Up Choice` on `PowerUpManager`). Card descriptions still show fixed text: `PowerUpEffect.Describe(stacks + 1)` (added for the pause screen) can now show the value at the next level.
6. **Playtest the wall buff zones** (see changelog): zone position, 20 s / 10 s pacing, the 2x damage and Phase buffs, then more buff types (each a `WallBuff` subclass + asset in `Resources/WallBuffs`).

**Windows / landscape**
- Make a Windows build and try it (window resize, fullscreen toggle).
- Fill the side areas (neon decoration, later stats / the wordmark).

**Later**
- More bonus item effects (each a new `BonusEffect` subclass plus a `BonusDefinition` asset).
- More wall effects beyond the buff zones (ideas to be thought out).
- Title screen using `Logo_Wordmark`.

**Playtest on a real phone**
- HP scaling (5 rows / x1.75), "DIFFICULTY INCREASED!" notice (position, size, wording), brick recolor.
- Bonus items and effects: crystal size, Shield/Laser/Multi-Ball feel, bonus HUD rings, sound mix and particle sizes.
- Card level pips and tags, app icon on a launcher, splash screen in a build.
- Pause screen: button size and spot under the notch, row text size, scrolling a long power-up list, auto-pause when the app goes to the background.

## 6. Project Changelog & Architecture Status
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

### [2026-10-24] - Game Over, clear bonus, art direction and audio (by Claude)
- **Done:** Game Over and restart screen (losing the ball ends the run), wall clear bonus with "CLEAR!" banner, neon retro art direction with rounded neon bricks and paddle, 20 sound effects and a synthwave music loop. Wall step interval set to 5 seconds.
- Each feature was built on its own `feature/...` branch, merged into `develop` and pushed.
- **Known issue:** the Unity editor occasionally froze because its asset import workers crashed while reading `Library/`. Rebuilding `Library/` was the planned fix; watch for it coming back.

### [2026-10-24] - Android build confirmed, Reroll enabled (by Claude)
- **Done:** the Android build target was tested successfully by another Claude worker (roadmap item 1 is done). Reroll button enabled: it re-rolls the cards with the same rarity weights, shows the remaining count ("REROLL x1"), keeps the dim and slides the new cards in. `PowerUpManager.Base Rerolls` (default 1 per level-up) and `BonusRerolls` for future stats.
- **Open issues:** reroll not yet playtested in the editor (use the `Debug: Trigger Level Up Choice` context menu on `PowerUpManager`). A reroll can show the same card again, since each roll is independent.

### [2026-10-24] - Bonus drops: Multi-Ball, Laser, Shield (by Claude)
- **Done:** `BonusDropper` re-enabled (`dropChance` 0.08, +0.01 per extra HP) with three bonuses (see section 4). `BallController` now has a static `Active` list, `IsExtra` and `SpawnExtra`; `BrickStack.TakeDamage` is the shared way to damage a stack. `BonusEffect.CanDrop` lets a bonus opt out of the roll (Shield, while one is up). Context menu on `BonusDropper`: "Debug: Drop Every Bonus".
- **Open issues:** not playtested yet. Placeholder sounds (reuses existing Sfx), no HUD timer for the Laser, extra balls fade by shrinking (no alpha). The Shield barrier sits at world Z -2.6 (`ShieldEffect`), check it against the paddle pull-back range.

### [2026-10-24] - Bonus drop balancing and last-ball rule (by Claude)
- **Done:** playtesting showed Multi-Ball appeared too often. Drop chance is now 4% per brick with no extra chance per brick HP (the wall density does not change, so it was not needed). Weights are now Multi-Ball 2, Laser 6, Shield 3 (Multi-Ball and Shield swapped by hand after testing). The bonus item feature is kept. Weights are the `Drop Weight` field on each `Assets/Data/Bonus_*.asset`, so they can be tuned without code. Also: losing the main ball while an extra is flying promotes that extra to main, the run only ends when the last ball is lost.
- **Open issues:** Laser and Shield not yet judged in playtests. Placeholder sounds, no Laser timer on the HUD.

### [2026-10-24] - Active bonus HUD (by Claude)
- **Done:** top-right stack of rows, one per active bonus: its name, then one circular timer ring per running instance (ring drains clockwise in the bonus color with the seconds left inside, blinks in the last 2 s, fades out when it ends). Each Multi-Ball pickup gets its own line with one ring (its extras share the countdown), so several procs stack downward instead of sideways; the XP counter under the bar is now left-aligned so it stays clear of the bonus rows; Laser is a single ring; Shield is a full ring with no timer. New `ActiveBonusTracker` (static registry: a running timer calls `Report` every frame with a unique id and a group, one that stops reporting drops off) fed by `LaserCannon`, `TemporaryBall` and `ShieldBarrier`. `UiSprites.Ring` is the new ring sprite. `ActiveBonusHud` builds itself at startup, so there is no scene setup. A new bonus only needs to call `ActiveBonusTracker.Report` to appear.
- **Open issues:** compiles clean but not yet judged visually or playtested (position vs the XP bar, chip size on a phone, blink readability). Colors are hard-coded in the reporters rather than read from `BonusDefinition`.

### [2026-10-24] - Brick HP scaling and color grading (by Claude)
- **Done:** brick HP now spawns inside a `[min, max]` window (`BrickGrid > HP Scaling`: `Base Min Hp` 1, `Base Max Hp` 5, `Rows Per Scaling` 10, `Hp Multiplier` 1.25). Every `Rows Per Scaling` rows after the starting wall, both bounds are multiplied (compounding, rounded) and the old 45/25/15/10/5 weights curve is stretched across the new window (the `Difficulty Per Row` ramp still applies). Brick color is now a ratio: `BrickPalette` is a gradient, a brick's color is `Evaluate((hp-1)/(currentMax-1))`, white = 1 HP, dark blue = the toughest brick spawning now. When the window scales up, `Brick.SetColorScale` recolors every living brick with a small pulse. `BrickGrid` exposes `HpScaled(min, max)`, `CurrentMinHp`, `CurrentMaxHp`, `HpScaleTier`.
- **Open issues:** not playtested; 1.25 every 10 rows is a first guess (min stays 1 for the first two procs because of rounding). `BrickPalette.asset` falls back to the default gradient (the old color array was dropped). The soft "it got harder" warning came right after (next entry).

### [2026-10-24] - HP scale-up soft warning (by Claude)
- **Done:** new `HpScaleNotice` (`Assets/Scripts/UI`): when `BrickGrid.HpScaled` fires, a "DIFFICULTY INCREASED!" line (pixel size 5, hot amber) pops in at ~84% of the screen height (just under the XP bar, tuned by hand), holds 1.8 s, fades out while drifting up. It runs on unscaled time (plays during the level-up slow-down), is skipped on game over, and builds itself at startup (no scene setup). Together with the brick recolor pulse it explains why the wall just got harder.
- **Open issues:** not judged visually on screen (position, size and wording to check on a phone). No sound cue yet.

### [2026-10-24] - Bonus and HP scale-up sounds and effects (by Claude)
- **Done:** 10 new synthesized sounds (`Sfx` values appended at the end: BonusPickup, BonusExpire, LaserCharge, LaserShot, ShieldUp, ShieldBreak, MultiBallSplit, BallFade, BallPromote, HpScaleUp). The generator now adds any missing `Sfx` to the scene's `SoundManager` with default mixing values (existing entries untouched; save the scene after running it). New `NeonFx` static particle helper (`Burst`, `Ring`, `Sparks`, `Flash`, `Embers`, `Line`). Wired in:
  * **Pickup** (all bonuses): chime + flash, ring and burst in the bonus color at the paddle.
  * **Laser:** charge-up sweep and sparks on a fresh cannon, "pew" per shot, the bolt now starts fat and thins out, muzzle flash and impact sparks, power-down sound and fizzle when it runs out.
  * **Shield:** hum-up sound, the barrier unrolls from the middle with a flare, idle glow pulse and drifting sparks, glass-shatter sound and a spray of shards on break. The barrier is now tinted cyan (bonus/HUD color) instead of the magenta tube material.
  * **Multi-Ball:** split blips, flash and ring on the main ball, streaks along each new path; an expiring extra pops with a soft blip.
  * **Ball promotion:** two-note call and gold flash + ring on the promoted ball (replaces the reused Confirm sound).
  * **HP scale-up:** low "dun-dun" cue with the notice, and every living brick throws a couple of embers in its new color.
- **Open issues:** compiled and generated, not yet heard or seen in play. Mix volumes and particle sizes (vs the 640 px pixelation) are first guesses: tune volumes on the `SoundManager` and sizes in the calls. The shield's collider also grows during the 0.35 s raise, so a ball arriving in that window could slip past the edges.

### [2026-10-24] - Neon bonus crystals and ball redesign (by Claude)
- **Done:** new unlit shader `BrickBreaker/Neon Fresnel` (`Assets/Art/Shaders`): color goes from Core (facing the camera) to Rim (edges), with an optional center hotspot, all HDR for the bloom. Falling bonus items are now faceted crystals (`CrystalMesh`, flat-shaded hexagonal bipyramid) in the bonus color with a tilted orbit ring, halo, floor light pool, bob and trail (`BonusItemVisual`, built in `BonusItem.Initialize`, tunable on the prefab). The ball got `BallVisual` (see section 4); the mesh is drawn by a child so it can squash and stretch without touching the collider, and extra balls reuse the cloned children. `NeonGlow` is the shared helper for additive halo/floor quads (generated radial texture).
- Checked in play mode with a screenshot: the three crystals read clearly in their colors at the 640 px pixelation, the ball reads as a hot orange core with a magenta-tipped tail.
- **Open issues:** the crystals are big on purpose (very visible); shrink `Crystal Radius/Height` and `Ring Radius` on the prefab if they feel too large on a phone. The halo and floor light under the ball are subtle; bounce sparks and squash were not judged in motion yet.
- **Bug to fix later (on its own `fix/...` branch, reported by the user):** level-up card rolls look far too repetitive. Several level-ups in a row offered the same three cards in a different order ("Power Ball", "Quick Ball", "Wide Paddle"). Likely cause, to confirm in `PowerUpManager`: the pool is tiny, 6 power-ups with one or two per rarity (Common: Quick Ball; Uncommon: Wide Paddle, Quick Learner; Rare: Power Ball; Epic: Time Warp; Legendary: Meteor Ball). Drawing 3 distinct cards with 60/25/10/4/1 weights then lands on nearly the same trio every time. Also check the fallback when a rolled rarity has no card left, and the max-stack filtering. Possible fixes: more power-ups per rarity, avoid repeating the previous offer, check the pick logic.

### [2026-10-24] - HUD cleanup and crystal brightness (by Claude)
- **Done:** the active bonus rings no longer show the seconds left (too tiny to read on a phone, the draining ring is enough); `ActiveBonusHud.ShowSeconds` brings them back. The XP numbers ("12/34") now sit centered inside the XP bar with a dark outline, instead of under it. The user toned the bonus crystals down by hand by 25% on the `BonusItem` prefab: `Core Brightness` 0.45, `Ring Intensity` 2.25, `Halo Intensity` 1.5, `Floor Glow Intensity` 1.2 (`Rim Intensity` unchanged at 5). The code defaults in `BonusItemVisual` still hold the older, brighter values; the prefab is the reference.
- Checked with a play-mode screenshot (HUD with all three bonuses active).
- **Drop rate (tuned by the user):** bonus drop chance raised from 4% to 6% per brick, still no extra chance per brick HP (`BonusDropper.Drop Chance` 0.06, `Extra Chance Per Hit Point` 0).
- **HP scaling (tuned by the user):** `BrickGrid > HP Scaling` is now `Rows Per Scaling` 5 and `Hp Multiplier` 1.75 (was 10 and 1.25), so the wall toughens faster. Also committed: the Android scripting define symbols in ProjectSettings.

### [2026-10-24] - Release v0.1 (by Claude)
- **Done:** first tagged version, cut from `develop` via `release/0.1`, merged into `main` with `--no-ff` and tagged `v0.1` (`PlayerSettings.bundleVersion` was already 0.1.0, Android version code 1). Contents: advancing endless wall with HP scaling, XP/level-up roguelite cards with reroll, Game Over and restart, three bonus items (Multi-Ball, Laser, Shield) with active-bonus HUD, neon retro art direction, synthesized sound effects and music loop, Android build target.
- **Open issues (carried over):** repetitive level-up card rolls (small power-up pool, fix on a `fix/...` branch); HP scaling, bonus visuals/sounds and HUD still to be playtested and tuned on a phone.

### [2026-10-26] - README status and roadmap update (by Claude)
- **Done:** README "Status" now matches v0.1 (bonus drops and Reroll are active) and gains a roadmap.

### [2026-10-26] - Windows landscape layout, first pass (by Claude)
- **Done (branch `feature/windows-landscape`):** "centered column" layout. New `ScreenColumn` (`Assets/Scripts/UI`): the playfield column is a centered 9:16 strip (`PixelWidth = min(Screen.width, Screen.height * 9/16)`), so on portrait phones it is the full width and nothing changes. Every canvas now goes through `ScreenColumn.ConfigureScaler` (1080x1920 reference, `Expand` match, i.e. the smaller scale: width on phones, height on wide screens, follows window resizes; `Shrink` is the opposite and blew the UI up 3x in landscape). The XP bar/badge and the active bonus rows sit in a `ScreenColumn` rect (column ∩ safe area; the bonus rows now also respect the notch, like the XP bar). Centered screens (level-up cards, Game Over, banners, notice) fit the column on their own; their dims still cover the whole screen. `BrickBreakerCamera` locks its horizontal FOV on the column aspect, so the framing in landscape is the phone one and the extra width shows the scene on the sides. `PaddleController` drag sensitivity is measured on the column width (click-and-drag kept for the mouse, by choice). Player settings: resizable window, 1600x900 default windowed size (fullscreen window at native resolution stays the default).
- **Also fixed:** the white bars at the bottom-left in landscape were a leftover `DiagCanvas` (world-space canvas with a giant `PixelText` at the origin) saved in `Game.unity`, invisible in the portrait framing. Removed from the scene.
- **Checked:** portrait in play mode (canvas scale unchanged, 330/1080); landscape HUD and menus checked by the user in a 1920x1080 Game view.
- **Open issues:** not yet tried in a Windows build (window resize, fullscreen). The side areas are plain black for now.

### [2026-10-26] - Logo and app icon (by Claude)
- **Done (branch `feature/logo`):** game logo and app icon, picked from two rounds of concepts. **Wordmark:** NEON in the game's glowing magenta pixel font, BREAKER in rounded neon HP bricks (violet at the top to white at the bottom), and one shot that tells the game: the ball leaves the cyan paddle, smashes the A and becomes the O of NEON. **Icon:** a magenta neon tube frame around a three-row wall, one brick smashed, the ball coming in at the same angle as in the wordmark. The art is SVG drawn by code in `Design/Logo` (`logo-lib.js` helpers, `logo-final.js` final designs, concept pages kept for reference) and rendered to PNG with `Design/Logo/export-logo.ps1` (headless Edge): `Assets/Art/Logo` holds `Icon_App` (1024), the Android adaptive layers `Icon_Adaptive_Foreground/Background` (432, the frame is left out because the launcher mask draws the outline) and `Logo_Wordmark` (transparent, 2400x1200, for a future title screen); `Design/Logo/export` holds the 512 store icon and the key art with background. Player Settings: default icon = `Icon_App`, Android adaptive icon = the two layers. Splash screen: the wordmark (imported as a sprite) for 2.5 s on the game's near-black (#05040C, no overlay), Unity logo below it (light on dark, dolly animation).
- **Open issues:** none for the icon wiring: the default icon and the Android adaptive layers are saved in `ProjectSettings.asset` (only the icon lines were committed; the keystore and window settings in that file are not part of this branch). The splash screen was not yet seen in a build. The icon is not yet checked on a real launcher.

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

### [2026-10-27] - Power-up card levels (by Claude)
- **Done (branch `feature/card-max-level`):** every level-up card now shows the level the pick grants and the cap: "LV 2/3" under the rarity, plus a row of pips (one per level: owned filled, the one this pick grants blinking, the rest empty). Unlimited power-ups show "LV n" and no pips. When the pick reaches the cap, the label and pips turn gold and a pulsing "MAX!" tag sits in the card's top-right corner; a first pick gets a green "NEW!" tag there instead (a 1-level power-up like Meltdown shows NEW!, with a gold "LV 1/1"). Colors are `Level Color` / `Max Level Color` on `PowerUpScreen`; the code is `PowerUpScreen.BuildLevelDisplay`, animated through `CardView.NextPip` / `Tag`.
- **Checked:** play-mode screenshots with forced stack counts (1/4 NEW, 2/4, 3/3 MAX, unlimited LV 4).
- **Open issues:** not yet judged on a phone (pip and tag size). Card descriptions still show fixed text, not the value at the next level.

### [2026-10-27] - XP rebalance (by Claude)
- **Done (branch `feature/xp-rebalance`):** analysis showed XP followed the wall's HP (x1.75 per 5 rows) while the level curve only grows polynomially, so level-ups sped up the longer a run lasted (about 27 in 180 s if every brick is cleared, one every ~5 s late on). Two changes: (1) each brick now carries `Brick.XpPerHp`, set at spawn by `BrickGrid` to `hpMultiplier^(-XP Scaling Exponent x HpScaleTier)` (`BrickGrid > HP Scaling > XP Scaling Exponent`, default 0.5; 0 = old flat 1 XP per HP, 1 = constant XP per row), and `ExperienceSystem` multiplies the XP of every hit by it (splash, burn and chain damage included, they go through `BrickHit`); (2) level curve slowed to Base XP 15 and Power 1.25 (scene and code defaults).
- **Expected pace** (simulation, upper bound, assumes every spawned brick is cleared): level 3 at ~15 s, 5 at ~55 s, 10 at ~150 s, 15 at ~215 s, so roughly 10-12 picks in 3 minutes instead of ~27.
- **Open issues:** not playtested. The real pace depends on the player's damage output; tune `XP Scaling Exponent`, Base XP and Power. The wall-clear bonus (50% of the level cost) is unchanged, re-check it. XP orbs still send one orb per XP (max 8 per hit), so lower per-hit XP may now show a single orb: this goes with the orb rework.

### [2026-10-27] - XP rebalance, softer pass (by Claude)
- **Done (branch `feature/xp-rebalance`):** the first pass was too hard (level 10 needed +64% XP on the curve and arrived ~75% later in the simulation). Retuned to the user's target of about +25% for level 10: Base XP 12, Power 1.2 (curve: 772 total XP to reach level 10 instead of 642, +20%) and `XP Scaling Exponent` 0.25 (was 0.5). Together the simulated time to level 10 is ~105 s instead of ~84 s (+24%) if every spawned brick is cleared. Other milestones in the same simulation (old -> new): level 5 at 32 s -> 40 s, level 15 at 121 s -> 151 s, level 20 at 147 s -> 186 s, so the late level-ups are still spaced out (about one per 35 s around level 20 instead of ~25 s), while the early game is only slightly slower.
- **Open issues:** not playtested; the simulation is an upper bound on XP speed. Tune `XP Scaling Exponent` (BrickGrid), Base XP and Power (ExperienceSystem) from play feel. Section 4 still says Base XP 10, Power 1.2 and flat 1 XP per HP: update it once the values are settled.

### [2026-10-27] - Merged, color-coded XP orbs and roadmap cleanup (by Claude)
- **Done (branch `feature/xp-orbs`):** XP orbs now come in 5 denominations, 1 (white/grey), 5 (green), 20 (blue), 50 (purple) and 100 (gold), each with its own color, size (1x to 1.65x), pop/landing burst (bigger for richer orbs) and a slightly higher pickup pitch. `ExperienceSystem` collects the XP of every `BrickHit` raised in a frame (the ball hit plus splash, chain, cleave and laser damage) and sends it once in `LateUpdate`; `ExperienceHud.Deliver` splits it greedily into the fewest orbs (e.g. 27 XP = 20 + 5 + 1 + 1). Origin: the orbs leave the ball closest to the first brick hit (it is touching it), or the brick if no ball is within `Orb Origin Range` (1.5, on `ExperienceSystem`), so XP reads as "the ball collects it" (burn ticks and wall-clear XP still leave the brick). The old `Max Orbs Per Hit` field was removed. The orb pool (120) falls back to adding the XP instantly if it runs dry.
- The roadmap now lives in one place (section 5); the per-entry roadmap blocks were removed from the changelog, and section 4 was synced (orbs, Base XP 12, XP scaling).
- **Open issues:** compiles clean, not judged visually or in play (colors vs the bloom, sizes at 1x to 1.65x, how the ball origin looks at the 640 px pixelation). A hit that earns under 5 XP still sends individual white orbs.

### [2026-10-27] - Chain Lightning fix (by Claude)
- **Cause found:** the arcs did run, but only when a brick was *destroyed*, each dealt 20% of a 1-damage ball (0.2 HP, carried per brick, so ~5 arcs to remove one HP), and the "bolt" was a few upward-flying particles. Net effect: nearly invisible.
- **Done:** Chain Lightning now procs on any hit (`AreaDamage > Chain Chance`, 40%), jumps brick to brick (each arc starts from the previous target, never revisits, range 2.2), and every arc removes at least 1 HP (`max(1, round(hit damage x percent))`). New `ChainBolt` (`Assets/Scripts/Visuals`): a flickering jagged LineRenderer on the NeonFx material, plus flash, sparks and a pitched `LaserShot` sound. Card text is now "Hits may arc to +1 brick".
- **Open issues:** compiles clean but not played; check the bolt's look at the 640 px pixelation, the proc rate and a dedicated sound (Opus).
- **Respec (same day, user request):** Chain Lightning is now "when a brick is hit, fires an arc to the 2 nearest bricks for 20% of the current ball damage, stacks to 3 jumps". It fires on every hit (no proc chance, the `Chain Chance` field was removed), arcs fan out from the hit brick (no hopping), damage is a fixed 20% (no longer stacking) with the usual fraction carry, first pick = 2 arcs, second pick = 3 (cap), so `maxStacks` on the power-up is 2. A 1-damage ball removes an HP from a target only every 5 arcs on it: raise the percent on `ChainLightningEffect` if it feels weak.

### [2026-10-27] - Pause screen (by Claude)
- **Done (branch `feature/pause-screen`):** new `PauseScreen` (`Assets/Scripts/UI`, builds itself at startup like the HUD, no scene setup). A cyan pause button sits top right, level with the XP bar (hidden during level-up cards, slow-downs and Game Over); it pauses on touch-down so the same tap cannot launch the ball. Escape / P toggle it, and the run pauses itself when the app goes to the background (`OnApplicationPause`). The screen: "PAUSED" in big pixel letters that pop in and ride a sine wave (cyan, magenta drop shadow), a 3x2 stats panel (level, time, bricks, damage, wall HP window, XP), then a scrolling list of every power-up picked, rarest first: rarity bar, icon on its diamond, name in the rarity color, "LV 2/3" with pips (gold "MAX" when capped) and one line of what it does in total. Rows slide in one after the other, pixel motes drift up behind. RESUME fades the panels and counts down 3-2-1 before the game runs again; RESTART turns into "SURE?" (shaking) and needs a second tap within 2.5 s.
- **Total effect texts:** `PowerUpEffect.Describe(stacks)` (virtual, null falls back to the card description) is implemented on every effect, e.g. "+3 DAMAGE PER HIT", "BALL SPEED +21%" (multipliers compound), "EVERY 5 HITS SWEEP THE ROW". `AreaDamage.CleaveEveryFor(picks)` is now the single Cleave interval formula. Also added: `PowerUpManager.Owned`, `UiButton.Pressed` (touch-down event), `PixelParticleSystem.Clear`.
- **Checked:** play-mode screenshots (pause screen with 10 power-ups, HUD button) and resume (time scale back to 1, overlay hidden).
- **Open issues:** not tried on a phone (button under the notch, row text at size 3, list scrolling by drag). Sounds reuse existing cues (CardSelect, Confirm, CountTick, Denied, Restart). Editing scripts during play mode breaks the self-built UI (domain reload): restart play mode after a change.
- **Follow-up (branch `fix/bonus-hud-spacing`, user feedback):** the active bonus rows sat right under the pause button; `ActiveBonusHud.TopOffset` is now 180 (was 155), leaving a ~30 px gap. Confirmed good by the user. Roadmap item 5 added: make brick HP more obvious.

### [2026-10-27] - Wall buff zones (by Claude)
- **Done (branch `feature/wall-buff-zones`):** glowing zones on the side walls that buff the ball that bounces in them. `WallBuffZoneSpawner` (`Assets/Scripts/Walls`, builds itself, no scene setup) runs the rules: one zone at a time, spawns after 20 s, lives 10 s; if a ball takes it the 20 s only restarts once the buff has faded, if it expires untouched it restarts right away. The clock only runs while a ball is flying. Fields on the spawner: `Spawn Interval`, `Zone Lifetime`, `Zone Length` (2.5), `Min T` / `Max T` (0.2 to 0.7 along the wall). Context menu "Debug: Spawn Zone Now".
  * `WallBuffZone`: placeholder visuals (glow strip and core on the inner wall face, light patch on the floor, rising embers), grows in, pulses, blinks faster in the last 3 s, burst + chime when taken, fizzle when it expires. It registers a `WallZone` on the `Wall` at runtime (`Wall.AddZone/RemoveZone`, `WorldLength`, `GetWorldPoint`, `WorldBounds`, `IsSideWall` added).
  * Buffs are data: `WallBuff` ScriptableObject (label, color, duration, spawn weight, `Apply`/`Remove`), assets in `Assets/Resources/WallBuffs` (loaded by the spawner). First one: `DamageWallBuff` (`WallBuff_Damage`, x2 damage, 5 s). `BallBuffAura` runs it on the ball (embers, HUD ring through `ActiveBonusTracker.Report`, removes it on expiry). `BallController.DamageMultiplier` is the hook (`Damage` now includes it, so the pause screen shows the boosted value).
- **Checked:** play mode, no console errors; zone spawns on the inner face of a wall (x = +-4, z 0.2 to 8.2); the buff takes damage 1 -> 2 and shows as active. Not checked by eye (the screenshot tool only showed the scene view), nor by a real ball bounce.
- **Open issues:** look not judged (zone is seen at a grazing angle from the camera, the floor patch may carry it), no dedicated sounds (reuses ShieldUp, BonusPickup, BonusExpire), no marker on the ball beyond the embers. Extra balls spawned during a buff are not buffed.
- **Follow-up (same branch, user feedback):** zones were hard to catch, so `Zone Length` is now 3.125 (+25%, floor patch wider too) and the 2x damage buff lasts 10 s (was 5). New second buff, **Phase** (`PhaseWallBuff`, `WallBuff_Phase`, violet, 10 s): the ball passes through bricks. `BallController.Phasing` switches off collision with a brick stack just before contact (`Physics.IgnoreCollision`, checked every physics step in `UpdatePhasing`), deals one normal hit per brick entered (`BrickStack.TakeDamage(Damage)`, so splash/chain/XP work as for a normal hit) and gives the collision back once the ball has left it and phasing is over. Walls and the paddle still bounce it. Both buffs have weight 1.
  * **Checked:** play mode, ball launched with Phase: it went through the wall (total brick HP 42 -> 22, ball kept its direction until the back wall), no console errors. Not seen by eye.
  * **Open issues:** no visual on the ball while phasing beyond the violet embers (a ghostly look would help); the hit lands when the ball is ~0.3 from the brick, slightly before overlap.
- **Tuning (same branch, user request):** Phase now lasts 6 s and halves the ball's damage while it is on (`PhaseWallBuff > Damage Multiplier` 0.5, fractions carried per brick through `Brick.DamageCarry`, so a 1-damage ball removes an HP every second brick it passes). Spawn odds are 75% 2x damage / 25% Phase (weights 3 and 1 on the assets). Zones appear farther from the paddle: `Min T` / `Max T` are now 0.4 / 0.8 (world Z about 3.4 to 9.8). Also fixed: `BallBuffAura` no longer spawns particles while the scene closes (it logged "objects not cleaned up" on leaving play mode).
  * **Checked:** play mode, phase ball took the wall from 37 to 20 total HP, no new errors. Not seen by eye.

### [2026-10-27] - Brick HP readability and roadmap reorder (by Claude)
- **Done (branch `feature/brick-hp-readability`):** brick toughness now reads at a glance, three cues stacked:
  * **Heat ramp:** `BrickPalette` gradient is now white (1 HP), cyan, lime, amber, hot red (toughest spawning), keys at quarter steps so each HP of the starting wall (1 to 5) is its own hue. Written into `Assets/Data/BrickPalette.asset` (the stale `hitPointColors` array is gone) and as the code default. `BrickPalette.GetToughness(hp, scaleMax)` is the shared 0-1 ratio.
  * **Weight:** `Brick > HP Readability` scales the body brightness (`Body By Toughness` 0.4x to 2.6x) and the outline glow (`Glow By Toughness` 0.7x to 1.45x) with toughness, so weak bricks look hollow and tough ones solid and bright.
  * **Every hit:** a non-lethal hit flashes the brick white-hot (`Hit Flash Brightness` 1.6, `Hit Flash Time` 0.18 s, ease-out) and throws `Hit Chips` (4) in its old color, then it settles on the color of the HP left. The HP scale-up recolor keeps its own fade (`Color Fade Time`).
- **Checked:** play-mode screenshot with every HP step forced on the wall: the five colors are distinct at the game resolution, body fill visible on the tough bricks. No console errors. The hit flash and chips were not judged in motion.
- **Roadmap:** reordered so non-playtest work comes first, a VFX/SFX enhancement pass (absorbs the old "dedicated AoE sounds and visuals" item) sits before the playtests, and the "Playtest on a real phone" group moved to the end.
- **Open issues:** the hot red of the toughest bricks is close to the lose line and the magenta wall trims; shift the last key toward orange-red if it confuses. With many hits at once (AoE power-ups) the chips may get busy: lower `Hit Chips`. No HP number on the brick: values grow into the dozens with HP scaling, so color and weight carry it for now.
- **HP display modes (same branch, user request):** the player picks how brick HP is shown, from a new "BRICK HP" row on the pause screen (segmented COLOR / NUMBERS / PIPS, above the Restart/Resume buttons; the power-up list got 75 units shorter to make room). `BrickHpDisplay` (static, `Assets/Scripts/Bricks`) holds the mode, saves it in PlayerPrefs (`BrickHpDisplay`, default COLOR) and raises `Changed`.
  * **Labels:** `BrickHpOverlay` (`Assets/Scripts/UI`, builds itself, canvas order 4 under the HUD) draws one `PixelText` per brick on a screen-space canvas and moves it onto its brick every frame. A first pass drew the labels in 3D, but at the 640 px render a brick is ~10 px tall and the digits were mush or spilled onto the next row; the UI is full resolution, so the labels are crisp. Size follows the brick's width on screen (`Width Per Pixel` 19, whole font pixels, 2 to 6), mostly white tinted by the HP color (`Whiteness` 0.6), 1-pixel dark drop shadow (an outline filled the holes of small glyphs). Labels scale with the brick (spawn pop-in, hit punch), pop when the HP drops, vanish on death. Bricks register in `Initialize` and unregister on destroy; `Brick` exposes `ShowsHpLabel`, `VisualScale`, `Size`, `HpColor`.
  * **Pips:** a new `*` glyph in `PixelFont` (4x4 square); up to 10 HP as rows of 5, the number above that. `PixelText` gained `Spacing` / `LineGap` setters (pips use 0).
  * **Checked:** play-mode screenshots of both modes with forced HP values (1 to 140): numbers readable up to 3 digits, pips countable. The selector buttons switch the mode and save it (tested through their click handler); the selector re-syncs whenever the pause screen opens. No console errors.
  * **Open issues:** two-row pips (6 to 10 HP) reach into the row behind; labels draw over the ball and particles (UI is on top of the 3D scene); not tried on a phone.
- **Hits left instead of HP (same branch, user request):** NUMBERS and PIPS now show the estimated ball hits to break each brick, `ceil(HP / damage)`, with the damage of the main ball (`BallController.Damage`, so wall buffs count; the strongest extra if no main ball; 1 with no ball). Labels get much lighter as damage grows (a 140 HP brick against a 5-damage ball shows 28), and pips cover far more bricks before the 10 cap. A label only pops when its own brick loses HP, not when a damage upgrade lowers every label at once. AoE damage (splash, chain, burn) is not counted, so it is an estimate.
  * **Checked:** play mode, ball damage 5, HP 1/2/3/4/5/8/12/27/140 read 1/1/1/1/1/2/3/6/28 on screen. No console errors.
  * **Idea, not done:** hide the label on 1-hit bricks so only the tough ones carry a number.
