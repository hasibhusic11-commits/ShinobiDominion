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

## ADR-052: Battle spells, cast in a live raid and fought again by the server, 2026-11-10
Four spells the attacker can cast during a live raid (ADR-050). The rules layer is the data, the checks, the sim on both sides, the server call and the tests. The screen layer (the cast buttons, the ring that aims and how each spell is drawn) is described under **Screen**.

- **Spells.** `ember_rain` (Ember Rain) damages enemy buildings and guards in a ring, in 3 pulses over 3 seconds. `mending_mist` (Mending Mist) heals the attacker's heroes and troops in a ring, in 5 pulses over 5 seconds. `frost_seal` (Frost Seal) stops towers and guards in a ring from acting for 4 seconds at spell level 1. `war_drum` (War Drum) makes the attacker's units in a ring deal more damage for 5 seconds. The names and every number are ours, and all are test values.
- **Data.** `spells.json` (client `Resources/Config`, copied into the server image like `troops.json`) holds, per spell, the Relic Hall level (building id `research_hut`) that opens it, the radius, the duration, the pulse count and a power per spell level. `SpellCatalog` and Go `spellCatalog` load it and refuse a bad file.
- **Who may cast.** The attacker's own Relic Hall decides: Ember Rain at hall level 1, Mending Mist at 2, Frost Seal at 3, War Drum at 4. The hall level that opens a spell is spell level 1, and each hall level after it is one spell level more. Each open spell can be cast once per battle. There is no cost, no inventory and no database change. The server puts the list (`spells`: id and level) in the session that `sd_raid_start`, `sd_rival_start` and `sd_raid_revenge` return, and `RaidSession.spells` holds it. `SpellCatalog.Available(village)` gives the same list for offline practice.
- **Casts.** A cast is `RaidSpellCast { spellId, x, z, tick }`: the cell it is centred on (any cell of the grid, buildings included) and the tick it was cast on. `sd_raid_deploy` takes an optional `casts` list next to placements and troop stacks. A cast never says its level. The server uses its own.
- **Checks** (`SpellCatalog.CastError`, Go `castError`, same names in the same order). Only a battle with an end tick may have casts, otherwise `BAD_DEPLOY_TICK`. Then, for each cast in turn: the spell must be in the session's list (`SPELL_LOCKED`), not cast before (`SPELL_USED`), on a cell of the grid (`BAD_SPELL_CELL`), at a tick from 0 to before the end tick (`BAD_DEPLOY_TICK`), and not earlier than the cast before it (`DEPLOY_TICK_ORDER`). Two casts may share a tick.
- **Sim.** `BattleSim.CastSpell` casts at the current tick, like `Deploy`. The spell lands at the start of the next tick, after any cards that land then, and applies before anyone moves. So a unit dropped on the same tick as a cast is already there when it lands. Pulses are spread evenly over the duration, the first on landing, and each pulse takes whatever is in the ring at that moment. Buildings are measured to their nearest edge, units and guards to where they stand. Ember Rain hits walls too (they still do not count toward the damage percent) and leaves hidden traps alone. Guards take it less their defense, and half while guarding. Mending Mist never heals past full HP. Frost Seal uses the stun that towers already had and gives guards the same. War Drum reaches only the units in the ring when it lands, and raises their hits on buildings and guards, not their healing. `Run` and Go `runTimedBattle` given the same drops, casts and end tick produce the identical log and checksum. A battle still ends when the last building falls, also when a spell fells it.
- **Log.** Seven new events after `Deploy`, numbered 20 to 26: `SpellCast` (cast number, cell x, cell z, effect), `SpellPulse` (cast, which pulse, of how many, radius), `SpellHit` (cast, building, damage, HP left), `SpellHitDefender` (cast, guard, damage, HP left), `SpellHeal` (cast, unit, amount, HP now), `SpellFreeze` (cast, tower or guard, which one, ticks) and `SpellBoost` (cast, unit, ticks, percent). No old number changed.
- **Client.** `IGameServer.RaidDeployAsync` takes the casts as an optional last argument and sends `casts` only when there are some. The raid screen sends every cast made in the battle with the drops.
- **Server.** `sd_raid_deploy` works out the allowed spells again from the attacker's village as it is then, checks the casts after the drops, turns them into sim inputs at its own levels and fights them with `runTimedBattle`. The request hash that catches a reused key covers the casts.
- **Screen.** A row of round buttons sits above the cards, after the Auto button: one per spell the session allows, with its level under it. Tapping one picks it (it glows, and its name and what it does show under the clock); the next tap on the map casts it there, on any cell, the enemy base included. A cast is refused, with the reason in words, until a hero or a troop has been dropped, because the server refuses a battle with casts and no army. A spent spell turns grey and reads "Used". With a mouse, a ring the width of the spell follows the pointer. In the fight a damage spell draws a target ring and falling fire that lands 0.22 seconds after each pulse, a heal draws a green ring and rising light, a freeze draws ice over each tower and guard for as long as it holds, and a boost draws a gold ring and a drum. The device makes the same checks as the server (`SpellCatalog.CastError`), so a cast it shows is one the server accepts. The result panel counts the spells cast. A raid opens on the heart of the enemy base: the Town Hall and every building within 4 cells of the group around it, plus 6 cells of open ground on each side, filling 94% of the screen and set 3% of its height below the middle, between the clock and the cards. A building further out is left for the player to pan to. In a live raid the camera does not follow the fight, because the player is aiming drops and spells at the base; a fight played back (Siege, Arena, Trial Tower) is still followed. A fallen building leaves burnt ground, the broken foot of its own walls cut from its own picture, and loose stones. Words lie over the base only while they are needed: how to start, what a picked spell does, or why a tap was refused.
- **Tests.** The shared vectors gained five spell scenarios (each spell once, a cast on the same tick as a drop, a cast that ends the battle by felling the last building, and two more) and a `refusedCasts` list with every refusal, which both sides must answer alike. C# and Go each have their own spell tests. `backend/tests/spells_test.go` fights a live raid with casts against a running server. `RaidSpellTests` (PlayMode) checks the buttons, the refusal before a drop, once per battle, the cast in the battle log, the aim ring, the opening view, that the view stays on the base once the fight is on, and the ruin a fallen building leaves.
- **Not changed.** Battles without casts keep their exact logs and checksums: the 15 earlier vectors pass untouched. Siege, Arena and Trial Tower have no spells. No table, migration or currency changed.
- **Known gaps.** The session does not store the spell list, so the server works it out again when the battle is sent. If a Relic Hall upgrade finishes in between, the server's spells are a level above the device's. The hall never goes down, so a legal cast is never refused for it, but the two results differ. The server's stands and the client logs the checksum difference, as in ADR-050. A frozen guard's own timers (guard, haste, attack) stand still while it is frozen. The path planner prices walls by a unit's attack without the boost. Once per battle is the only limit, so balance rests on the hall levels alone. The device ends a battle by itself when the army is down and no spell is still falling; it does not wait for a spell that has not been cast, so the player ends such a battle with the button or lets the clock run out. On a touch screen the aim ring cannot follow a finger that is not down, so the ring shows only when the spell lands.

