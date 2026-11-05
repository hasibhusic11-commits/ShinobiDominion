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
- Hero bases are capped at six at every Dojo level, per non-negotiable rule 4.
- New players start with two placed Hero Bases and build the other four.
- Removing a building gives no refund.
- All numbers in `Resources/Config/buildings.json` are test values and the file is marked not approved.

## ADR-012: Nakama's Go runtime with `crypto/rand`
Decided in Phase 3. See ADR-018 for persistence.

## ADR-018: Custom Postgres tables, not Nakama's wallet and storage
Each change of value is one SQL transaction in our own tables, starting with a lock on the player's row. This gives all-or-nothing behaviour directly, instead of relying on separate wallet and storage writes, and it was proven with concurrency and crash tests. The cost is that Nakama's built-in wallet and storage console views do not show this data, and one player's mutations run strictly one at a time, which is fine at player scale.

## ADR-019: Village stays on the device for now
Phase 3's brief names inventory, summoning and evolution, so those moved first. Village layout and gold have not moved, which means rule 10 is not yet fully met. See `SECURITY_MODEL.md`.

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

## ADR-013: Docker for the local server
Go plugins do not run natively on Windows, so the server runs in Docker on WSL2. Unity, Docker, Nakama and Postgres together are tight on this PC's 7.3 GB. It worked for every test here, but an active Unity Editor session alongside the server has not been measured.
