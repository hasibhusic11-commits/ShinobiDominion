# Scorecard against Castle Clash

Date: 2026-11-10. Owner's goal: every part of the game better than Castle Clash, in play and in looks.

This is a judgment per area, made from this game's own screenshots (the PlayMode capture set, 1600x900) and from
`CASTLE_CLASH_REFERENCE.md` and `GENRE_GAP_REVIEW.md`. Nothing here is copied from Castle Clash: no names, art,
characters, levels, text or numbers. The aim is to beat its systems and its finish with our own content.

"Ahead" means a player would notice ours is better. "Level" means no clear winner. "Behind" means a player would
notice theirs is better. Each row says why.

## Where we stand

| Area | Verdict | Why |
| --- | --- | --- |
| Fair play | Ahead | The server fights every battle again and decides every reward (ADR-050). Castle Clash is known for hacked accounts; ours cannot be paid by a tampered device. |
| Summon honesty | Ahead | Every rate is shown and rolled on the server. No hidden pity or guarantee. |
| Text sharpness | Level | Cinzel and Rubik at full resolution, outlined. Still legacy text, so very small sizes are softer than a modern SDF font. |
| Menus and panels | Ahead | Painted brass and lacquer kit, nine-sliced, aligned on one grid (alignment pass e2c9f57). Castle Clash's menus are dense and dated. |
| Building art | Level | Painted, five looks per building as it levels. Theirs has more animation on each building (flags, smoke, glow). |
| Village ground | Behind | Plain grass up close, few decorations, tiny block villagers. Theirs is busier. |
| Hero art | Behind | 8 Legendaries are real 3D models; the other 12 heroes are block figures. Theirs are all finished. |
| Troop art | Behind | All troops are block figures. |
| Battle effects | Level | Was behind: hits were coloured orbs and ruins were grey blobs. Now shots and hits use painted effect pictures, a fallen building leaves the broken foot of its own walls, and health bars are framed with a delayed damage segment. Theirs still has more frames per effect: ours are single pictures that move, grow and fade. |
| Battle HUD | Level | Was behind: flat boxes and diamond shapes for stars. Now painted stars that pop in, a damage bar, a clock that turns red and ticks at the end, and a result with stars and counted loot. The clock is still outlined text, not a painted dial. |
| Battle camera | Level | Was behind: the whole 50 by 50 field was shown. Now a raid opens on the heart of the enemy base with ground to drop on, and stays there while the player aims. A lone building far from the base is off screen with no arrow pointing to it yet. |
| Battle control | Level | Was behind: drop cards and watch. Now four spells are cast during the fight, once each, and the server fights them again (ADR-052). Theirs has more spells. |
| Hero depth | Behind | We have levels, evolutions, skills and gear. Theirs adds talents and skill books (see the gap review). |
| Modes | Level | Raids, campaign, siege waves, arena, trials, guild. Theirs has more modes; ours are all server-checked. |
| Dungeons to farm | Behind | Ours pay only for new stars. Theirs can be replayed for materials. |
| Trophies and "Next" | Behind | Rival raids exist, but there is no trophy ladder or skip-to-next rival. |
| Sound | Level | Every action has a sound, but all are made in code. Recorded sounds would be better than both. |
| Phone performance | Not measured | Not yet profiled on a phone. |

## Ranked gaps, biggest first

1. **Battle presentation.** The fight is the part players watch most, and it was the weakest. Fix: real effect
   pictures for shots and hits, ruins made from each building's own picture, framed health bars with a delayed
   damage segment, a painted HUD with real stars and a damage bar, a camera framed on the enemy base, and a result
   screen with stars and counted loot.
2. **Battle control.** Fix: four spells, cast once each per battle, unlocked by the Relic Hall, checked by the
   server like every drop (ADR-052).
3. **Hero and troop art.** 12 heroes and every troop need real models. This needs Meshy, with the owner signed in
   and the credit cost shown first. It cannot be done from code.
4. **Village life.** Ground detail, decorations, villagers with real figures, small animations on buildings.
5. **Hero depth.** Talents and skill books.
6. **Farmable dungeons, trophies and Next.**
7. **Recorded sounds and music** in place of code-made ones.
8. **Modern text rendering** (SDF) and a phone performance pass.