## ADR-051: Buildings are moved, never removed, 2026-11-09
The owner's rule, like Castle Clash. Once placed, a building, tower, wall or trap stays. It can be moved, upgraded and (for towers) switched, but never removed.

- The Remove button is gone from the action row and the details panel.
- `Village.TryRemove` and the server's `village.remove` always answer `NotRemovable` (`NotFound` for an unknown id). `sd_village_remove` still exists, so an old or tampered client gets that refusal instead of an unknown call.
- `removable` in `buildings.json` now only decides what the build catalog offers. The Town Hall is not offered.
- The shared rules vectors and the tests that removed buildings now check the refusal.

## ADR-050: Raids are fought live, card by card, and the server fights them again, 2026-11-09
The owner chose real-time deploying, like Castle Clash (`GENRE_GAP_REVIEW.md`). This replaces placing everything before Start Battle. The server still decides every result.

- **Timed deploys.** Each dropped unit has a `deployTick`. It lands after that tick, acts from the next one, and the log records it as a `Deploy` event (19: unit, x, z). A timed battle (one with an end tick) ends at that tick or when every building is down. It never ends just because the field is empty, since more cards may still fall. Battles without an end tick (Siege, Arena, Trial Tower, and the old raid request) are unchanged, and so are their logs.
- **Unit numbers.** Units are numbered in landing order: by tick, heroes before troops on the same tick, otherwise as listed (`BattleSim.LandingOrder`, Go `landingOrder`). So the client's live numbering and the server's list (heroes, then troops) always agree, whatever order the cards fell in within one tick.
- **Checks** (`DeployError`, same names on both sides). Cells are checked as before. Every tick must be from 0 to before the end tick, and the end tick at most 1800. Ticks must rise among heroes and among troops (`DEPLOY_TICK_ORDER`), otherwise the error is `BAD_DEPLOY_TICK`. Two heroes may share a cell only on different ticks. A request without an end tick may not carry ticks.
- **Live stepping.** `BattleSim.Start` begins on an empty field at tick 0. `Deploy` drops a card at the current tick, `Step` plays one tick, and `Events` and `Finish` report what happened. `Run` with the same drops and end tick gives the identical log. The Go port runs the same `step`. The shared vectors gained three timed scenarios: waves of troops ended early, a second wave beside guards, and a hero landing where another left.
- **Client.** Scouting a raid starts the battle: an empty field with the clock running at real speed, and no speed button. Tapping a card, then open ground, drops it now, and this device's copy shows it at once. Dropped cards stay dropped. **Auto** drops everything left. **End Battle** stops the battle, and leaves at no cost if nothing was dropped. The battle also ends when every building is down, when time is up, or when everyone dropped has fallen and no card is left. Then the drops and the end tick go to `sd_raid_deploy`, and the result screen shows the server's result. If its checksum differs from this device's, the client logs it and still shows the server's.
- **Server.** `sd_raid_deploy` takes `tick` on each placement and troop stack, plus `endTick`, and fights them again with `runTimedBattle`. Dungeons, rivals and revenge all use it. It refuses `BATTLE_CLOCK_AHEAD` when the end tick (and so every drop) is later than the real time since the raid started, plus a 3-second margin. So a client can't fight the battle, see how it goes and send a better list. Without `endTick` it is the old all-at-once battle, which a live player can also do by dropping everything at tick 0.
- **Not changed.** Siege, Arena and Trial Tower are still fought by the server alone and played back.
- **Known gap.** Troops are used up only when the battle is sent. A player who closes the game mid-battle walks away from the raid: no reward, but no troops lost either. If that matters, charge troops or record a loss when a started raid expires.

