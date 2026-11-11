# Full-repository audit remediation — 2026-11-11

Scope: current `main` `9eedfd0` plus the unmerged draft Hero UI PR #1. This
audit-fix PR is **separate** and is never a license to merge untested Unity UI.

## Fixed in this branch

| Finding | Implementation | Evidence expected |
|---|---|---|
| Docker shell trace exposed expanded secrets | `backend/docker-compose.yml` uses `/bin/sh -ec`, not `-ecx` | Inspect Compose output and logs in a disposable local environment; ensure no secret values emitted |
| JSON RPC decoder could accept extra values | `backend/nakama/main.go` enforces EOF after one JSON document | New cases in `backend/nakama/main_test.go`; Go CI |
| Assigned Elite had an empty Hero Base | `VillageView` draws cached visual fallback if no prefab and `HeroGuard` attaches the same halo | Added `HeroFlowTests` test; requires Unity PlayMode |
| Stale mobile safe area on rotation | `VillageBootstrap` reapplies `UiRoot` safe area on bounds changes | `UiPolishTests` normalizes notch/cutout test; requires device validation |
| No automatic CI | `.github/workflows/quality-gates.yml` | Three jobs: C# rules, Nakama Docker Go tests, static catalog validation |
| Guild chat had no report action | `sd_guild_report`, migration `019_guild_chat_reports.sql`, SDK client, UI Report button | Live Nakama integration test; see *remaining moderation work* |
| Inconsistent shared UI window art | Legacy/scenic `PanelView` shells use approved `SdTheme` nine-sliced frame and ribbon | Screenshot compare all scenes and resolutions; see below |
| Stale summon/economy documentation | `docs/ECONOMY_AND_ODDS.md` matches actual **70/28/2** pool | `tools/ci/validate_catalog.py` verifies valid pool references |
| Hero descriptions violated no-companion rule | `docs/HERO_DESIGNS.md` removes animal companions and stale summon statuses | Owner art-direction review |
| Stale art/security notes | Updated `docs/ART_STATUS.md` and `docs/SECURITY_MODEL.md` | Review against current code and asset registry |

## Not equivalent to a production launch

These require new assets, provider credentials, product decisions, a moderator
operating process, or physical hardware. Do not mark them **fixed** based on a
code review alone.

1. **All Legendary hero content and evolutions:** eight hero concepts; four
   Legendaries `contentComplete: false`; 24 authored Evo I/II/III forms missing.
   Current `form: -1` 3D prefabs share the same in-world look at all stages.
   Do not silently enable or add summon entries; those change advertised odds.
2. **Full UI v3 migration:** Battle Menu is done on `main`; Hero Altar and
   Hero Detail are in separate draft PR #1; the remaining gameplay screens
   require screen-specific layouts, artwork and real screenshot approval.
   Replacing shared frame sprites alone does not make them premium.
3. **Account recovery:** a device-only ID saved in PlayerPrefs is not a
   recoverable login. Add platform/email identity linking and verified recovery.
4. **Production hosting and abuse controls:** require HTTPS/WSS, backups, shared
   rate-limiter, telemetry, secrets management and tested disaster recovery.
5. **Guild moderation operations:** the report action and evidence table exist
   in this branch, but there is no secured moderator review console, response
   workflow, user blocking, meaningful content moderation, or retention policy.
6. **Real-device and user testing:** first-run/login, landscape cutouts, 4:3
   and tall aspect ratios, font scaling, 60fps targets, battery, memory, LFS
   image correctness and a broad gameplay playtest.
7. **Progression approval and account safety:** current numbers are explicitly
   provisional. Do not release unapproved drop rates or copy Castle Clash assets.

## Quality gates

- Pull request checks: `.NET core rules`, `Nakama Docker Go tests`,
  `Catalog resources`.
- Unity PlayMode/EditMode and real Nakama/PG integration are **not** run
  automatically on GitHub until a Unity runner/license and live integration
  environment have been set up.
- `python3 tools/ci/validate_catalog.py --release` intentionally fails while
  content/art launch blockers remain, rather than quietly declaring a release.
- Exact mobile screenshot acceptance matrix: 1280×720, 1600×900, 2340×1080,
  and 1600×1200. Capture HUD, Battle Menu, Altar, Hero Detail, Summon
  single/ten, village build/upgrade, Siege, Arena, Rankings, Quests, Gift,
  Guilds and battle victory/defeat. Compare readable text, overlap, touch
  size, original art quality and safe margins.
- Check accessibility: labels legible without zooming, action contrast, color
  not the only state indicator, and touch targets >= 44pt logical target.

## How to review

`main` remains untouched. Review draft PR #2 and its CI runs. On a
feature checkout run:

```bash
python3 tools/ci/validate_catalog.py
python3 tools/ci/validate_catalog.py --release  # expected to fail until complete
dotnet test tools/core-tests/ShinobiDominion.Core.Tests.csproj
docker build -f backend/nakama/Dockerfile .
```

Then run live `backend/tests` and Unity Test Runner; review new screenshots
before moving a draft PR to ready. Merge the separate hero v3 PR only after
its own Unity checks and ownership approval.
