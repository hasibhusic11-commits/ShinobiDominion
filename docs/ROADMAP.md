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

## Phase 3c: mountain and 15 building levels — done, awaiting your review
The outside is a mountain with no houses. All buildings have 15 levels with their own stats and five texture packs. See ADR-024 and ADR-025.

## Phase 4: combat vertical slice — in progress
First slice built, awaiting your playtest: ATTACK opens a scripted raid, heroes from your bases deploy, walls and towers work, role abilities fire, and the server decides the result and reward (ADR-026).

Troops are built too: 4 camps, a Training Hall, camp space by strength, and landing them in raids (ADR-027).

Defender heroes and raids on real players' villages are built, awaiting your playtest (ADR-028). Verified with shared battle logs in C# and Go, the live server and the real client, but only with test accounts, not two people.

Still to do in this phase: battle sound.

## Hero designs and sharper UI: awaiting your review
All 30 heroes have an original design and a generated model, and only Legendaries have four looks. See `HERO_DESIGNS.md` and `docs/hero_designs/`. The 24 heroes outside the current pool are not summonable until you approve them, because adding them changes the odds. The interface was redrawn with crisper frames, buttons and icons.

## Phase 5: polish and ship readiness — not started

## Visual overhaul — done as temporary art, awaiting your review
Curved-roof ninja architecture for every building, modular walls with level tiers, Hero Bases with figures and emblems, textured ground, forest, mountains, waterfall, toon lighting, smoother camera and a quality setting. See `ART_STATUS.md` for what still needs an artist, and `VISUAL_BASELINE.md` for the before state and asset evaluation.

## Overnight build, 2026-11-08 to 09: game modes, guilds, timers, research, gear and smoothness. Awaiting your playtest. Screenshots in `docs/ui/v15/`
ATTACK now opens a Battle menu with five modes (ADR-039, ADR-045):
- **Raid:** rival villages.
- **Dungeons.**
- **Village Siege:** 10 levels of waves against your own village.
- **Arena:** hero duels for points, with rankings.
- **Trial Tower:** 30 floors of guardian heroes.

The home screen's round shortcuts now work:
- **Daily Quests:** 7 quests that pay gems.
- **Daily Gift:** gold, Mana and gems once a day.
- **Rankings.**
- **Mail:** raids on your village.

Summoning now has a reveal: a burst of light in the rarity's colour, then the card. A ten-pull reveals its rarest hero.

Smoothness:
- 60 fps instead of Unity's mobile default of 30.
- Counters that roll without fighting each other.
- A camera that springs back at the edges.
- Faster texture building at startup.

**Build timers (ADR-040):**
- Upgrades past level 2 take time and need one of your builders (2 at first).
- A timer floats over each upgrading building.
- Gems finish an upgrade at once.

Verified:
- The shared rule scenarios pass in C# and Go.
- A backend test checks that ten finishes at once charge one gem once.
- Offline Unity tests cover the timer, the finish medallion and an upgrade finishing on its own.

**Feel:**
- Battles have an x1, x2 or x3 speed button, remembered between battles.
- The camera drifts after the fight unless you have just moved it.
- Builders send up sparks and dust over upgrading buildings.
- New players get a five-step guide with an arrow (collect, summon, assign, first battle, first upgrade). It never blocks a tap, can be skipped, and is remembered.

**Loading screen:** a title screen while the game first reaches the server, so the village never visibly swaps.

**Music:** background music composed in code: koto, drone and taiko in the village, with drums and a shaker in battle. There is a Music switch in the Menu. Samples are in `docs/audio/`. Nobody has listened to it yet.

**Trial Tower (ADR-045):** a fifth battle mode. Climb 30 floors of guardian heroes; each floor pays once, on its first clear.

**Hero gear (ADR-044):** Weapon, Armor and Charm on every hero, upgraded with gold and Mana from the hero page. The server applies gear in every battle.

**Chest and shop (ADR-043):** a free Treasure Chest every 4 hours, and a gem shop that swaps gems for gold or Mana at fixed rates. No real money anywhere. Every home-screen button now works.

**Guilds and names (ADR-042):**
- Set your name in the Menu.
- The friends shortcut opens Guilds: found one for 300 gems, or join one (up to 30 members), with a member list and guild chat.
- Chat has no word filter or reporting yet, which a public release needs.

