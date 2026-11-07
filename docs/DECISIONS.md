# Decisions

The brief in `docs/spec/` wins over anything here. These cover what the brief left open. All are reversible unless noted.

## ADR-001: Unity 6000.3.28f1 LTS
Newest Unity 6 LTS on the release API on 2026-11-05, changeset `8f082d2fe476`. The other LTS line, 6000.0.85f2, is older with no benefit for a new project.

## ADR-002: Game rules live in an engine-free Core assembly
`ShinobiDominion.Core` has `noEngineReferences: true`. Grid, placement, walls, saves and navigation rules compile with no Unity at all.
- A .NET harness at `tools/core-tests` runs those tests in about a second, with no editor or license.
- The harness compiles at C# 9, Unity 6's language level, so newer syntax cannot slip in.
- The same files run under Unity's Test Runner.
- The rules can be ported line for line to the server in Phase 3.

## ADR-003: Newtonsoft.Json for saves and config
Unity's `JsonUtility` needs the engine, which would stop Core tests running outside Unity. `com.unity.nuget.newtonsoft-json` is an official Unity package, and the same library comes from NuGet in the harness.

## ADR-004: uGUI built from code, with the built-in font
UI Toolkit needs theme assets and TextMeshPro needs an imported resources folder, and both add setup that has nothing to do with proving the layout. uGUI with the legacy runtime font has no such setup. Replace the font before release, because it will not cover every language.

## ADR-005: Procedural placeholder art and a code-built scene
The Village scene holds a single `VillageBootstrap` object, and everything else is created at runtime. Buildings are generated meshes in a neutral palette, each with its own silhouette. All of it is honest placeholder art and is replaced in Phase 5.

## ADR-006: Pathfinding (provisional, decide in Phase 4)
Lean toward deterministic grid A* over the village's integer grid. The server has to re-run or verify battles, and NavMesh is neither deterministic nor available in the Go backend. `com.unity.ai.navigation` is not installed for now. The obsolete NavMeshComponents repo is rejected.

## ADR-007: Tap picking uses render bounds, not physics
No colliders exist. A tap tests the ray against each building's bounds, so tapping a tall tower's roof selects the tower. Physics adds nothing here, and this avoids hundreds of wall colliders.

## ADR-008: Wall tool behaviour
- A diagonal drag locks to the longer axis, and ties go to X.
- A run is all-or-nothing. If any cell is invalid, the whole run is refused and those cells show red with an X.
- Crossing an existing wall counts as invalid.

## ADR-009: Local save is a prototype stand-in
Phase 1 stores the village and wallet in `persistentDataPath/village_v1.json`, which a player could edit. This does **not** meet the brief's online integrity rule. Phase 3 must move authority to Nakama before any of this state has value. Saves are validated on load, and a rejected file is kept as `.rejected`.

## ADR-010: "Spirit Turret" instead of "Chakra Turret"
The brief lists "Chakra Turret" as an example but also requires original terminology such as Spirit Force. Using Spirit Turret avoids the closer association.

## ADR-011: Rule details the brief left open
- Dojo HQ, Builder Hall, Hero Altar and Summoning Gate can be moved but not removed, since removing them would lock out their menus.
- Hero bases are capped at six at every Dojo level, per non-negotiable rule 4. *Replaced by ADR-030: one base, one hero.*
- New players start with two placed Hero Bases and build the other four. *Replaced by ADR-030: they start with their one base.*
- Removing a building gives no refund.
- All numbers in `Resources/Config/buildings.json` are test values and the file is marked not approved.

## ADR-012: Nakama's Go runtime with `crypto/rand`
Decided in Phase 3. See ADR-018 for persistence.

## ADR-018: Custom Postgres tables, not Nakama's wallet and storage
Each change of value is one SQL transaction in our own tables, starting with a lock on the player's row. This gives all-or-nothing behaviour directly, instead of relying on separate wallet and storage writes, and it was proven with concurrency and crash tests. The cost is that Nakama's built-in wallet and storage console views do not show this data, and one player's mutations run strictly one at a time, which is fine at player scale.

## ADR-019: Village on the server, with rules in two languages
The server enforces village rules in Go, in the same locked transaction as everything else. The client keeps its C# copy for instant previews only. A shared scenario file runs through both, so a rule changed on one side alone fails a test.

Consequences, reversible unless noted:
- **Undo is offline-only.** Online it would be a free refund, and a building "undo" needs its own rules first.
- **Earlier local villages are not migrated.** They were forgeable test data, so each player starts from the server's starter village.
- **Editor and development builds keep offline play** for testing. Release builds do not.
- **Hero assignment now needs a real Hero Base**, and removing a base frees its hero in the same transaction.

## ADR-020: Local machine limits
WSL2 is capped at 3 GB in `%USERPROFILE%\.wslconfig`, because this PC has 7.3 GB and Unity also needs room.

