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
  * Rectangular 2:1 bricks generated in a grid (7 columns, 4 starting rows, Cell Size ~0.5 tuned in the Inspector, `Brick Height` 0.4). Bricks have HP; their color is a heat ramp of the **hits left** to break them with the current ball damage, `ceil(HP / damage)` (`Brick.HitsLeft`; with damage 1 on the starting wall: 1 = white, 2 = cyan, 3 = lime, 4 = amber, 5 = hot red; hot red is always the hits the toughest brick spawning now would take, so the ramp stretches as the HP window scales up and cools down as damage grows: white = one-shot). Tough bricks have a fuller body and a stronger glow, weak ones look hollow; every non-lethal hit flashes the brick white and throws chips in its old color, then it settles on the color of the hits left.
  * **HP display setting** (player choice, pause screen "BRICK HP" row listing NUMBERS / PIPS / COLOR, saved between runs in PlayerPrefs, **NUMBERS by default**): COLOR (the above only), NUMBERS (a pixel number over each brick) or PIPS (one square per hit beyond the first, in a single row: 1 pip = 2 hits up to 4 pips = 5 hits; a pixel skull from 6 hits, meaning "tough, leave it for later"). Both show the hits left (same value as the color), not the raw HP; one-hit bricks show no label. Labels are drawn by `BrickHpOverlay` on a full-resolution UI canvas that follows the bricks, not in the pixelated 3D scene.
  * Stacking (several layers per cell) exists in code but is set to 1 layer max: tall stacks hid the ball.
  * **Advancing wall (Space Invaders style):** the whole wall steps one row toward the paddle every 5 seconds, only while the ball is in play.
  * **Endless rows:** every step adds a new row at the back. Later rows get fewer gaps and tougher bricks (difficulty ramp per row). **HP window:** when a row spawns, max HP = `ceil(smoothedDamage x targetHits)`, min = max / 5; `smoothedDamage` = main ball base damage (`BallController.MainBaseDamage`, no wall buffs) averaged over ~20 s, `targetHits = 5 + 2.0 x run minutes` (clock runs while the ball flies). `paceFactor` (1 to 4) raises it when the 25 s kill rate per ball in play exceeds the target; the window grows at most x2 per row and shrinks at most 10% per row (`BrickGrid > HP Window`). "DIFFICULTY INCREASED!" fires when the max grew 75% since the last notice.
  * **Special bricks:** per brick, climbing over 20 rows (`BrickGrid > Special Bricks`). **Armored** (steel-blue, from row 6, up to 10%) ignores splash and area damage (Shockwave, Chain, Volatile, Cleave, Meltdown, Overkill Spill); direct hits, Piercing, Echo, Laser, Satellite and Paddle Cannon still land. **Healer** (green, from row 10, up to 6%, `BrickHealer`) restores 25% of the max HP of its damaged direct neighbours every 4 s while a ball flies.
  * **XP brake (invisible):** when the kill rate (25 s average) passes 2x `Target Kills Per Second`, every brick pays `2 / (rate / target)` of its XP, floor 0.25 (`ExperienceSystem > Kill Rate Brake`), so a snowballing build does not level up every 2 to 3 s.
  * **Wall clear bonus:** destroying every brick grants 25% of the XP needed for the current level (as XP orbs), shows a "CLEAR!" banner, immediately spawns a 4-row wave and restarts the step timer.
  * **Lose line:** a red line near the paddle. If a brick reaches it, the run ends.