## What this batch does

Items 1 and 2. See the end of this file for what shipped and what was checked.

## Rules this work keeps

- Systems and finish are matched or beaten; content stays our own.
- No pets. Heroes fight for the team and never build. Buildings are moved, never removed.
- The server decides currency, inventory, placement and battle results.
- No paid art packs or artists (ADR-022).

## What shipped in this batch

Date: 2026-11-10. Items 1 and 2 of the ranked gaps. Every number below is a test value.

**The fight**
- Shots and hits are painted effect pictures (arrows, thrown blades, fire, spirit bolts, lightning, sparks, smoke,
  a blast), drawn by one effect shader. The 12 pictures are the owner's own, from the `SD_Effects` folder of the
  "Buildings, States, Effects" pack (see `THIRD_PARTY_NOTICES.md`).
- A fallen building leaves burnt ground, the broken foot of its own walls and loose stones. The walls are cut from
  that building's own picture in 16 slices that rise 4 to 20% of its base width, so every ruin is the ruin of that
  building.
- Health bars are framed, and the health just lost stays as a pale segment for a moment before it drains.
- Damage numbers, a camera shake sized to the hit, and a 7% lean-in when the Town Hall falls.

**The screen**
- Stars are painted (46 px in the fight, 82 and 68 px on the result), dark until earned, and pop in one by one.
- The clock turns red in the last 30 seconds and ticks in the last 10.
- The result counts the loot up and says how many spells were cast.
- Words lie over the base only while they are needed: how to start, what a picked spell does, why a tap was refused.

**The camera**
- A raid opens on the heart of the enemy base: the Town Hall and every building within 4 cells of the group around
  it, plus 6 cells of open ground on each side, filling 94% of the screen, set 3% of the screen height below the
  middle so it sits between the clock and the cards. For the first campaign stage the view size works out at about
  12.7, against 24 when the whole field was shown, so the base is nearly twice as large on screen.
- In a live raid the view stays where the player left it. A fight played back (Siege, Arena, Trial Tower) is still
  followed by the camera.

**Spells** (ADR-052)
- Ember Rain, Mending Mist, Frost Seal and War Drum, opened by Relic Hall levels 1 to 4, each cast once per battle
  by tapping its round button and then the map. The server fights every cast again; the device and the server run
  the same checks.

**Sound**
- Nine new sounds, all made in code like the rest: a blast, a zap, a heal, an arrow, a clock tick, a count-up step,
  a spell cast, a landing and a war drum. Battle sounds play a little higher or lower each time, on four voices.

## What was checked, and what was not

- Core tests (.NET): 202 passed, 0 failed, 2 skipped, run again at commit time.
- Server unit tests (Go, in Docker): vet clean, all pass (47 test functions, 16 of them for spells), run again at
  commit time. Server integration tests against a running Nakama: all pass (106 test functions, 3 of them live
  raids with spells). These were last run before the final screen-only changes (ruins, camera, hints), which touch no rules code.
- Unity PlayMode: 144 tests, 110 passed, 0 failed, 34 skipped (all 34 need the server key, which this run did not
  have). `RaidSpellTests` covers the spell buttons, the refusal before a drop, once per battle,
  the cast in the battle log, the aim ring, painted stars, the red clock, the ruin, the opening view, that the view
  stays on the base during the fight, and that the lean-in comes back exactly.
- Looked at by eye in the capture set (1600x900): the raid opening, the fight, a picked spell, a spell landing,
  the ruins and the result.
- Not checked: a phone or tablet (no device run, no frame-rate measurement); the spell row played by hand on a
  touch screen; portrait or very narrow screens.

## Known gaps left by this batch

- Troops and 12 of the 20 heroes are still block figures, and every sound is still made in code. These are
  stand-ins and are items 3 and 7 above.
- A building that stands far from the rest of an enemy base is off screen when a raid opens, and no edge arrow
  points to it yet. The player finds it by panning.
- Each effect is one picture that moves, grows and fades, not a drawn sequence of frames.
- The result panel covers the left part of the base, and has an empty band under its button.
- The spell gaps listed under ADR-052 (a Relic Hall upgrade finishing mid-raid, no aim ring under a finger, once
  per battle as the only limit).
