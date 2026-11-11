# Audit remediation — change log and validation constraints

Baseline: `main` SHA `9eedfd0` (2026-11-11).

The current branch implements concrete safety, gameplay-visibility, documentation, and QA changes, **not a completed game-wide art redesign**.

## Implemented source edits
- Remove shell xtrace from Docker Nakama startup; it could emit expanded credentials.
- Reject HTTP backend configuration in shipping Unity clients; editor/development remains local-test capable.
- Display a temporary fallback mesh for an assigned hero lacking an authored prefab instead of invisible defense. Clean up the generated mesh when its GameObject is destroyed. Add a regression PlayMode test.
- Add secure per-message guild reports, validated against the reporting player's guild and own-message ownership, backed by migration 019; UI requires selecting a reason and confirming.
- Use top-level JSON trailing-data validation by requiring the second decode to return EOF.
- Add CI for Go, shared .NET game rules, and gameplay config consistency; record missing art as warnings until real content is approved.
- Correct false 'no battles' documentation, update pool odds from actual JSON, label historical art/roadmap, remove separate pet companions from design.

## Explicitly not fixed without upstream work
- Account recovery: needs product choice, server auth-provider design, UI and secure migration.
- Full Legendary content completion and 32 evolved forms: needs original approved art and a deliberate new summon-pool version.
- All legacy UI screens, finished Elite and troop assets, real phone profiling: require Unity screenshots, assets and hardware.
- Complete safe guild moderation: reporting exists, but staff queue, privacy controls and enforcement do not.

See `docs/RELEASE_READINESS.md` for release-blocking gates and `docs/GUILD_MODERATION.md` for secure report handling.

## Caution
Do not claim test passes that were not observed. GitHub code edits were made without a local Unity Editor and without live Nakama integration in this session.
