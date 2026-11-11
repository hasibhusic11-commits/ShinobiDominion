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

## Hero art correction — same real character, consistent portrait presentation
The shipped `hero_sprites.json` registry declares 18 enabled Heroes3D prefabs, including Common heroes. Yet roster screens without concept art previously rendered the old procedural `HeroPortraits` proxy, which explains the mismatched blocky characters in the owner's screenshot.

- Added `HeroPortraitBaker.cs` with Unity Editor menu **Tools > Shinobi Dominion > Art > Bake Missing Hero Portraits**. It renders each available **existing real hero model** to a 512×640 transparent PNG under `Assets/_Game/Resources/HeroPortraits/{heroId}.png`. Source concept paintings are never overwritten.
- `HeroConcepts` now loads those baked hero-specific images when no `HeroConcepts/{heroId}` authored painting exists; Hero Altar and Hero Detail then share the same real character artwork rather than a procedural impostor. Dedicated evolution paintings still take precedence.
- If the 3D prefab is missing, the model has no renderer, or the hero is absent from the current 3D registry, the fallback remains; the baker reports the case rather than fabricating a hero identity.
- **These PNGs are not yet created or committed**. Open the actual Unity project, execute the menu action, visually inspect each baked Common/Elite portrait, and commit the generated PNGs (and Unity .meta files) before claiming the screenshot defect fixed in a build. Reopen the Altar after baking.
- Run **Tools > Shinobi Dominion > Art > Rebake 3D Hero Portraits** if the underlying authored model changes. This overwrites only earlier baked portraits, never original character concept paintings.

## Structural wall corrections
The old wall renderer only drew `wall_post_v2_*` when `mask == 0` (an isolated tile). Connected endcaps, bends, and intersections could therefore lose their corner posts. A post is now drawn for **every** wall cell; the north/east spans still use the same connectivity rules. Missing tier images fall back to the nearest older tier, then to a newer tier if **no** earlier matching piece exists. This retains the requested wall type/direction and scales by the actual art tier. Tests cover forward/backward fallback. Full authentic ten-tier wall sprite packs still require authoring.

**Verification remaining:** Unity Editor compilation; actual portrait bake output and transparency check; portrait framing and legibility at mobile/tablet sizes; post/segment seam rendering at corners, T-junctions and straight runs; device PlayMode tests. Do not mark 10/10 or production ready without these screenshots and tests.

## Portrait import validation (follow-up)
- Baking now imports PNGs as **readable, uncompressed RGBA textures** so `HeroConcepts.Card` can crop its runtime sprites using `Sprite.Create`. A non-readable import can fail despite the PNG existing.
- A capture must contain a believable fraction of visible alpha. Mostly transparent (under 1%) or nearly all opaque (over 96%) results are rejected and logged rather than added as blank/incorrect portraits. If the URP camera forces opaque alpha, fix the capture configuration first.
- The bake method can also be called with Unity's `-executeMethod ShinobiDominion.Editor.HeroPortraitBaker.BakeMissing` in Editor batch mode, with an absolute `-projectPath` and log file. This is a build-machine task, **not performed by this GitHub integration**.
- Review that the character faces the camera: the baker uses its original prefab transform and frames the renderer bounds; some models may need dedicated art-directed camera poses. Those models require human visual inspection, not silent acceptance.

## Altar hero identity pass
- Every roster bust now displays the **live hero archetype name** in an individual slim lacquer plate on the portrait's lower edge, separate from the level plate. This makes duplicate and similarly colored heroes distinguishable without opening each detail page.
- Names use the existing body font with text-best-fit rather than baked label pixels; the actual roster remains the source of truth.
- Added a PlayMode test asserting that the card's name matches the corresponding hero archetype.
- The portrait baker now imports readable/uncompressed PNGs (required by the existing runtime `Sprite.Create` crop path) and rejects transparent/opaque failed captures instead of silently adding broken art.

## Automatic Unity execution (follow-up)
- `Assets/_Game/Scripts/Editor/HeroPortraitAutoBake.cs` schedules one non-destructive missing-portrait bake **per normal Unity Editor session**, after compilation/imports settle. It skips batch builds and PlayMode, preserves concept paintings and existing baked portraits, and uses SessionState to prevent repeated recursive imports.
- The baker now exposes `ShinobiDominion.Editor.HeroPortraitBaker.BakeForCI`. If an eligible hero model cannot be rendered, batch execution returns a nonzero result instead of silently succeeding.
- `tools/art/bake_hero_portraits.ps1` invokes the exact Unity 6000.3.28f1 editor expected by this project on Windows, saves `artifacts/hero-portrait-bake.log`, and fails with useful logs on an unsuccessful exit. The editor must be installed locally; there is no Unity binary in the assistant environment.
- Portrait framing is now head-to-hips to improve character readability in the Altar, rather than rendering an undersized full-body figure. Actual poses may still need adjustment after reviewing captured PNGs.
- Hero-name and level plaques are separated. Regression coverage checks that their vertical extents do not overlap.
- **Verification remains open:** actual PNGs, generated Unity `.meta` files and editor rendering have not been produced in this chat; the generated-art files only exist after Unity executes the baker. A successful code commit does not equal 10/10 asset approval.