## ADR-049: Raids on real players take real loot, with shields and revenge, 2026-11-09
The owner chose the genre's rule (`GENRE_GAP_REVIEW.md`). This replaces ADR-028's "a raid never costs the rival anything" and its daily pay limits.

- **Loot on offer.** When a raid begins, the server records 20% of the gold and 20% of the Mana the rival has. The attack screen shows those amounts as the available loot.
- **Loot taken.** The attacker wins that share times the damage done (60% damage takes 12%). It is limited by what the rival still has when the battle resolves and by the attacker's storage. The rival loses exactly what the attacker gains, and nothing else of theirs changes.
- **Shield.** A rival hit for 30% damage or more is shielded for 12 hours: matchmaking skips them. Starting a raid on another player ends your own shield.
- **Defense log and revenge.** Mail lists raids on your village, with the attacker's game name (never their account id), the stars, the damage and what was lost. Each raid offers Revenge once: it starts a raid on that attacker, even through their shield.
- Server: `loot.go` (`beginRivalRaid`, `takeLoot`, `rpcRevenge`, `rpcDefenseLog`), RPC `sd_raid_revenge`, migration 017 (`sd_player.shield_until`, `sd_raid.loot_gold`, `loot_mana`, `revenged`). The player state carries `shieldUntilMs`.

## ADR-048: Up to six heroes, 2026-11-09
The owner asked for the game to play like Castle Clash (`GENRE_GAP_REVIEW.md`) and chose six heroes. This replaces ADR-030's one hero at a time.

- Up to six heroes stand on Hero Bases. They all guard the village and all fight in raids. Client: `Village.MaxAssignedHeroes = 6`. Server: `maxBaseSlots = 6`.
- Hero Bases unlock with the Town Hall: 1 at Town Hall 1, 2 at 2, 3 at 4, 4 at 6, 5 at 8, and 6 from Town Hall 10.
- Raid teams and rival guards are every hero on a base, so they grow without other changes. Arena and Trial Tower stay one hero against one, as duels.
- The balance tools field the team a player has by each stage: one hero at stage 1, then one more about every three stages, up to six. The campaign was retuned with them (strengths up to 300% are now allowed); Commons still win every stage at the expected level.

## ADR-047: Dungeons are not villages, and Common and Elite heroes can farm them, 2026-11-09
The balance report (`BALANCE_REPORT.md`) showed a Common hero could not clear the campaign from stage 4, and Elites mostly lost at the expected level. Legendaries are rare and summoning has no pity, so most players would be stuck. The owner's direction: Common and Elite heroes must clear enough to farm, and, as in Castle Clash, dungeons are not held to a village's limits.

**Dungeons may be crowded.** This supersedes ADR-030's one-guard rule for dungeons only; player villages keep one guard.
- Guards: 1, rising to 5 from stage 13. Commons guard the early stages, Commons and Elites the middle, Elites the late ones. A Legendary leads each chapter's last stage (5, 10 and 15). Stage 2's Legendary guard is now a Common.
- Towers: Arrow Towers from the start, Magic Towers from stage 3, Cannon Towers from stage 4. Stage 15 has 18 towers.

**Each dungeon has a strength.** `strength` in `raids.json` is a percent that scales the HP and attack of every building, wall, tower and guard in it. The client (`RaidDefinition.Scaled`) and the server (`raidDefinition.scaled`) apply it the same way.

