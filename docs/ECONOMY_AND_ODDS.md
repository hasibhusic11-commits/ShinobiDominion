# Economy and odds

**Every number here is a test value that you have not approved.**

## Summon pool, version 1 (`backend/nakama/config/summon_pool.json`)
| Hero | Rarity | Weight | Chance |
|---|---|---|---|
| River Scout | Common | 7,500 | 75.000% |
| Lantern Warden | Elite | 2,000 | 20.000% |
| Solar Beast Sage | Legendary | 125 | 1.250% |
| Thunder Empress | Legendary | 125 | 1.250% |
| Stone Titan | Legendary | 125 | 1.250% |
| Moonfang Oracle | Legendary | 125 | 1.250% |

Totals: Common 75%, Elite 20%, Legendary 5%. Only heroes with finished content can be in a pool, so the four incomplete Legendaries are refused until they are ready, and the server refuses to start otherwise.

To change rates, edit the file and raise `version`. Clients holding old rates are then refused and must fetch the new ones, so a player is never charged under rates different from those shown.

## Prices and starting values
- 100 crystals per roll. A ten-pull costs exactly 1,000 and has no discount or bonus.
- New players start with 3,000 crystals, no heroes and no catalysts. That is hardcoded as `startingCrystals` in `backend/nakama/main.go`.

## Rules that do not change
No pity, no guarantee, no duplicate protection, no selector, no scripted first summon. A ten-pull is ten independent rolls.

## Decisions still yours
Final rates, prices, crystal income, how catalysts are earned (there is no source yet), and how XP is earned (only a development button exists).
