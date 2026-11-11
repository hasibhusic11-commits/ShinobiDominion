# Economy and summon odds — current shipped prototype data

**Last reconciled: 2026-11-11. All rates, costs and caps are provisional and NOT APPROVED for release.**
The canonical numbers live in `backend/nakama/config/summon_pool.json`, `Resources/Config/heroes.json`,
and `Resources/Config/buildings.json`. This document summarizes them, never overrides them.

## Summoning

Pool `version: 1` — **Standard Gate**.
`TEST RATES - NOT APPROVED for release. Owner's working rates 2026-11-08: Common 70%, Elite 28%, Legendary 2%, split evenly within each rarity.`

| Rarity | Summonable characters | Total weight | Probability |
|---|---:|---:|---:|
| Common | 10 | 7,000 | 70.000% |
| Elite | 10 | 2,800 | 28.000% |
| Legendary | 4 | 200 | 2.000% |
| **Total** | **24** | **10,000** | **100.000%** |

- 4 of the 8 designed Legendaries are currently included (Solar Beast Sage, Thunder Empress, Stone Titan, Moonfang Oracle).
- Other four Legendary IDs are in `heroes.json` but marked `contentComplete: false`. **Do not add them until their content is approved and their published odds are updated.**
- Gems are called `crystals` in the internal server and serialization code; the player-facing resource is **Gems**. The other resources are Gold and Mana. No wood or energy.
- One summon costs **100 gems**. Ten independent summons cost **1000 gems** with no hidden discount.
- A newly created player receives **3,000 gems** from `startingCrystals` in `backend/nakama/main.go`.
- **No pity, duplicate protection, selector, guaranteed rare result or scripted first pull**. The server uses cryptographic randomness and returns actual advertised odds.
- Changing the table requires a `version` bump. An old-version request is rejected rather than charged at unadvertised rates.

## Hero evolution

Base plus **Evo I, Evo II and Evo III**. Each transition requires max level (200),
one **duplicate of the exact hero**, and one catalyst. Current test catalyst IDs:
`awakening_scroll`, `ascension_stone`, `sovereign_scroll`.
The server decides sacrifices and rewards. No pets or animal companions.

## Building progression

Current shipped content uses **15 levels per building** (21 building types). This reflects **ADR-025**; an earlier design discussion considered ten. **Do not silently change all upgrade tables** without a deliberate new gameplay decision, a balance rerun and synchronized client/server tests.

Gold/Mana mine production, capacities, tower damage, builder timers and upgrade costs live in `buildings.json`.
Do not quote old numeric tables as current after a balance generator change.

## Dungeon rewards

Fifteen campaign stages. Server-controlled rewards include Gold/Mana on eligible
stars, Hero XP from eligible wins, first-clear gems, and the three one-time
catalysts (stages 5, 10 and 15). Check `backend/nakama/battle_rpc.go`
and the shared rules tests before changing reward caps.

## Next owner decisions

Final summon odds, game launch roster completeness, approved reward pacing,
monetization (currently **no real-money purchases**), building level ceiling,
and a launch-ready account-recovery/authentication system.
