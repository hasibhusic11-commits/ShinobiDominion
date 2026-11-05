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

## Phase 3: Nakama backend — blocked on your decision
Needs a local server environment. Accounts, server inventory, server-side evolution, and summoning with crypto-secure randomness and no pity.

## Phase 4: combat vertical slice — not started
## Phase 5: polish and ship readiness — not started

## Latest test results (2026-11-05)
| Suite | Result |
|---|---|
| .NET harness (`tools/core-tests`) | 95 of 95 passed |
| Unity EditMode, 6000.3.28f1 | 95 of 95 passed |
| Unity PlayMode, 6000.3.28f1 | 33 of 33 passed |
| Windows player | built, 0 errors, ran 20 seconds with no errors logged |
