# Reference study: early Castle Clash

Written notes from about 10 frames across 3 gameplay videos (2026-11-05). This is for studying presentation and game flow only. Its art, names, icons and assets must not be copied, and no reference images are kept in this repo. I saw still frames, not motion or sound.

Sources: [Brave Squads (2013) gameplay](https://www.youtube.com/watch?v=gcTUu8K4tas), [Android gameplay](https://www.youtube.com/watch?v=PsyggV2OZMc), [Let's Play Part 1](https://www.youtube.com/watch?v=Vyt2AjLb-nY).

## Why it looks like a real game
| Area | Early Castle Clash | Shinobi Dominion today |
|---|---|---|
| Framing | The owner confirms zoom behaves the same as ours, so camera is not a real difference. The frames were simply zoomed in. | Same zoom behaviour |
| Proportions | Fat, toy-like, exaggerated shapes | Realistic, thin architecture |
| Materials | Hand-painted: chunky planks, big tiles, painted-in light and wear | Flat colour with a generated pattern |
| Ground | Paved tile pad under each building, with painted cracks and themed maps | One plain green sheet |
| Density | Buildings packed together | Sparse by design of a 50 x 50 map |
| Life | Crowds of little troops walking around the base | Nothing walks |
| Art budget | Real artists | Generated in code |

## Game flow seen
- **Home:** resource bars with their maximums, buildings tapped directly, and timers over buildings being upgraded.
- **Battle screen:** End Battle at top centre, a countdown from 3 minutes, Available Loot at the right, percent destroyed at bottom right, and your resources at top left.
- **Deployment:** tap to drop units. Red zones around buildings refuse with "can't attack here".
- **Units:** heroes and troops are separate. Heroes do not lead troops, which correct what these frames first suggested. Per the owner, troops work like Clash of Clans: see ADR-021.
- **Destruction:** buildings collapse into rubble, and fallen units leave small gravestones, so progress is visible.
- **Modes:** themed battle maps such as lava caves, plus a painted world map for expeditions.
- **Upgrades:** a framed wooden panel where consuming heroes adds skill experience.
