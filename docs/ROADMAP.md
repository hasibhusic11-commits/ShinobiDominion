# Roadmap

"Verified" means run on this machine with results recorded in this file.

## Phase 0: audit — done
`REPOSITORY_AUDIT.md` and `DECISIONS.md`. No third-party code copied.

## Phase 1: playable village — done, awaiting your hands-on playtest
Verified by automated tests, simulated mouse and touch input, rendered screenshots, and a Windows player that launched without errors.

**Not verified:** how it feels with a real mouse, any real touchscreen or phone, and performance on target hardware.

## Phase 2: hero management — done locally, awaiting your hands-on playtest
Six functional heroes (four Legendary, one Elite, one Common), roster, Altar filters, base assignment, level 1 to 200, and the three-transition evolution rules with four placeholder forms.

**Known gaps:**
- This runs on the client. It does **not** yet meet online integrity (rule 10), which is Phase 3's job.
- Only 6 of the 30 target heroes are functional, and 4 more Legendaries are data stubs marked incomplete.
- Forms are simple block figures, not character art. Portraits and equipment are not implemented.
- XP has no real source. Only a development button grants it.
- In release builds the roster starts empty, because summoning does not exist yet.

## Style pass — done, awaiting your review
Chunky UI, card screens, castle walls, shadows and collect markers (ADR-017). Shadow settings are not yet measured on a phone.

## Phase 3: Nakama backend — done, awaiting your hands-on playtest
Local server, device sign-in, server inventory, x1 and x10 summoning with crypto-secure randomness and no pity, displayed odds, server-side evolution, locks and assignments. Verified by attack, concurrency and crash tests, and by the real game talking to the real server.

**Not done:** summon reveal animation, TLS and production setup, real sources for XP and catalysts.

## Phase 3b: village on the server — done, awaiting your hands-on playtest
Layout, gold, limits, walls, moves, upgrades, removal and collection are all server-decided. Forged local saves are ignored online and never loaded in release builds. Battle results are the one remaining part of rule 10, and they arrive with Phase 4.

**Not done:** build timers, and online undo.

## Phase 4: combat vertical slice — not started
## Phase 5: polish and ship readiness — not started

## Visual overhaul — done as temporary art, awaiting your review
Curved-roof ninja architecture for every building, modular walls with level tiers, Hero Bases with figures and emblems, textured ground, forest, mountains, waterfall, toon lighting, smoother camera and a quality setting. See `ART_STATUS.md` for what still needs an artist, and `VISUAL_BASELINE.md` for the before state and asset evaluation.

## Latest test results (2026-11-05)
| Suite | Result |
|---|---|
| .NET harness (`tools/core-tests`) | 104 of 104 passed |
| Unity EditMode, 6000.3.28f1 | 104 of 104 passed |
| Unity PlayMode, including live-server, release-mode and visual tests | 58 of 58 passed |
| Backend unit tests, including the shared rule scenarios | passed; part of the server build |
| Backend attack and concurrency suite | passed |
| Crash recovery and production mode | passed |
| Windows release player, server up | built with the new art, connected, loaded 7 buildings and 9 heroes from the server, no missing-art errors |
| Windows release player, server down | kept running without errors, reported the server unavailable |

Local builds embed the development server settings, so do not distribute them.
