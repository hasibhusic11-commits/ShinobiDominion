# Release readiness — authoritative audit follow-up
Updated: 2026-11-11. Source branch: `fix/full-audit-security-hero-qa`.

This is a **release gate**, not a marketing score. Do not publicly launch or enable real-money purchases while BLOCKED rows remain. Reevaluate after each verified Unity/device/server run. All existing game balance numbers are provisional.

## Issue status

| Audit finding | Status | Evidence / next gate |
|---|---|---|
| Docker logs may reveal credentials through `sh -x` | **Code fix in this branch** | Compose command uses `-ec`; inspect container logs during startup for leaks |
| Release client may connect over HTTP | **Code fix in this branch** | `ServerConfig.Load` rejects non-HTTPS outside editor/development; deploy real HTTPS and test remote environment |
| Elite heroes invisible on Hero Bases | **Code fix in this branch** | Temporary, labelled procedural fallback and `HeroSpriteTests` regression; replace with authored Elite models before visual approval |
| No automated PR tests | **Code fix in this branch** | `.github/workflows/quality.yml` runs Go, .NET, and config QA; verify actual green checks |
| Guilds lack report action | **Reporting implementation in this branch** | Per-message report, server membership validation, rate limit and migration 019; **moderation operators and enforcement are still missing** |
| Design source instructs pet companions | **Docs fixed** | `docs/HERO_DESIGNS.md` explicitly prohibits pets and removes separate wolf/beast spirits |
| Stale economy, security, art, roadmap descriptions | **Docs corrected or labelled historical** | Verify config files remain canonical when changed |
| 8 launch Legendaries, only 4 in active banner | **BLOCKED: content sign-off** | Four `contentComplete=false`. Confirm the finished abilities/visuals, then version the pool and obtain explicit economy/rarity approval; do not silently unlock unfinished units |
| Unique artwork for Base + Evo I/II/III | **BLOCKED: art production** | All eight Legendary models currently use `form:-1` as a shared model across evolutions. Draft PR #1 adds form-specific portrait slots. Require 32 original visual forms, approved at game scale, plus combat animations |
| Complete high-quality UI migration | **BLOCKED: art & Unity screenshot QA** | Battle Menu uses v3; draft PR #1 updates Hero Altar/Detail. Remaining screens need design-by-screen implementation and capture approval |
| Account recovery / portable sign-in | **BLOCKED: identity UX and server migration** | `PlayerPrefs` device ID is not a recovery credential. Add provider/email verification, link/unlink/recover, account transfer and loss-of-device test cases before launch |
| Chat moderation and player safety | **BLOCKED: operator workflow** | Report queue now records evidence. Add reviewer roles, action audit log, abuse handling, retention policy, player block/mute, appeal and escalation |
| Mobile QA / performance / accessibility | **BLOCKED: devices** | Safe-area refresh on dimension/cutout changes is now implemented with a normalization regression test, but device rotation and notches still need hands-on validation. Test 60/30 fps targets, memory, draw calls, touch sizes, offline/reconnect, battery, text scaling |
| Production backend hardening | **BLOCKED: deployment** | TLS endpoint, secret rotation, durable DB backup/restore, shared rate limiting, abuse telemetry, private/admin endpoints, incident response and independent security review |

## Consolidated audit checks (2026-11-11)

- PR #3 now includes responsive safe-area refresh and a Unity safe-area regression test originally developed in the separate PR #2 branch.
- The Go JSON decoder regression suite now covers trailing `null`, `true`, numeric values and malformed text, in addition to duplicate objects and unknown fields.
- The PR workflow runs both `tools/qa/audit_configs.py` and `tools/ci/validate_catalog.py` for production-reference and content consistency checks. The catalog script can be run manually with `--release` to **fail deliberately** while incomplete Legendary art/content remains.
- PR #2 overlaps with PR #3 and must **not be merged blindly**. Compare remaining differences (notably guild reporting, UI styling and additional integration tests) before choosing a single canonical implementation.
- A green Go/.NET/config workflow is **not** evidence of passing Unity Editor/PlayMode, real Nakama integration or device tests.

## Merge gates for this branch

1. Review all changed files. No unapproved rule, summon, stat, currency, progression, tower or building-limit changes.
2. In CI, obtain green `configs`, `core`, `backend` checks for the PR.
3. In Unity `6000.3.28f1`, compile the client and run **all** EditMode / PlayMode tests, especially `HeroSpriteTests` and `ModeTests`; note live-server skips separately.
4. With Nakama and Postgres running, run backend integration and concurrency suites including new guild report migrations; verify no migrations fail.
5. Test reporting as two different players in the same guild; verify ID ownership, spoofed ID rejection, no self-report, different-guild rejection, invalid reason rejection, ten-per-day limit, and duplicate behavior.
6. Test one Elite defending a village and in battle, inspect mesh memory lifetime and absence of ghost visuals after reassignment.
7. Capture screenshots for 16:9, 20:9, and 4:3 plus notched phone/tablet screen; rotate/rescale while running, and verify HUD remains inside the refreshed safe area. Compare visual quality.
8. Review backend log files with test-only credentials and ensure secrets are absent after startup.

## Controlled content migration — eight Legendaries

Hero stats and skills exist for eight archetypes. **Only four are marked completed and present in the live random pool**. This is intentional, not a config parser failure. To open the four withheld Legendaries:
- Approve finished art and skills, including base plus Evo I/II/III.
- Set each approved hero's `contentComplete` true.
- Add it to `backend/nakama/config/summon_pool.json`; rebalance **all** rarity weights deliberately, preserve no pity, then increment pool version.
- Update odds disclosures, server balance vectors, art readiness checks and release notes, and re-run all tests.

## Strict design constraints
- Gold, Mana and gems only. No wood, energy or stamina.
- Six heroes may defend and deploy. No pets or independent animal companions.
- Use original ninja/fantasy art, not copied Castle Clash assets. The game should share genre clarity and tactile presentation, not licensed content.
- The game is 50 × 50 isometric; don't change gameplay to mask presentation bugs.
- All hero illustrations need true transparent alpha; do not present a painted backdrop as transparency.
- Avoid high-density generic UI and tiny phone text. Do not award a subjective '10/10' without owner screenshots and hands-on approval.
