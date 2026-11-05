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

## Phase 3: Nakama backend — done for its named scope, rule 10 not yet fully met
Local server, device sign-in, server inventory, x1 and x10 summoning with crypto-secure randomness and no pity, displayed odds, server-side evolution, locks and assignments. Verified by attack, concurrency and crash tests, and by the real game talking to the real server.

**Still on the device, so still forgeable:** village layout, gold, building upgrades and mine collection. See `SECURITY_MODEL.md`. This must move before Phase 4 rewards mean anything.

**Not done:** summon reveal animation, TLS and production setup, real sources for XP and catalysts.

## Phase 3b: village authority — next recommended
Move layout, gold and upgrades to the server.

## Phase 4: combat vertical slice — not started
## Phase 5: polish and ship readiness — not started

## Latest test results (2026-11-05)
| Suite | Result |
|---|---|
| .NET harness (`tools/core-tests`) | 102 of 102 passed |
| Unity EditMode, 6000.3.28f1 | 102 of 102 passed |
| Unity PlayMode, including 8 against the live server | 41 of 41 passed |
| Backend unit tests | passed; part of the server build |
| Backend attack and concurrency suite | passed |
| Crash recovery and production mode | passed |
| Windows player | built, 0 errors, ran 20 seconds with no errors logged |

Local builds embed the development server settings, so do not distribute them.
