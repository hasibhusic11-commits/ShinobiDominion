# QA results: M1 + M2 (2026-11-11)

Setup:
- Unity 6000.3.28f1, project `unity/`.
- Local server: `docker compose -p sd-raid up -d --build` in `backend`, both containers healthy.
- `SD_SERVER_KEY` taken from `backend/.env`.

| Run | Command | Result |
|---|---|---|
| Full PlayMode suite | `tools/unity/run_hero_sprites.ps1 -Filter '.' -SkipBuild` with `SD_CAPTURE_DIR` set | **155 passed, 0 failed, 0 skipped** |
| Overhaul + mode tests after the last fix | same script, `-Filter 'UiOverhaulTests\|ModeTests'` | **24 passed, 0 failed** |
| EditMode suite | `Unity.exe -batchmode -runTests -testPlatform EditMode` | **214 passed, 0 failed, 2 skipped** |

The first full run failed 38 tests because Docker Desktop had stopped and the server was unreachable. Every failure was "Could not connect" in SetUp. The suite passed fully once Docker and the server were restarted.

## What the new tests check (`Tests/PlayMode/UiOverhaulTests.cs`)
- `BattleMenu_ShowsFiveIllustratedModes_AndEveryRouteStillWorks`:
  - all five mode buttons are open with a hero on a base;
  - each card shows its own `mode_*` painting;
  - the real hero note and dungeon star count are shown;
  - Siege opens by its button and by tapping the whole card;
  - Arena, Trials, and Dungeons by card tap each reach their real screens.
- `BattleMenu_WithNoHero_LocksHeroModes_AndSaysWhy`:
  - Raid, Arena and Trials are locked, with the reason shown;
  - Siege and Dungeons stay open.
- `CaptureBattleMenuAndKit`: the screenshots at four shapes, plus the component gallery.

The existing `ModeTests` and `ServerIntegrationTests` still drive the menu by the same button names, and they pass.

## Not verified
- On a real phone or tablet: safe-area notches, touch feel and frame time.
- Reduced-motion: no such option exists yet.
- Controller or keyboard focus.
