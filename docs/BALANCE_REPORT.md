# Balance report

Written by `BalanceReportTests` from the shipped data on 2026-11-09. Every battle is the real battle rules, the same ones the server runs. All numbers in the data are still test values.

## 1. Campaign

Each of the 24 summonable heroes leads the army a player would have by that stage (militia, 10 more per stage, up to 160) at 70%, 100% and 130% of the level expected there. Cells show the average stars, then the share of heroes that win at least one star.

| Stage | Expected level | Common 70% | Common expected | Common 130% | Elite 70% | Elite expected | Elite 130% | Legendary 70% | Legendary expected | Legendary 130% | Flag |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1. Ridge Outpost | 1 | 1.4 (100%) | 1.4 (100%) | 1.4 (100%) | 2.0 (100%) | 2.0 (100%) | 2.0 (100%) | 2.3 (100%) | 2.3 (100%) | 2.3 (100%) |  |
| 2. Cinder Garrison | 10 | 2.0 (100%) | 2.0 (100%) | 2.0 (100%) | 2.5 (100%) | 2.9 (100%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) |  |
| 3. Charcoal Pass | 25 | 1.0 (80%) | 1.2 (80%) | 2.0 (100%) | 2.9 (100%) | 2.9 (100%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) |  |
| 4. Kiln Yard | 37 | 1.2 (80%) | 1.6 (80%) | 2.0 (100%) | 2.1 (100%) | 2.4 (100%) | 2.8 (100%) | 2.8 (100%) | 3.0 (100%) | 3.0 (100%) |  |
| 5. Ember Keep | 50 | 1.6 (80%) | 2.2 (100%) | 2.5 (100%) | 2.9 (100%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) |  |
| 6. Reed Marsh Camp | 62 | 0.4 (30%) | 1.3 (100%) | 1.8 (100%) | 2.2 (100%) | 2.9 (100%) | 3.0 (100%) | 2.8 (100%) | 3.0 (100%) | 3.0 (100%) |  |
| 7. Lantern Ford | 74 | 1.0 (50%) | 2.0 (90%) | 2.6 (90%) | 2.3 (80%) | 2.4 (80%) | 2.4 (80%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) |  |
| 8. Fogbound Shrine | 86 | 1.3 (80%) | 2.6 (100%) | 2.9 (100%) | 2.8 (100%) | 2.8 (100%) | 2.8 (100%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) |  |
| 9. Willow Stockade | 98 | 0.2 (10%) | 0.9 (80%) | 1.7 (100%) | 2.1 (100%) | 2.9 (100%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) | 3.0 (100%) |  |
| 10. Hollow Citadel | 110 | 0.3 (20%) | 1.1 (100%) | 1.9 (100%) | 1.6 (100%) | 1.9 (100%) | 2.2 (100%) | 1.5 (100%) | 2.0 (100%) | 2.0 (100%) |  |
| 11. Slate Stair | 128 | 0.0 (0%) | 1.0 (80%) | 2.0 (100%) | 2.1 (100%) | 2.9 (100%) | 2.8 (100%) | 2.5 (100%) | 3.0 (100%) | 3.0 (100%) |  |
| 12. Cold Forge | 146 | 0.4 (40%) | 1.0 (100%) | 2.2 (100%) | 2.1 (100%) | 2.8 (100%) | 2.8 (100%) | 2.8 (100%) | 2.8 (100%) | 2.8 (100%) |  |
| 13. Whitecrag Bastion | 164 | 0.0 (0%) | 0.8 (80%) | 1.2 (100%) | 1.3 (90%) | 2.3 (100%) | 2.6 (100%) | 2.0 (100%) | 2.8 (100%) | 2.8 (100%) |  |
| 14. Sky Gate | 182 | 0.0 (0%) | 0.7 (70%) | 1.0 (100%) | 1.3 (100%) | 1.6 (100%) | 1.6 (100%) | 1.3 (100%) | 1.8 (100%) | 1.8 (100%) |  |
| 15. Summit Sanctum | 200 | 0.2 (20%) | 1.2 (100%) | 1.2 (100%) | 1.8 (100%) | 2.4 (100%) | 2.4 (100%) | 2.3 (100%) | 2.3 (100%) | 2.3 (100%) |  |