- **Game Over:** the run ends when the ball falls past the paddle (no lives) or the wall reaches the lose line. A short animation plays (`GameOverAnimation`: freeze-frame ball poof, the wall trembles red, then blows up brick by brick in a chain spreading from where the run was lost, and a final collapse with camera shake), then the game slows to a stop and a pixel-style Game Over screen shows the reason, level, bricks destroyed, run time and best level (saved between runs, with a "NEW BEST!" effect), and a Restart button that reloads the scene. Owned by `GameManager`.
- **Pause:** a pause button (top right) or Escape / P freezes the run, and the run pauses itself when the app goes to the background. The pause screen shows the run stats and every power-up picked with its level, pips and total effect (`PowerUpEffect.Describe`). Resume counts down 3-2-1; Restart needs a second tap. Owned by `PauseScreen` (builds itself).
- **Ball damage:** the ball has a Damage stat (starts at 1). A hit removes that many HP from the bottom brick of a stack. Every level-up adds +1 base damage for free (`PowerUpManager > Passive Damage Per Level`), and the Power Ball / Meteor Ball cards raise that gain for every following level-up (`DamagePerLevelEffect`).
- **Progression / RPG Elements:**
  * Endless experience (XP) system starting at Level 1.
  * **XP source:** every ball hit grants XP for the HP actually removed from the brick (not on destruction). A brick is worth `XpToNextLevel / Bricks Per Level` XP (`ExperienceSystem > Bricks Per Level`, 8), split per HP: `xp = damageDealt / brick.MaxHp x that`. All the XP of one ball hit (including splash, chain and cleave damage in the same frame) is merged into the fewest orbs, color-coded by value: 1 = white/grey, 5 = green, 20 = blue, 50 = purple, 100 = gold (bigger orbs for bigger values). The orbs leave the ball that made the hit (or the brick if no ball is within 1.5 units) and fly to the XP bar; the XP is only added when an orb lands, with retro pixel particle bursts.
  * **Level curve:** `Required XP = Base XP x (Level ^ Power)`, rounded. Defaults: Base XP = 12 (integer), Power = 1.2. Both editable in the Inspector at runtime for balancing.
  * **HUD:** level badge + rounded XP progress bar (top left), run clock (m:ss, `RunTimerHud`) centered under the bar, "LEVEL UP!" pixel banner at ~62% of the screen height.
  * **Upgrades (Roguelite style):** on level-up the game slows down to a full stop, then a card screen offers 3 random power-ups (count can be raised later by external stats). The player picks one and confirms. A Reroll button swaps the cards for a new roll (1 per level-up by default, `Base Rerolls` on `PowerUpManager`, extendable through `BonusRerolls`).
  * **Rarities:** Common (grey), Uncommon (green), Rare (blue), Epic (purple), Legendary (gold), picked by weight (44 / 30 / 16 / 7 / 3). Every offer holds at least one damage card when one is available (`PowerUpEffect.IsOffensive`): an offer without one swaps a card for a damage card, rolled with the same rarity weights. Each power-up is a data asset (definition + effect), some with a max stack count.
  * **Corruption:** the Uncommon card **Glitch Core** (unlimited) gives a corruption charge. While a charge is held, a red **CORRUPT xN** button sits under Reroll / Confirm on the card screen; it does not use the offer. It slides the card screen up and brings in the **corruption screen** from below: an infected, flickering background (stepped blood-red field, rolling band, scanlines, edge noise, tears, a glitching CORRUPT title) and a scrolling inventory of every owned upgrade, 3 per row, rarest first. Exquisite / Cursed cards, Glitch Core and cards whose effect has no `Remove` show a LOCKED plate. Tapping a card locks red target brackets on it; the bottom bar shows the odds as a split bar ("70% EXQUISITE / 30% CURSED") and the cost ("LOSE 1 LEVEL OF X (3 > 2)", or "X IS REMOVED (1 > 0)"). CORRUPT spends the charge and **one owned level** of that card (`PowerUpEffect.Remove`, written for every card of the normal pool; a card at level 1 leaves the owned list and can be offered again), then plays the sequence (shatter, virus, CRT collapse, reveal) and gives a random card from the **Exquisite** pool (red, above Legendary) or the **Cursed** pool (grey, hatched, a real downside); these rarities have weight 0 and only come from corruption. **The odds follow the rarity of the corrupted card:** Common 30%, Uncommon 40%, Rare 55%, Epic 70%, Legendary 80% (`PowerUpManager > Corruption > Exquisite Chances`; a used-up side gives the other 100%). No dowry, no cap change. On CONFIRM the outcome is added to the run **on top of** the level-up pick and the card screen slides back with the same offer and rerolls. **Exquisite (8):** Greed, Laser Frenzy, Bright Orbs (XP +25%, max 3), Triple Fission, Grand Choice, Phoenix Ball, Supernova, Meltdown. **Cursed:** Leaden Bonuses (max 2, about x1.5 fall), Dim Orbs (max 2, about x0.7 XP), Jittery Ball (ball +12%, max 2), Withered Paddle, Rushing Wall (max 3), Restless Ball, **Brittle Pact** (no more Shield drops, Shield Plus or Phoenix; never rolled once one of those is owned), **Hungry Wall** (+25% HP window), **Toll** (every level-up pushes the wall 2 rows, `BrickGrid.QueueExtraSteps`). Global limits (XP x0.4, wall x0.5, fall x2.5) are constants in `PowerUpStats`.