**The strengths are tuned, not guessed.** `BalanceReportTests.TuneCampaign` (run with `SD_CAMPAIGN_TUNE=tools/balance/campaign_strength.json`) finds, for each stage, the strongest setting up to 100% at which at least 70% of Common heroes win a star at the level expected there. `tools/balance/generate_campaign.js` reads that file. It replaces the old Python generator, which no longer matched the building roster. The result, at the expected level:
- Commons: every stage won, usually 1 to 2 stars; 30% over-levelled, 1 to 3 stars.
- Elites: about 2 to 3 stars.
- Legendaries: 2 to 3 stars.
- Under-levelled Commons often lose, so levelling still matters.
- The expected levels are the campaign tests' anchors (1 at stage 1, 10, 25, 50 at stage 5, 110 at stage 10, 200 at stage 15). If the owner wants a different pace, change the anchors and rerun the tuner.

## ADR-046: The owner's building roster, new art, switchable towers, traps and the painted map, 2026-11-09
The owner set the official building list, sent new art for every building and a painted village map, and gave count rules. Numbers not named below are Claude's test values.

**Roster and footprints**
- Town Hall 4×4, Hero Base 2×2, Wall 1×1, traps 1×1, every other building 3×3. The 50×50 map is unchanged.
- Internal ids stay; display names change: `dojo_hq` is the Town Hall, `research_hut` the Relic Hall (troop research moved there), `troop_camp` the Army Camp, `training_grounds` the Training Center, `defense_tower` the Arrow Tower, `spirit_turret` the Magic Tower.
- New: Cannon Tower, Guild Hall, Arena, Mana Vault, Warehouse, Bomb Trap, Hero Trap. Removed: Crystal Mine and the Watchtower (the owner dropped it).
- The Builder Hall and Summoning Gate stay as extras (building-driven navigation).
- Player-facing text says "Town Hall", never "Dojo".

**Counts (owner)**
- Warehouse: one. Gold Mine and Mana Mill: four, five from Town Hall 5, six from Town Hall 10.

