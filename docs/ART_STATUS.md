# Art status and provenance

**All world art is original and generated in code. No third-party models, textures or shaders are in the project.** It is a real improvement on primitives, but it is temporary art, not professional final art.

## How it is built
| Part | File |
|---|---|
| Mesh tools: curved roofs, pillars, foliage | `Scripts/Game/World/WorldRendering.cs` |
| Buildings, Hero Bases, hero figures | `Scripts/Game/World/BuildingArt.cs` |
| Ground, forest, mountains, water | `Scripts/Game/World/NatureArt.cs` |
| Walls | `WallView` in `Scripts/Game/World/VillageView.cs` |
| Effects and the quality setting | `Scripts/Game/World/WorldEffects.cs` |
| Procedural surface textures | `Scripts/Editor/ArtBaker.cs` |
| Toon, ground and water shaders | `Assets/_Game/Shaders/` |

Art never touches game rules. `BuildingMeshes.For` is the single point to swap in real models, by building id.

## Needs a professional artist
| Asset | Today | Why replace it |
|---|---|---|
| **Heroes** | Tiny block-built figures | Highest priority. Your evolution rule needs four striking forms that keep one identity, and code can't deliver that. Portraits don't exist yet either. |
| **Dojo HQ and key buildings** | Generated, fairly detailed | Hand modelling adds character and asymmetry. |
| **Textures** | Generated grayscale patterns | Fine at this distance, simple up close. |
| **Icons and UI** | Generated shapes | Currency icons and the avatar are plain. |
| **Trees and rocks** | Generated | Acceptable. Low priority. |

Your request asked for prefabs. The project generates models in code rather than using prefabs, since there is no authored art to store in them yet. Real art should arrive as prefabs through `BuildingMeshes`.

## Requirements not fully met
- **Movement not seen:** wind, water flow and lantern flicker are built, but I only reviewed still images. Please check them in Play Mode.
- **Hero idle:** a gentle sway from the shader, not real character animation.
- **Building upgrades:** only walls change appearance with level.
- **Ambient occlusion:** a cheap darkening near the ground, with no screen-space effect, by design for phones.
- **Phone performance: not measured.** See below.

## Measured cost (High quality)
- 172,000 vertices in view at most, in 18 mesh renderers on 3 materials (down from 806,000 after review).
- Low quality halves plants, stops movement and particles, and turns off shadows. Plant density changes after a restart.
- The world is built when the game starts, and that load time has not been measured on a phone. Baking it ahead of time is the likely fix if it is slow.
- 4K shadow maps are set at High. These may be too heavy for mid-range phones, and Phase 5 should test them.

## Third-party register
None used. See `VISUAL_BASELINE.md` for what was evaluated. Anything imported later must be added here and to `THIRD_PARTY_NOTICES.md`.