No stage is flagged as too hard or too easy.

## 2. Arrow, Cannon and Magic Tower

One tower at a given level, built so it cannot fall, against three attackers on their own: a swarm of 40 militia (110 HP), a pack of 20 armoured troops (700 HP, 30 defense), and one Elite hero at level 100 with no army. Seconds are how long the tower takes to kill the whole group ("-" if not within the 3-minute battle); hero damage is over the first 60 seconds.

| Tower | Level | Attack | Range | Swarm: seconds | Armoured pack: seconds | Damage to the hero |
|---|---|---|---|---|---|---|
| Arrow Tower | 1 | 60 | 13 | 89.5 | - | 483 |
| Cannon Tower | 1 | 132 | 10 | 26.0 | 49.0 | 406 |
| Magic Tower | 1 | 72 | 13 | 37.5 | 79.5 | 372 |
| Arrow Tower | 8 | 200 | 16 | 40.9 | 109.5 | 1,995 |
| Cannon Tower | 8 | 440 | 13 | 23.0 | 38.5 | 1,488 |
| Magic Tower | 8 | 240 | 16 | 24.4 | 45.0 | 1,612 |
| Arrow Tower | 15 | 340 | 18 | 39.1 | 62.3 | 3,276 |
| Cannon Tower | 15 | 748 | 15 | 21.4 | 34.0 | 2,851 |
| Magic Tower | 15 | 408 | 18 | 16.6 | 37.8 | 3,023 |

The hero is Lantern Warden.

## 3. Economy in hours of mine income

Gold income at each Town Hall level: as many Gold Mines as allowed, each at the highest level that Town Hall allows. Upgrade costs are then shown as hours of that income. Collecting is assumed to be on time; raids and rewards come on top.

| Town Hall | Mines | Mine level | Gold per hour | Town Hall's next upgrade | Hours of income | Everything newly unlocked, in hours |
|---|---|---|---|---|---|---|
| 1 | 4 | 4 | 5,280 | 2,000 | 0.4 | 13.7 |
| 2 | 4 | 4 | 5,280 | 4,000 | 0.8 | 0.0 |
| 3 | 4 | 7 | 9,120 | 8,000 | 0.9 | 74.7 |
| 4 | 4 | 7 | 9,120 | 14,000 | 1.5 | 0.0 |
| 5 | 5 | 7 | 11,400 | 24,000 | 2.1 | 0.0 |
| 6 | 5 | 10 | 17,100 | 40,000 | 2.3 | 212.3 |
| 7 | 5 | 10 | 17,100 | 64,000 | 3.7 | 0.0 |
| 8 | 5 | 10 | 17,100 | 100,000 | 5.8 | 0.0 |
| 9 | 5 | 13 | 24,600 | 150,000 | 6.1 | 469.9 |
| 10 | 6 | 13 | 29,520 | 220,000 | 7.5 | 0.0 |
| 11 | 6 | 13 | 29,520 | 320,000 | 10.8 | 0.0 |
| 12 | 6 | 15 | 36,000 | 460,000 | 12.8 | 513.8 |
| 13 | 6 | 15 | 36,000 | 640,000 | 17.8 | 0.0 |
| 14 | 6 | 15 | 36,000 | 900,000 | 25.0 | 0.0 |
| 15 | 6 | 15 | 36,000 | max | 0.0 | 0.0 |

Upgrade waiting times by level (builders work on these while income comes in):

level 2: instant, level 3: 1 min, level 4: 3 min, level 5: 10 min, level 6: 20 min, level 7: 40 min, level 8: 1 h, level 9: 2 h, level 10: 4 h, level 11: 6 h, level 12: 8 h, level 13: 12 h, level 14: 16 h, level 15: 24 h.