- **Power-ups & Items:**
  * Bonus items drop on brick destruction (6% per brick, no extra chance for tougher bricks, `BonusDropper` enabled) and fall toward the paddle. Three bonuses so far, each a `BonusDefinition` + `BonusEffect` asset in `Assets/Data`. The relative drop weight (Drop Weight field on each `Bonus_*` asset, editable) is Multi-Ball 5, Laser 3, Shield 3 (a cleared wall also drops a Multi-Ball). The per-brick chance is scaled by target kill rate / actual kill rate, clamped 0.3 to 1.5 (`BonusDropper > Drop Budget`), so drops per minute stay roughly constant. Bonus gate: a bonus never has two items falling at once, and Multi-Ball cannot drop while extra balls fly or a split is pending (Laser and Shield can drop while active: a Laser refreshes its timer, a second Shield adds a charge):
    * **Multi-Ball:** after a ~1 s countdown on the main ball (a ring closing in with 3 rising ticks, glowing previews of the extras orbiting it), and only once the ball heads toward the top of the screen, 2 extra balls fan out from it for 10 s, shrinking away in the last 2 s. Losing an extra never ends the run, and if the main ball is lost while an extra is flying, that extra is promoted to main (stops fading, run continues). The run only ends when the last ball is lost.
    * **Laser:** 15 s, the paddle fires an instant bolt straight ahead every second, dealing the ball's current Damage to the first brick hit. An aim guide (`LaserSight`) shows a faint beam to the next target and corner brackets on that brick, closing in as the shot charges.
    * **Shield:** rare. A barrier behind the paddle bounces the ball back, one charge per bounce, then breaks. Each Shield caught while it is up adds a charge (max 3, 5 with Shield Plus); the HUD ring has one lit segment per charge held.
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
Single source of truth for what is left. Update it at the end of every task (tick off, add, reorder); entries in `CHANGELOG.md` only describe what was done.

**v0.4: run structure and engagement** (design proposals from the 2026-10-29 phone-run analysis; plan with Opus before building). Goals from the user: a "long run" is about 6 minutes (commute-friendly, casual, a bit of challenge) and runs must be less monotonous.

*A. Quick tuning.* **Built 2026-10-29 (changelog "Quick tuning A")**, pending playtest.

*B. Final boss at ~5:00.* The wall stops and a pixel-art monster built from the brick grid appears (use brick stacking, hidden at 1 layer today, only for the boss, for depth). Body parts with different rules: weak point (x2 damage), arms/claws that can be broken to stop an attack, a core to break to win. Boss HP = measured damage per second x ~45 to 60 s so it lasts the same for every build. Player HP (3 hearts) starts at the boss: telegraphed attacks the paddle must dodge; losing the ball costs a heart and a new ball spawns (Phoenix Ball becomes a heart or similar). Victory screen with the time, optional ENDLESS continue. Lighter elites/mini-bosses at ~2:00 and ~3:30 (one big brick with a weak point, drops a rare card). Retune XP/HP pacing so level ~25 to 30 is reached by minute 5.

*C. Engagement ideas (choose a few).* Candidate special bricks (after Armored and Healer): **Bomb** bricks that push the wall one row when destroyed, **Rusher** bricks that descend faster than the wall. Active ability on a kill-fed charge meter (tap to fire a big explosion or ball burst). Perfect catch: tapping on the paddle sweet spot gives a power bounce. Hazards and bad items to dodge or catch on purpose. Special wall events every ~60 s (golden bricks, meteor shower, frenzy with extra balls, cursed row). Risk/reward shrine once or twice per run (ties into Corruption). More multi-ball sources (a card splitting the ball every Nth brick). A wall clear should feel like an event (it already triggers a bonus shower, see changelog).

*E. Wall clear waves.* A clear spawns 4 rows at once; in the 2026-10-31 runs a player with 5 clears faced about 40% more bricks than one with 2 (with Rushing Wall on top). Decide whether a clear should still harden the wall (cooldown between clear bonuses, smaller wave, or a wave that scales with the time since the last clear).

*D. Ball speed ramp, rework (built 2026-10-30 as a placeholder).* +15% of the start speed every 60 s of play (was 20%), no cap, with a "BALL SPEED UP!" notice (`GameManager`, `BallController.RunSpeedFactor`). To rework here: step size, interval and a cap, tied to the boss and the run length. The ball already stretches and glows more with its speed; a first pass adds a longer, brighter trail (`BallVisual > Speed Look`).

**Later**
- More bonus item effects (each a new `BonusEffect` subclass plus a `BonusDefinition` asset).
- More wall effects beyond the buff zones (ideas to be thought out).
- Title screen using `Logo_Wordmark`.

## 6. Changelog
Moved to `CHANGELOG.md` at the project root.
