# Art and rendering status — current repository

Last reconciled: 2026-11-11. This is a prototype with **mixed authored and
procedural art**, not a uniformly finished 3D game.

## Implemented asset paths

- Painted map: `Resources/Map/village_map.png` plus levelled building artwork
  in `Resources/Buildings/` (current shipped config: **15** levels).
- UI assets: `Resources/UI/` includes owner-supplied artwork, painted controls,
  currency icons, nine-sliced interface sprites and third-party licensed fonts.
- Concept paintings: 18 hero base-form PNGs in `Resources/HeroConcepts/`.
- In-world heroes: 18 rigged models under `Art/Heroes3D/` and prefabs under
  `Resources/Heroes3D/`—all eight named Legendaries and ten Common heroes.
  They share animations and are **not all owner-approved or perfectly faithful
  to the concept paintings**.
- Ten Elites do **not** have equivalent finished 3D prefab coverage. Their
  battle renderer already uses procedural mesh fallbacks. The audit branch
  extends that development fallback to Hero Bases so they no longer disappear.
- `Resources/Config/hero_sprites.json` uses `form: -1` for these 3D prefabs:
  the same model is displayed at every evolution unless a form-specific asset
  is subsequently registered.
- An experimental layered Solar Beast Sage sprite prototype exists but is
  disabled by default. It is not a universal solution for hero art.

## Remaining art work

1. Finish the eight Legendary base forms to a consistent original stylized
   mobile-strategy quality bar, then author **24 distinct evolution forms**
   (8 heroes × 3 upgrades); use only original, single-character artwork.
2. Finish models and animations for ten Elite heroes and polish Common heroes.
3. Replace procedural troop meshes with authored rigged or sprite-animated units.
4. Complete the v3 UI art migration beyond Battle Menu and the separate
   Hero UI draft PR. Arena, Siege, Guilds, ranking, gift, quest and summoning
   screens need cohesive, professionally authored asset treatment.
5. Add purposeful building/environment motion, ground richness, shadow and
   combat-impact refinements that stay performant on phones.
6. Verify alpha transparency, silhouette readability at battle scale, sprite
   resolution and licensing/provenance for all external/generated assets.

## Release and runtime verification

The GitHub connector can read Unity C# and metadata but cannot execute the
Unity Editor or inspect Git LFS artwork as full rendered images. Existing
repository screenshots and test reports are historical, not substitutes for
new captures on the audit branch. The game requires measured Android/iOS
frame time, memory, build size, crash behavior, safe area, text/touch targets,
and real-device first-run verification.

The production content gate `python3 tools/ci/validate_catalog.py --release`
fails while launch Legendary content or form art remains missing.