## ADR-014: Heroes stay on the device until Phase 3
Roster rules live in Core so they can move to the server, and they already use idempotency keys. Release builds start with an empty roster. The dev roster is compiled in only for the editor and development builds, and it is a direct grant, never a summon.

## ADR-015: Hero power formula
`stat = base + growth * (200 * evolutionIndex + level - 1)`, with growth required to be non-negative. Evo I Lv1 is always one step above Base Lv200. Numbers are test values.

## ADR-016: Altar opens straight into the roster
Building options such as Move and Upgrade sit behind an "Altar building options" button, since the roster is what the Altar is for.

## ADR-017: Genre conventions adopted, Castle Clash itself not copied
The owner shared Castle Clash screenshots as a style reference. Adopted as genre conventions: dense isometric compound, chunky framed UI, card roster grid, full-screen hero and build screens, shop-style building cards, and collect markers over resource buildings, with a tap collecting straight away.

Not adopted: Castle Clash's art, UI assets, icons, names or specific look. The brief forbids it, and it is a legal risk. This game's identity is dark lacquer and brass UI, plaster-and-tile castle walls and pagodas.

**Home screen, decided by the owner on 2026-11-05:** ATTACK only. The screenshots show icon rails and a five-button bottom bar, and the owner chose to keep non-negotiable rule 2 instead. Do not add them. Management screens are full-screen but still open only from buildings.

## ADR-027: How troops work, added on 2026-11-05
Follows the owner's ADR-021. Up to four Troop Camps, gated by Dojo level, and a Training Hall (the old Training Grounds). Stronger troops take more camp space. Choices Claude made that the owner can overrule:
- Training is instant, as upgrades are. Timers can come later.
- Troops are used up when they land, win or lose, and those not landed stay in camp.
- Troops are single-level and have no abilities. Heroes keep theirs.
- If a camp is removed, troops already trained stay, but no more can be trained until there is room.
- A raid still needs at least one hero. At most 200 troops can land in one raid, to bound the server's work.
- Troop names and all numbers are test values. Offline practice troops aren't saved.


## ADR-031: Heroes hop walls, everything else goes around or breaks through, decided by the owner on 2026-11-07
- **Every hero** (attacking or guarding) hops over walls and never needs to break one. Hopping a wall cell takes twice as long as a step. Ranged heroes already shoot over walls from where they stand.
- **Troops** (and monster waves, when they exist) take whichever is faster in real time: walking around, or breaking the wall in the way. The cost of breaking is the hits that wall's health needs at the troop's damage and attack speed.
- Guards can now leave their compound over the walls to reach attackers. Buildings still block everyone.
- The same rule is in the game's and the server's battle code, checked by the shared battle logs.

## ADR-030: One hero at a time, decided by the owner on 2026-11-07
The owner decided that only one hero is used at a time. **This replaces non-negotiable rule 4 ("Six on the field")** in the master prompt, and the owner made that call.
- A village has **one Hero Base** at every Dojo level, and new players start with it placed.
- That one hero leads every raid and guards the village. Troops still fill out the army.
- Putting another hero on the base swaps them.
- Every dungeon stage and rival village has **one enemy guard**.
- Existing players keep the hero on their earliest base, and the others go back to the roster (`007_one_hero.sql`). Extra bases built before this stay, empty, and the hero cannot be put on a second one. Offline saves are fixed the same way when they load.
- The first two stages were made easier so one level 1 hero with a small army can win them. A test checks every chapter for one hero at the expected level with the army a player would have by then.
- Still open: whether rarer heroes should matter more now, since only one is ever used.

## ADR-029: A dungeon campaign and battle feel, added on 2026-11-07
The owner asked for the game to feel like Castle Clash, so this borrows the genre's usual shape and none of its art, names or layouts. The home screen is unchanged: ATTACK stays the only thing docked at the bottom, with no icon rail, and there is no energy counter.
- **15 stages in 3 chapters**, opened in order. The server refuses a locked stage with `RAID_LOCKED`, and the client only draws the map.
- **Rewards are all decided by the server.** A win gives each hero who was landed some XP, a first clear gives crystals, and each chapter's boss gives one catalyst, once. A loss pays nothing.
- **Hero XP from dungeons is capped at 30 wins in 24 hours**, so it cannot be farmed without limit.
- **Raids on rivals give no dungeon rewards.**
- **Battle feel:** a battle panel with loot, stars and damage, damage numbers, a gravestone for each fighter who falls, a results screen with rewards, and sounds made in code. A sound switch is in the Menu. Walkers in the village are for show only, and nothing in the rules knows about them.
- All values are test values, made by `tools/balance/generate_campaign.py`. Under ADR-030 each stage has one guard. How generous crystals and catalysts should be is still the owner's call.
## ADR-028: Defender heroes and raids on rivals, added on 2026-11-07
Claude made these choices while the owner slept, and the owner can overrule any of them. All numbers are test values.
- **Guards:** heroes on a village's Hero Bases defend it. Each stands on its base, notices attackers within 8 cells, chases up to 14 cells from its post, then walks back. Roles keep their abilities. Attackers stop and fight any guard in reach. Stars still count only buildings, so a raid can win with a guard alive.
- **Rivals:** a raid can target a real player's village. The server chooses the closest Dojo level, preferring villages with guards, then at random. Players can't choose a rival.
- **Snapshots:** the rival's layout and guards are copied when the raid starts, so nothing they do later changes it.
- **A raid costs the rival nothing.** No gold, buildings or heroes are lost. This is the largest choice here, because Castle Clash style raids usually take resources. It avoids griefing and needs no shields while the player count is small. Taking resources can be added later.
- **Farming limits, with no energy:** a rival raid pays 150 gold per star per rival Dojo level, once per rival per 24 hours and on at most 10 raids in 24 hours. Raids are otherwise unlimited.
- **Privacy:** the rival's hero ids aren't sent, and the defense log never says who attacked.

