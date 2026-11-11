# Altar and wall presentation — Pass 2 (2026-11-11)

## Screenshot-grounded defects
The owner's live Altar screenshot showed a full-width, opaque brown strip through the character cards, uneven portrait treatment between Legendaries and Elite/Common, and insufficient visual distinction between shelves and selectable characters. The first issue was a rendering/layout choice in `VillageController.Heroes.cs`, not a missing art file.

## Code fixed in this branch
- Hero Altar: replace the 40-unit opaque brown shelf with a 12-unit lacquer shelf and 3-unit brass cap. The level is displayed on an individual nine-sliced dark plate for each hero, preserving assigned/locked icons and real levels.
- Added a Unity regression check asserting the shelf stays thin and each card receives a rendered level-plate sprite.
- Walls: a missing art tier can no longer silently eliminate that piece when an earlier version of the **same type** exists. Resolve `wall_seg_x`, `wall_seg_z`, and `wall_post_v2` backward through visual tiers, preserving world-space scale via resolved tier metadata. Does not affect game level, HP, connections, placement or progression.
- Added wall-art fallback tests. All Unity tests still require an actual Unity editor runner.

## What is *not* fixed by code
- Elite/Common roster art still relies on procedural figures for heroes without original painted portraits; their real character-specific illustrations must be authored and imported into `Resources/HeroConcepts/{heroId}.png`.
- Evolution forms require their own original `{heroId}_evo_1.png`, `_evo_2.png`, `_evo_3.png`.
- Walls still require production-quality, originally painted `wall_seg_x_{tier:00}`, `wall_seg_z_{tier:00}`, and `wall_post_v2_{tier:00}` sprites for all ten visual tiers with matching ground footprints and camera angle. A fallback makes gaps less likely but is **not** complete art.
- The repository currently maps **15 gameplay upgrade levels** to **10 visual tiers** in `BuildingSprites.ArtLevel`. Changing this requires a separate gameplay/product decision; never silently alter economy or progression during an art pass.
- Key building sprites, UI icons, Battle mode illustrations, Hero Detail panels and other menus still need an asset-by-asset visual review.

## Approval gate
This is an unverified source pass, not 10/10 certification. Before approval, capture live Unity Altar and wall screenshots at 1280×720, 2340×1080 and 1600×1200, including a 90-degree wall turn, T junction, disconnected post, mixed-level adjacency, hidden/wounded segments, and tiers 1/5/10. Measure touch hitboxes and inspect missing art logs. Judge live screenshots against the owner image, not generated concept mockups.

## Original visual direction
Feudal ninja shrine identity, gold/brass metalwork, dark lacquer, controlled crimson accents, painting-first character/architecture art, high legibility. Castle Clash is a reference for tactile mobile strategy presentation only; do not duplicate its protected assets.
