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

## Building levels (test values)
Every building has 15 levels. Edit `tools/balance/generate_building_levels.ps1` and rerun it to change them. Full tables are in `buildings.json`.

| Stat | Level 1 | Level 15 | Growth |
|---|---|---|---|
| HP (most buildings) | base | 9x | Faster and faster |
| HP (walls) | 300 | 120,000 | Fastest of all |
| Upgrade cost | base | 450x base, for 14 to 15 | Faster and faster |
| Gold mine, per hour | 600 | 6,000 | Steady |
| Gold mine, holds | 2,400 | 31,200 | Faster and faster |
| Vault capacity | 15,000 | 225,000 | Faster and faster |
| Tower attack | 60 | 340 | +20 per level |
| Tower range | 13 | 18 | Steps |
| Hero base bonus | 2% | 30% | +2% per level |

Dojo level needed: 3 for level 5, 6 for level 8, 9 for level 11, 12 for level 14.

## Dungeon rewards (test values, ADR-029)
Made by `tools/balance/generate_campaign.py`. Only the server pays these.
- Gold: each stage pays per new star, as raids always have.
- Hero XP: 30 + 25 for each stage number, per hero landed, on each win. At most 30 paying wins in 24 hours.
- Crystals: paid once, on a stage's first clear.
- Catalysts: one each, once only: stage 5 Awakening Scroll, stage 10 Ascension Stone, stage 15 Sovereign Scroll.
