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
  * Rectangular 2:1 bricks generated in a grid (7 columns, 4 starting rows, Cell Size 0.5). Bricks have HP (1 = white ... 5 = dark blue) and fade toward white as they lose HP.
  * Stacking (several layers per cell) exists in code but is set to 1 layer max: tall stacks hid the ball.
  * **Advancing wall (Space Invaders style):** the whole wall steps one row toward the paddle every few seconds, only while the ball is in play.
  * **Endless rows:** every step adds a new row at the back; clearing the wall spawns a fresh wave. Later rows get fewer gaps and tougher bricks (difficulty ramp per row).
  * **Lose line:** a red line near the paddle. If a brick reaches it, the run is lost (event exists, Game Over flow not implemented yet).
- **Ball damage:** the ball has a Damage stat (starts at 1). A hit removes that many HP from the bottom brick of a stack.
- **Progression / RPG Elements:**
  * Endless experience (XP) system starting at Level 1.
  * **XP source:** every ball hit grants 1 XP per HP actually removed from the brick (not on destruction). XP travels as small orbs (one per XP point, max 8 per hit) from the brick to the XP bar, and is only added when the orb lands, with retro pixel particle bursts.
  * **Level curve:** `Required XP = Base XP x (Level ^ Power)`, rounded. Defaults: Base XP = 10 (integer), Power = 1.2. Both editable in the Inspector at runtime for balancing.
  * **HUD:** level badge + rounded XP progress bar (top left), "LEVEL UP!" pixel banner at ~62% of the screen height.
  * **Upgrades (Roguelite style):** on level-up the game slows down to a full stop, then a card screen offers 3 random power-ups (count can be raised later by external stats). The player picks one and confirms. A Reroll button exists but is disabled for now.
  * **Rarities:** Common (grey), Uncommon (green), Rare (blue), Epic (purple), Legendary (gold), picked by weight (60 / 25 / 10 / 4 / 1). Each power-up is a data asset (definition + effect), some with a max stack count.
- **Power-ups & Items:**
  * Bonus item drops on brick destruction (falling item caught by the paddle) are implemented but **disabled** (`BonusDropper` turned off). Decision pending on keeping this feature.
  * Catching the item with the paddle triggers a perk/ability (e.g., Laser, Multi-ball, or other bonuses to be defined later).

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