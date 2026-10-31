# Corruption refactor: brief for Opus

Draft by Sonnet, 2026-10-31. Visual and flow reference: `corruption-concept.html` (v2, clickable). Read the changelog entries "Corruption system", "Card pool pass", "Corruption tiers, dowries and Toll", "Rebalance after personal playtest" and "Corruption on unlimited cards" in `Assets/CLAUDE.md` first: this refactor rewrites most of them.

## Goal
Corrupting stops being an alternative to picking one of the offered cards. With at least one charge, the player opens a separate inventory screen, picks any owned upgrade, and gambles one level of it for a random Exquisite or Cursed card.

## New flow
1. **Card screen:** CORRUPT x N is enabled by a charge alone (no card selected). It never consumes the choice, the rerolls or the offer. Hint line: "CORRUPT NEEDS NO CARD".
2. **Slide:** the card screen slides up and out (~0.7 s, unscaled, ease in-out), the corruption screen slides in from below. BACK slides back with the charge unspent.
3. **Inventory:** every owned upgrade as a card in the card-screen style, **3 per row**, rarest first like the pause list. If it overflows, drag vertically with the same gesture as the pause screen power-up list (reuse it: drag threshold, momentum, tap vs drag). Cards show name, description, rarity, current level (`LV 3/4` or `LV 3`).
4. **Eligibility:** normal-pool cards only (capped or unlimited), and only if the effect can undo a level (`CanRemove`). Exquisite and Cursed cards, and cards without `Remove`, are drawn dimmed with a LOCKED stamp and cannot be selected.
5. **Selection:** tap selects (red dashed frame). A bottom bar shows the odds ("55% EXQUISITE  45% CURSED", by the rarity of the selected card, same table as today), the cost ("LOSE 1 LEVEL OF X (3 > 2)", or "X IS REMOVED (1 > 0)") and CORRUPT / BACK.
6. **Sequence:** reuse the shipped one. Every other card shatters (the shards fly and are sucked into the chosen card), the chosen card glides to the center, virus glitch, CRT cut, reveal Exquisite or Cursed, cost note, one CONFIRM. Cards scrolled off screen can simply vanish.
7. **Resolution:** on CONFIRM the selected card loses **one level** and the outcome card is added to the run. The screen slides back to the **same** card screen (same offer, same rerolls); the player still picks one of the offered cards afterwards. If more charges remain, CORRUPT is still enabled.

## Rule changes
- **No more cap reduction.** Remove `GetMaxStacks` penalties, the "MAX LV 3 > 2" wording and the "gone for the run" case. A card at level 1 is removed from the owned list and may be offered again later like any card. The cap is untouched.
- **No dowry.** A Cursed outcome pays nothing: delete `CorruptionDowry`, the guaranteed Rare+ / Legendary roll and the reroll bonus, and their texts and profiler detail.
- **Cost is the same for both outcomes** (one level), as today for unlimited cards.
- The outcome is gained on top of the offered pick (cost text: "LOSE 1 LEVEL OF X (3 > 2). YOU GAIN A RANDOM EXQUISITE OR CURSED CARD."), so a run with many charges gets stronger and more cursed over time. Watch this in the profiler.
- Exquisite and Cursed cards cannot be corrupted (unchanged).
- Odds, pools (7 Exquisite, 5+ Cursed), Glitch Core and the `IsOpenOutcome` check (Brittle Pact vs Phoenix / Shield Plus) stay.
- Open question for the user: Glitch Core can be corrupted too (it takes a charge back). Keep, or lock it.
- Open question: what happens at 0 owned eligible cards (CORRUPT stays disabled with "NOTHING TO CORRUPT"?).

## Corruption screen art direction
A bit flickery, close to the virus of the sequence, running from the moment the screen slides in (the prototype has a CSS version to translate):
- dark blood-red radial field whose brightness drops in hard steps (not smooth), a rolling red band, scanlines drifting in 4 px steps, low-alpha pixel noise redrawn about 9 times a second, occasional 80 ms horizontal tears;
- title "CORRUPT" in red with a cyan / magenta split that jitters and skews now and then;
- the sequence's virus ramps on top, so the screen reads as "getting infected" before it breaks. Keep the inventory cards readable (the flicker is background only, strongest at the edges).
- Sounds: reuse `CorruptStart` / glitch layers; a new soft looping hum or static bed under the screen is optional (Opus call).

## Code touch points (to verify in the code)
- `PowerUpManager` + `Corruption.cs`: `CanCorrupt`, `Corrupt`, `CostIsPayable`, `GetOdds`, `CorruptionDowry`, `GetMaxStacks` / `IsMaxed` (drop the penalty), `Corrupted` event, `CorruptionResult` (`LevelLost` becomes always true; add the removed card flag). Corrupt takes an owned card, not an offered one.
- `PowerUpScreen.Corruption.cs` (about 1,200 lines): button, hint, sequence, reveal. Needs a new inventory view and the slide. Keep the sequence code.
- Pause screen list (`PauseScreen`): the drag-scroll to reuse; also its text for corruption cards.
- `RunProfiler`: `corrupt` event fields (drop dowry, add levels lost).
- Docs to update at the end: section 4 (Corruption bullet) and the roadmap playtest item.

## Out of scope
New cards, pool changes, HUD for the charges held (still a later idea, now more useful since the button no longer depends on the offer).

## Check list for the playtest
Slide length; inventory with 3, 12 and 30 owned cards (scroll feel on a phone); locked cards readable; background flicker not tiring over a long session; a run with many charges (power creep); the odds line fits under the cards at phone width.
