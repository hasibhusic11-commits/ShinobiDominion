# M3 — Heroes Altar & Hero Detail v3 (feature branch)

**Status:** implementation committed, **NOT YET UNITY-VERIFIED**. Do not merge until the owner reviews real screenshots.

## Scope

This is the first code pass toward the painted, tactile, collectible-hero presentation of classic fantasy mobile strategy games, using original Shinobi Dominion artwork.

- **Shared UI:** `SdKit.Tabs` uses the same nine-sliced lacquer/gold button system as the v3 Battle Menu; `SdKit.EvolutionStars` draws only earned blue stars.
- **Heroes Altar:** uses `ShowThemedWindow`; rarity tabs occupy a single bar above the original scrolling shelf display. Hero level, assignment, filters, selection and all gameplay routes are preserved.
- **Hero Detail:** uses `ShowThemedWindow`, retains Hero / Attributes / Gear / Mastery tabs, places browse arrows inside the left portrait bay and uses gold tactile primary buttons.
- **Portraits:** optional progression-specific transparent art at `Resources/HeroConcepts/{heroId}_evo_1.png`, `_evo_2.png`, `_evo_3.png`. Base art stays `{heroId}.png`. The existing illustrated base art is displayed when an evolved form lacks a distinct illustration. **Never misrepresent this as completed evolution artwork.**
- **Evolution UI:** no Roman-numeral extra badge in the Altar. Blue stars represent actual earned evolutions; no empty stars on Base.
- **Game rules:** unchanged. Server-side inventory, hero progression, duplicate sacrifice, currency, rarity and summon probabilities untouched.

## Not supplied by this branch

- New final hero PNGs, mesh/rigged in-world hero models, skill icon set, new building illustrations, or a full game-wide v3 conversion.
- New UI screenshots or verified Unity/Android/iOS test runs. This branch was edited through a GitHub integration without a Unity Editor.

## Required Unity review before merge

1. Open the project using the documented Unity version; confirm no compilation or console errors.
2. Run `HeroFlowTests`, `UiOverhaulTests`, then the full PlayMode and EditMode test suites. Follow existing local-server setup instructions; record commands and actual results.
3. At 1280×720, 1600×900, 2340×1080, and 1600×1200, capture:
   - Altar showing all / legendary filter / empty state / 20+ heroes
   - Hero detail showing all four tabs, Hero Detail navigation, long names, and enabled / disabled Evolve
   - Base and evolved heroes with 0, 1, 2, and 3 blue stars
4. Verify all touch targets, scrolling, bottom-cost controls and tab strip fit within the brass frame without overlap, especially the compressed view at 4:3.
5. Verify live data and actions: filter, assign, train, skill, mastery, evolve, return to Altar, and close to village.
6. Compare captures against `docs/ui/overhaul/references/` and the original v2 before-screenshots. Review with the owner; do not declare 10/10 without approval.

## Known work still needed

- The art is not upgraded in this branch. Quality is limited by existing 2D painting sources.
- Hero evolution-specific PNG files must be authored and added separately to obtain truly distinct four-stage visuals.
- `World/HeroArt.cs` still contains procedural mesh characters and requires an artist-authored animated rig pipeline to deliver production-quality in-world heroes.
- Real phone/tablet safe-area, touch and frame-time measurements were not performed.
- More UI screens still use v2. Migrate them only after v3 hero screens are visually accepted.