**Research (ADR-041):**
- The Research Hut runs troop studies paid in Mana: +8% HP and attack per level.
- One study runs at a time; gems finish one at once.
- Studies apply to your troops in raids and dungeons.

`Campaign_ThroughTheScreen_ShowsLockedStages_AndServerRefusesThem` failed in some full runs. Its server refusal could take longer than the test's fixed one-second wait, so the test now waits for the answer. The Battle menu also now loads its list once any earlier request finishes, instead of dropping it.

| Check | Result |
|---|---|
| .NET harness (`tools/core-tests`) | 177 of 177 passed |
| Go unit tests, including Siege balance and Arena duels | passed; part of the server build |
| Backend suite, including gift, quest, Siege, Arena, timer, research, guild, chest, shop, gear and Trial Tower tests | passed |
| Unity PlayMode against the live server, including `ModeTests`, `UpgradeTimerTests` and `GuideTests` | 132 of 132 passed |

**Not verified:**
- How any of it feels in your hands or on a phone.
- Two real people in the Arena or in one guild.
- The music: nobody has listened to it yet.

All the new numbers are test values. Guild chat needs a word filter and reporting before any public release.

## Building roster, new art, switchable towers and the painted map, 2026-11-09. Awaiting your playtest (ADR-046)
- Your 20-building list with your footprints, minus the Watchtower you dropped. Every building uses your new art, drawn at 80% of its square.
- Arrow, Cannon and Magic Towers are one building with a Switch button, and each fires its own way.
- Your counts: one Warehouse; four Gold Mines and Mana Mills, five from Town Hall 5, six from Town Hall 10.
- Bomb and Hero Traps work on the server and now show in battle playback.
- Mana storage has a cap, raised by the Mana Vault and Warehouse.
- Your painted map is the ground, with the grid running corner to corner between its torches. The camera stays inside it.

**Verified on 2026-11-09:**
- Server: Go unit tests and shared rule and battle scenarios pass, including tower switching, the new count limits and a Cannon and Magic Tower battle that both copies of the rules play identically. The server integration suite passes (92 s).
- Unity: all 178 EditMode tests and all 136 PlayMode tests pass against the rebuilt local server, including the Switch button and the painted-map camera tests.
- Not run: the .NET core test runner, which Windows Application Control blocks on this PC. The same tests ran inside Unity instead.

**Balance report** (`BALANCE_REPORT.md`, Phase 5): every hero against every campaign stage, the three towers side by side, and the economy in hours of mine income. Regenerate it with `SD_BALANCE_REPORT=<path>` on the EditMode test `BalanceReportTests`. It found the Magic Tower beat the Arrow Tower at everything, now fixed, and that a Common hero could not clear the campaign from stage 4. Fixed by ADR-047: dungeons now hold more guards and towers, each scaled by a tuned strength, so Common and Elite heroes can farm every stage.

**Still to do:**
- Art for the Builder Hall and Summoning Gate (still old).
- Your numbers for the tower limit and the other counts you have not set.

## Earlier test results (2026-11-07, branch `feature/campaign`)
Dungeon campaign and battle feel, awaiting your playtest (ADR-029).
| Check | Result |
|---|---|
| .NET harness (`tools/core-tests`) | 147 of 147 passed |
| Unity PlayMode, including live-server tests | 96 of 96 passed |
| Backend suite, including 9 campaign tests | passed |
| Backend unit tests with shared battle logs in C# and Go | passed; part of the server build |

Not verified: sounds were checked as non-silent clips, but nobody has listened to them. Not run on a phone. Production-mode tests were not rerun for this branch. Balance has only been tested by simulated teams, not by a person playing.

## Previous test results (2026-11-07, branch `feature/raid-defenders`)
| Suite | Result |
|---|---|
| .NET harness (`tools/core-tests`) | 135 of 135 passed |
| Go server unit tests, including the shared battle logs | passed; part of the server build |
| Backend attack and concurrency suite, with new rival tests | passed |
| Production mode: no dev calls, rival can't be chosen | passed |
| Unity PlayMode against the live server, 10 suites | 89 of 89 passed |

Not rerun this time: crash recovery, Windows release player, `ScreenCaptureTests`.

## Earlier test results (2026-11-05)
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