## ADR-026: How raids work, added on 2026-11-05
The first raid slice follows the brief's phase 4. Choices Claude made that the owner can overrule:
- **The server fights the battle.** It keeps the defender and team from when the raid started, takes only where heroes stand, runs the battle and pays any reward. The client plays the log back. The battle is whole numbers with no randomness, and the Go and C# copies must produce identical logs in shared tests.
- **Rewards are limited without energy.** Raids can be repeated freely, but gold is only paid for stars beyond the player's best on that raid.
- **Stars:** 50% damage, destroying the Dojo, and 100% damage. Walls don't count towards damage.
- **Heroes can't land within 2 cells of any enemy building.**
- **Not built yet:** defender heroes, player-versus-player and manual abilities. All role, tower and reward numbers are test values.

## ADR-025: 15 levels per building, decided by the owner on 2026-11-05
Every building has exactly 15 levels, each with its own row of stats in `buildings.json`, generated by `tools/balance/generate_building_levels.ps1`. The client and the server read the same file.
- Growth follows the genre's pattern: fixed steps for attack, might and hero bonuses; steps every few levels for range; faster and faster growth for HP, cost and storage. Walls gain HP fastest.
- The shapes were studied from a public Castle Clash handbook (en.casclash.com), not the Fandom wiki, which refused automated access. The owner chose on 2026-11-05 to keep our own numbers instead of matching Castle Clash's more closely. They are still test values to tune, because the brief says balance stays tunable, and the source's 30-level tables don't fit 15 levels anyway.
- New rule, added by Claude and easy to remove: higher levels need a Dojo level (level 5 needs Dojo 3, 8 needs 6, 11 needs 9, 14 needs 12). The Dojo itself has no such limit.
- Upgrades are instant. The handbook's upgrade timers are not implemented.
- Attack, range, might and hero bonus are stored and shown, but nothing uses them until combat in Phase 4.

## ADR-024: The outside is a mountain, decided by the owner on 2026-11-05
Replaces ADR-023. No houses, gate, training yard, bridge or giant trees remain outside the map. The village sits on a mountain shelf: the ground climbs through forest and bare rock to snow behind the map, and falls away as a cliff to a lower forest in front. The map itself stays at height 0, so placement is unaffected. Only trees and rocks whose tops stay below map level grow in front of the map, so nothing can hide the village.
## ADR-023: Surroundings with a hidden ninja village feel (replaced by ADR-024)
The owner asked for a Naruto-themed outside area. Actual Naruto material stays out: no Konoha, leaf symbol, Hokage monument or characters, because they are protected and non-negotiable rule 9 forbids them. The area instead uses general shinobi-setting ideas in original form: rooftop districts, a great gate, giant trees, a footbridge and a training yard. No carved faces on cliffs, since that would be too close to one specific setting. All of it sits outside the 50 x 50 grid.

## ADR-022: No budget, decided by the owner on 2026-11-05
The owner will not spend money on this game, so no purchased asset packs and no commissioned artists. Art stays generated by Claude. This limits how close it can get to hand-painted games, so the goal is the best chunky, toy-like style code can make. Whether free CC0 assets count as allowed is still open.

## ADR-021: Troops, decided by the owner on 2026-11-05
Raids use troops as well as heroes, and this adds to the brief, which described only heroes.
- Troops are separate units. Heroes do not lead them.
- The village has **4 troop camps** and a **training hall**, which makes troops that fill the camps' space.
- **Each troop takes up camp space according to how strong it is.**
- Not yet decided: camp capacity, troop types, training time and cost, and whether camps are upgradable. These will be test values when built.

## ADR-013: Docker for the local server
Go plugins do not run natively on Windows, so the server runs in Docker on WSL2. Unity, Docker, Nakama and Postgres together are tight on this PC's 7.3 GB. It worked for every test here, but an active Unity Editor session alongside the server has not been measured.
