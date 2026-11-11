# Economy and odds

**Every number here is a test value that you have not approved.**

**Source of truth:** `backend/nakama/config/summon_pool.json`, `unity/Assets/_Game/Resources/Config/heroes.json`, and `buildings.json`. This document was outdated. The section below reflects main at `9eedfd0`; check those config files for any later changes.

## Summon pool, version 1 (`backend/nakama/config/summon_pool.json`)

| Rarity | Included heroes | Weight per hero | Combined chance |
|---|---:|---:|---:|
| Common | 10 | 700 | 70% |
| Elite | 10 | 280 | 28% |
| Legendary | 4 | 50 | 2% |

All rates are **proposed test rates**, not final owner-approved numbers. Individual Common heroes have 7% each; individual Elites 2.8% each; individual featured Legendaries 0.5% each. The four additional Legendary archetypes in `heroes.json` are currently `contentComplete=false` and excluded from the pool, not accidentally forgotten.

The server displays the active summon table and rejects a stale pool version. Ten-pulls are ten independent rolls without pity, discounts, duplicate protection or guaranteed rarity.

## Prices and starting values
- 100 gems (called `crystals` in server identifiers) per roll. A ten-pull costs exactly 1,000 and has no discount or bonus.
- New players start with 3,000 crystals, no heroes and no catalysts. That is hardcoded as `startingCrystals` in `backend/nakama/main.go`.

## Rules that do not change
No pity, no guarantee, no duplicate protection, no selector, no scripted first summon. A ten-pull is ten independent rolls.

## Decisions still yours
Final rates, prices, crystal income, how catalysts are earned (there is no source yet), and how XP is earned (only a development button exists).

## Building levels (test values)

Current `buildings.json` still defines **15** levels per building. This may conflict with the older design preference for 10 visual upgrade stages. Do not silently remove five gameplay levels: agree on the design and migrate the data, art assets, economy and tests together.

See the generated `docs/BALANCE_REPORT.md` for current (provisional) costs, timers and income. Earlier fixed tables in this document became stale.

## Dungeon rewards (test values, ADR-029)
Made by `tools/balance/generate_campaign.js` (ADR-047). Only the server pays these.
- Gold: each stage pays per new star, as raids always have.
- Hero XP: 30 + 25 for each stage number, per hero landed, on each win. At most 30 paying wins in 24 hours.
- Crystals: paid once, on a stage's first clear.
- Catalysts: one each, once only: stage 5 Awakening Scroll, stage 10 Ascension Stone, stage 15 Sovereign Scroll.