**One tower, three kinds (owner)**
- The Arrow, Cannon and Magic Towers are one building. Only the Arrow Tower is in the build catalog. A placed tower switches kind any time from its Switch button: free, instant, keeps its level, not while upgrading.
- Data: `group: "tower"` on all three; `switchOnly` on the Cannon and Magic Towers; one shared count limit.
- Server: `sd_village_switch` RPC (`switchKind`), refusal `NotSwitchable`; placing a switch-only kind is refused.
- Each kind has one job, in both sims (tuned with the balance report, `BALANCE_REPORT.md`): the Arrow Tower does the most damage to heroes (fast single shots); the Cannon Tower crushes armoured packs (one heavy shot every 25 ticks, 2.2x attack, a 1.25-cell blast at full damage, shortest range); the Magic Tower clears swarms (1.2x attack every 15 ticks, a 1.5-cell splash at 40%, the Arrow Tower's range).

**Mana storage**
- A base cap of 10,000 Mana, raised by each Mana Vault and Warehouse. Collecting and server rewards (`payMana`) stop at the cap; the shop refuses a Mana swap that doesn't fit.

**Traps**
- Hidden, never targeted, not in the damage score. A Bomb Trap hurts every troop around it once; a Hero Trap hurts and stuns the first hero to step on it. Both sims match (events 17 TrapFired and 18 TrapHit).
- Battle playback hides traps until they fire, then shows a blast or a snap, damage numbers and the stun.

**Layout migration**
- `sd_player.layout_version` (migration 015). On load, `fitLayout` deletes buildings no longer in the list and moves overlapping ones to the nearest free spot, walls first.

**Art and the map**
- Every building on the list uses the owner's art (10 art levels over 15 game levels). Pictures are drawn at 80% of their square, with grass around them.
- The ground is the owner's painted map. The 50×50 grid runs corner to corner between its four corner torches, fitted with a bilinear map (`VillageMap`). The old built mountain, river and scattered props are not drawn while the painting is present.
- The camera never shows past the painting: panning springs back at its edges and zoom-out stops when it fills the screen.

## ADR-045: The Trial Tower, added on 2026-11-09 while the owner slept
A fifth battle mode, like the genre's hero trials. Numbers are test values in the `trials` block of `modes.json`.

**The floors**
- 30 floors. Each has a fixed guardian hero:
  - Commons on the low floors, then Elites, then Legendaries.
  - The Legendaries evolve on the highest floors.
- Guardians gain 6 levels per floor.
- Guardian strength runs from 56% on floor 1 to 230% on floor 30.
- Tests check that any new Common hero can clear floor 1, and that a level 1 Legendary cannot clear floor 30.

**Fights**
- Each fight is an Arena-style duel: your hero against the guardian, for the guardian's shrine.
- The server fights it.

**Rewards and limits**
- The first clear of a floor pays gems and Mana and opens the next floor.
- Replays are free, the same every time, and pay nothing.
- There is no daily limit and no energy.

## ADR-044: Hero gear, added on 2026-11-09 while the owner slept
The brief puts equipment in the Hero Altar, and Castle Clash heroes have gear. Claude chose the simplest version that fits one hero at a time. Numbers are test values in the `gear` block of `heroes.json`, read by both client and server.

**How gear works**
- Every hero has three slots: Weapon (+attack), Armor (+HP) and Charm (+defense), each from level 0 to 10, at +5% of its stat per level.
- Gear level N costs 1,500 × N gold and 150 × N² Mana.
- It needs a hero level of 20 × (N − 1) + 1, counted across evolutions.
- Gear belongs to the hero. A hero spent as an evolution duplicate takes its gear with it. There are no gear items to collect, so nothing new is random.

**Integrity**
- Gear is upgraded only on the server.
- The server applies it in every battle: raids, dungeons, guards, the Arena and Village Siege.
- The client shows the same numbers using shared maths, checked by matching tests in C# and Go.

**On screen:** the hero page has a Gear button.

## ADR-043: Treasure Chest and gem shop, added on 2026-11-09 while the owner slept
These fill the last placeholder buttons on the home screen. Numbers are test values in `modes.json`.

**Treasure Chest** (the chest shortcut)
- Free gold and Mana every 4 hours: 500 gold and 150 Mana per Dojo level.
- It opens once per wait, on the server's clock.

**Gem shop** (the gem-chest shortcut and the "+" buttons)
- Four fixed swaps of gems for gold or Mana:
  - 50 gems for 5,000 gold.
  - 200 gems for 22,000 gold.
  - 50 gems for 1,200 Mana.
  - 200 gems for 5,200 Mana.
- Nothing is random.
- A gold swap that would not fit in the vault is refused rather than wasted.
- There is no way to buy gems with money, and no real-money purchase anywhere.

## ADR-042: Player names and guilds, added on 2026-11-09 while the owner slept
Castle Clash has guilds, and the home screen already had a "friends" shortcut, so that shortcut now opens Guilds. Claude's choices:

**Names**
- Players can set a name in the Menu: 3 to 16 letters, digits, spaces, - or _.
- Until then they show as "Shinobi" plus four letters of their id.
- Names show on the HUD, in the Arena, in the rankings and in guilds.
- Names are not unique. Guild names are.

**Guilds**
- Founding a guild costs 300 gems (a test value).
- Guilds are open to join, up to 30 members, one guild per player.
- If the leader leaves, the longest-serving member leads; a guild with nobody left closes.
- The guild list ranks guilds by their members' total Arena points.

**Chat**
- One line per message, up to 200 characters, at most 12 a minute per player.
- Control characters are removed and text is never shown as markup.
- Each guild keeps its last 100 lines.
- The open guild screen checks for new lines every 5 seconds, unless the player is typing.
- **Not done, and needed before any public release:** a word filter, reporting, muting and moderation tools.

**Integrity**
- Every name, membership and chat line goes through the server, which checks it.
- Joins lock the guild, so 35 players joining at once still stop at 30.

**Taps during loading**
- A tap made while a screen is still loading now waits its turn instead of being dropped.
- A second tap during a change (a summon, a battle, a guild action) is still dropped, so a double tap never acts twice.

## ADR-041: Troop research at the Research Hut, added on 2026-11-09 while the owner slept
The owner said Mana pays for research (ADR-038), and the Research Hut was already in the catalog. Numbers are test values in the `research` block of `troops.json`, read by both client and server.

**How studies work**
- Each study raises one kind of troop's HP and attack by 8% per level, up to level 10. Defense does not change.
- Study level N needs a Research Hut of level N or higher.
- A study costs the troop's training Mana × 20 × N². For example, the first Village Militia study costs 200 Mana.
- Studies take from 5 minutes for level 1 to 24 hours for level 10.

**Limits and finishing**
- Only one study runs at a time.
- Like upgrades (ADR-040), a study finishes on its own on the server's clock, or at once for gems.

**In battle**
- Research applies to your troops when they land in raids and dungeons.
- It does not apply to Siege attackers, who are enemies.
- The client builds boosted troops the same way, so battle logs still match.

The new table is in `011_research.sql`. The shared rule scenarios cover research in both C# and Go.

## ADR-040: Upgrades take time and builders, added on 2026-11-08 while the owner slept
This replaces "Upgrades are instant" in ADR-025, because Castle Clash style games have build timers. All numbers are test values, set in `upgradeSeconds` in `buildings.json`, which the client and server both read.

**Timing**
- Upgrading to level 2 is instant, as before.
- Higher levels take time:
  - 1 minute for level 3.
  - Rising to 1 hour for level 9.
  - 24 hours for level 15.
- Walls are always instant.
- Placing a new building is still instant.

**Builders**
- Each running upgrade uses one builder.
- A village has 2 builders, plus 1 at Builder Hall level 6 and 1 more at level 11.
- When all builders are busy, a timed upgrade is refused with "All builders are busy".

**Finishing**
- The building reaches its new level on its own when the time is up, on the server's clock.
- The server finishes due upgrades under the player lock before every call, so the client's clock can't speed anything up.
- A running upgrade can be finished at once for gems: 1 gem per 2 minutes left, at least 1.

**While upgrading**
- A building keeps working at its old level.
- It can be moved, but not removed or upgraded again.

**On screen**
- A timer with a progress bar floats over each building being upgraded.
- The building's Upgrade medallion becomes a "finish for gems" medallion.
- The details panel shows how long an upgrade takes and how many builders are free.

New column is in `010_upgrade_timers.sql`. The shared rule scenarios cover timers, builders and finishing, and run in both C# and Go.

## ADR-039: Game modes, quests and the daily gift, added on 2026-11-08 while the owner slept
The owner asked for "game modes available" and smoothness, and said to decide open questions by asking "would Castle Clash have this?". Claude made these choices, and the owner can overrule any of them. All numbers are test values in `Resources/Config/modes.json`, which the client and server both read.
- **ATTACK now opens a Battle menu** with four modes: Raid (rivals), Dungeons, **Village Siege** and **Arena**. The home screen is unchanged.
- **Village Siege** (the genre's "monsters attack your base" mode): 10 levels of rogue ninja bands attack the player's own village as it stands. The server fights it in one call and the client plays the log back. The player's hero leaves its base and stands by the Dojo, because a guard at the base never met attackers who went straight for the Dojo. Stars are for defending: none if the attackers take a star, one if they don't, two under 30% damage, three under 10%. Only new stars pay gold and Mana, and the first hold of a level pays gems. Level N opens after level N-1 is held. Nothing in the village is lost. Tests check that any level 1 Common hero holds level 1 in a brand-new village, and that the last level beats one.
- **Arena**: a duel. The player's hero charges a shrine (drawn as a Dojo, 1,200 HP) that the opponent's hero guards. Toppling the shrine wins. Opponents are real players with a hero on guard and points close to the player's. When there are none, a "wandering" hero is used, whose level rises with the player's points. Wins earn 8 to 35 points, depending on the opponent's points, and losses cost 10. Being attacked never costs points. There is **no energy**: duels are unlimited, but only the first 8 wins each UTC day pay gold and Mana.
- **Rankings** (the trophy shortcut) show the Arena's top 30 and the player's rank.
- **Daily quests** (the quests shortcut): 7 quests that reset at midnight UTC and pay 20 to 50 gems each. Progress is counted on the server inside the actions themselves: collecting, training, upgrading, raid wins, Arena duels, Siege holds and summons.
- **Daily gift** (the gift shortcut): once per UTC day, 1,000 gold, 300 Mana and 50 gems. Gold is capped by the vault.
- **Mail** (the mail shortcut) lists raids on the village. As before, it never says who attacked.
- Friends and the two chests still say "coming in a later update".
- New tables are in `009_modes.sql`. Every Siege and Arena battle is logged in `sd_mode_fight` for limits and audits. Every call uses an idempotency key.

## ADR-038: Mana, the third currency, and the Mana Mill, decided by the owner on 2026-11-08
The owner named the currencies Gold, Gems and Mana, and chose that Mana pays for hero upgrades, troop training and research. **Every number below is an unapproved test value.**
- **Wallet:** `mana` is a column on `sd_player` (migration `008_mana.sql`, `CHECK (mana >= 0)`), next to gold and crystals, so the one player lock covers all three. Every change writes a `currency = 'mana'` ledger row, and the dev audit checks the balance against the ledger. The state RPC returns it as `village.mana`; the HUD's purple counter shows it (the picture is still `icon_chakra`).
- **Starting Mana:** new villages get `startingMana` (500) from `buildings.json`. Players from before the migration start at 0 and build a Mill.
- **Mana Mill** (`mana_mill`): a 3 x 3 Collector, 600 gold, limits 1/1/2/2/3 at Dojo 1 to 5, so a new player can build one at once. Level 1 makes 200 an hour and holds 800; level 15 makes 2,000 and holds 10,400. Upgrade costs are double the Gold Mine's. Mana has no village-wide storage cap: each Mill's own `storedCap` is the only limit, so no Mana storage building is needed yet. Collection uses the server clock only. Art: the owner's water-wheel painting as art levels 1 to 3, reused for 4 to 10 until upgraded paintings exist.
- **Troops cost Mana as well as gold:** militia 10, kunai thrower 20, shadow runner 40, shield bearer 50, iron ox rider 120 (`manaCost` in `troops.json`). Gold is checked first, then Mana, and nothing is taken unless both are there.
- **Hero training** (`sd_hero_train`, Hero Altar hero page): Mana buys exactly one level, costing `manaPerLevelBase + manaPerLevelStep x (level - 1)` from `heroes.json` (20 + 5 per level: 20 at level 1, 1,010 at level 199, about 102,000 from 1 to 200). XP already earned toward the next level is kept. Raid XP still levels heroes as before. Evolution is unchanged: no Mana cost was added to it, because rule 6 fixes what a transition needs.
- **Research does not exist yet.** The Research Hut still says its features arrive later. The only hook is the shared spend rule, `Village.TrySpendMana` in C# and `spendMana` in Go, which research should call when it is built.
- **Refusal:** `InsufficientMana`, the same name on both sides, shown as "Not enough Mana".
- Client and server rules are kept in step by new scenarios in `rules_vectors.json` (production and the cap, collection, training refusals, hero training).

## ADR-037: The heroes are 3D models, decided by the owner on 2026-11-08
Replaces the sprite-hero direction for the eight Legendaries. The owner asked for 3D heroes and paid for Meshy Pro.
- Each hero was made from the owner's painting: Meshy redrew it as a clean A-pose reference without effects (fire, lightning, auras, spirit animals), then made a 20,000-triangle model, auto-rigged it on a shared 24-bone humanoid skeleton and added library animations. Moonfang Oracle's staff came out loose, so it was cut off and reattached to her hand.
- `tools/art/pack_hero_3d.py` (Blender) packs each hero into one FBX with clips idle, walk, run, attack, special, hit and death. The attack is cut to one strike and nothing drifts. The colour texture is 1024 px.
- `Hero3DBuilder` (editor) makes an unlit material (the texture already carries the painted light), an Animator with the sprite heroes' states (Idle, Run, Attack) and `Resources/Heroes3D/<id>` at the sprite heroes' height. `hero_sprites.json` lists them with form -1, meaning every evolution form, so evolutions keep the same model until forms get their own.
- `HeroSpriteActor` gains a solid mode: a 3D hero turns to face where it goes instead of standing flat towards the camera. Village and battle code is unchanged.
- The Sage's 2D sprite prototype stays in the project but is switched off.
- Not done yet: special, hit and death clips are imported but not played; effects such as the Sage's flames; separate models per evolution form.

## ADR-036: 10 Common and 10 Elite heroes, rarity-scaled specials, summon odds 70/28/2, decided by the owner on 2026-11-08
The owner wants many more heroes that are easy to summon.
- **Roster:** the 20 Common and Elite heroes already in `heroes.json` are now live (`contentComplete`). Two of the 12 Commons were removed so there are exactly 10: Ash Monk (same role and element as Ember Recruit) and Gale Runner (a third Assassin). No player could own them, since incomplete heroes can't be summoned. The other 4 Legendaries stay off until finished.
- **Odds (no pity):** Common 70%, Elite 28%, Legendary 2%. Each rarity's share is split evenly between its live heroes: 700, 280 and 50 out of 10,000.
- **Specials by rarity:** Commons' specials still fire after 5 hits but aren't that good; Elites' are a little better; Legendaries' are full. A special's extra effect is scaled to 50% for Common, 75% for Elite and 100% for Legendary: the bonus part of a power hit (an Assassin's 300% becomes 200% or 250%), guard and stun time, and heals. The server sets it from rarity, and C# and Go share a test scenario for it. Solar Beast Sage's rally is Legendary only.
- **Art:** until the owner's ChatGPT paintings arrive (prompts in `docs/art/common_elite_hero_prompts.md`), these heroes show the old generated figure. Each painting is a neutral A-pose without effects, so it serves both the hero page and Meshy 3D.
- 50, 75 and 100 are unapproved test values.

## ADR-035: Hero signature specials, starting with Solar Beast Sage's Radiant Roar, decided by the owner on 2026-11-08
The owner wants Solar Beast Sage to play like a Castle Clash-style team buffer: he boosts his teammates' attack speed. Only the mechanic is borrowed; no names or art.
- A hero can now have its own special in place of its role's. `heroes.json` sets `"special": "rally"` on Solar Beast Sage. The server copies it into every battle from the archetype, so the client cannot claim it.
- **Radiant Roar (rally):** when his charge bar fills (5 landed hits), he roars at once instead of making the Fighter power hit. Every living ally within 6 cells, himself and troops included, attacks 30% faster for 6 seconds (60 ticks). Being hasted again resets the 6 seconds; it does not stack.
- **Roaring Sun (Evo II and up):** the haste lasts 9 seconds. This keeps the spec's rule that skills unlock or improve at each evolution. Battles now receive each hero's evolution for this.
- It works the same for guards: a guarding Sage roars for the other guards.
- Radiant Roar moved from Evo II to his base form, because his identity needs it from the start. His skills: Sun Palm (base), Radiant Roar (base, his special), Beast Stride (Evo I), Roaring Sun (Evo II), Celestial Guardian (Evo III). Beast Stride and Celestial Guardian are still descriptions only, as before.
- C# and Go run the same rule, checked by two new shared battle scenarios. 30%, 6 cells, 6 s and 9 s are unapproved test values.

## ADR-034: A 30 degree camera, closer default zoom, ground shadows, decided on 2026-11-07
The owner compared the village with screenshots of two well-known base builders: ours was too far away, at the
wrong angle for the painted art, and the buildings looked pasted on.
- The camera looks down at 30 degrees (the classic 2:1 angle the owner's art is painted for), not 35.264.
- Home opens at orthographic size 7 (was 10), battles at 11 (was 15). Zoom in now stops at 3.5 (was 5); zoom out
  still reaches the whole map.
- Every painted building and wall cell has a soft ground shadow; walls stand 35% taller than painted.
- New tower and wall art is requested from the owner with exact framing (`docs/art/tower_wall_prompts.md`).

## ADR-033: The owner's newest mockup and art packs, decided on 2026-11-07
The owner sent a newer home screen (`docs/ui/target_home_mockup_v2.webp`) and two art packs (UI buttons and icons;
every building at levels 1 to 10), and asked for both. This replaces ADR-032's layout.
- ATTACK moves to the bottom left, as the pack's painted crimson medallion.
- A column of round shortcuts runs down the left: gifts, rankings, quests, friends, mail. Settings moves to the top
  right beside an original clan banner (the owner's red tomoe banner, not a Naruto symbol).
- Currencies read gold, crystals, chakra (purple), each on the pack's bar with a "+". Power is no longer drawn.
- Two chest buttons on the right. There is no chest, gift, mail, ranking, friends or quest feature yet: each opens a
  plain "coming in a later update" window, with no timer and no badge count, so nothing pretends to work.
- A tapped building's actions are the pack's painted medallions (five states each) in a row at the bottom right.
- Buildings are the pack's painted pictures, standing upright on their footprint's front corner. The game keeps 15
  levels; the 10 pictures spread over them (level 1 shows picture 1, level 15 shows picture 10). Rules, footprints,
  the grid and the server are unchanged. Walls are the pack's V piece cut into one arm per grid axis plus a post.

## ADR-032: The owner's home screen mockup, decided on 2026-11-07
The owner sent a painted home screen and asked for it exactly. This replaces ADR-017's ATTACK-only bottom.
- Bottom left: a row of round shortcuts (mail, rankings, friends, quests, settings). Settings moved here from the
  player bar. Mail, rankings, friends and quests open a "coming in a later update" window until each is built.
- Top left: a round portrait (the guarding hero's painting, or the initial), name and level on an arrow-shaped
  plate, and a power number under it. Power is display only: building might plus the guarding hero's stats.
- Top right: three currencies, gold, Chakra (new) and crystals, each with a "+" button. Chakra has no source yet and
  reads 0; the "+" buttons open a "coming in a later update" shop window. No real-money purchases.
- Building actions are square framed tiles with labels, under an arrow-ended plaque; ATTACK sits in a spiked ring.
- The art is a stand-in drawn in code until the owner's ChatGPT sheets (prompts in Downloads) arrive.
- The mockup's banners use Naruto's Hidden Leaf symbol, and the owner asked for it exactly. It is not used: an
  original crest replaces it, because the symbol is protected.

## ADR-031: Only guards hop walls; attackers go around or break through, decided by the owner on 2026-11-07
- **Guard heroes** (a village's heroes defending it) hop over walls to reach attackers; hopping a wall cell takes twice as long as a step. Buildings still stop them.
- **Attackers** (heroes and troops alike, and monster waves when they exist) cannot hop. They take whichever is faster in real time: walking around, or breaking the wall in the way. Breaking costs the hits that wall's health needs at the attacker's damage and attack speed.
- Ranged attackers still shoot over a wall at a target in range, from where they stand.
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
