# Visual baseline and asset evaluation

Audited 2026-11-05 at git tag `pre-visual-overhaul`, Unity 6000.3.28f1, URP 17.3. Baseline image: `docs/screenshots/baseline_home.png`.

## Why the village looked basic
| Area | Finding |
|---|---|
| Geometry | Every building is a few stacked boxes and tapered blocks. Roofs are straight-sided with no curve, eaves or tiles. |
| Materials | Flat colour only, using URP Lit with no surface detail at all. |
| Ground | One flat colour with faint rectangular patches, which read as paving rather than earth or grass. |
| Lighting | Dark, low-contrast and muddy. No warm and cool separation, so forms blur together. |
| Environment | Almost nothing is visible at the default zoom. Trees are two stacked cones, with no hills or moving water. |
| Scale | Buildings are small against a huge empty plane, so the scene feels sparse. |
| Life | Nothing moves. |
| Effects | No glow, smoke or energy, so nothing draws the eye to the Dojo or the Gate. |

## System map (unchanged by this overhaul)
| System | Where |
|---|---|
| 50x50 grid, placement, walls, saves | `Scripts/Core`, mirrored in `backend/nakama/village.go` |
| Selection, panels, flow | `Scripts/Game/VillageController*.cs` |
| Camera and input | `Scripts/Game/Input/IsoCameraAndInput.cs` |
| Art | `Scripts/Game/World/`. Art is fully separate from game rules, so this overhaul replaces only that layer. |

## Evaluated tools and assets
| Source | License | Decision | Why |
|---|---|---|---|
| Unity-MCP | Apache-2.0 | **Not adopted** | Its README says the project path cannot contain spaces, and this one sits under `Game Design`. Setup also needs a sign-in to an external site, which is your account decision. Unity's batch mode gives me compile, tests and rendered screenshots instead. |
| com.unity.toonshader | Unity Companion | **Not adopted** | Still a preview package, and heavy for mobile. A small custom toon shader fits one shared material better. |
| KayKit City Builder Bits | CC0 | **Not adopted** | Shipped as glTF in a Godot addon layout, which Unity needs an extra package to import. Its style is a modern town, not ninja architecture. |
| KayKit Adventurers | CC0 | **Deferred** | Same import issue. Rigged characters could stand in as hero bodies later, but they are Western fantasy adventurers, not shinobi. |
| VKev URP Shaders | MIT | **Reference only** | Built on a beta Unity version, with outline effects that use extra render passes. Nothing copied. |
| Kenney Nature Kit | CC0 | **Could not obtain** | The download link is not exposed on the page to automation, and a guessed link returned a web page, not a zip. Nature assets were built in-house instead. If you download the zip, I can evaluate it. |
| Kenney Castle Kit | CC0 | **Not adopted** | Western medieval castles do not suit the ninja identity. |
| Blender | n/a | **Not used** | Not installed, and scripted modelling adds a tool without changing the result much. Meshes are generated in code. |

No third-party art was added in this overhaul, so `THIRD_PARTY_NOTICES.md` has no new asset entries.

## What this is, honestly
All art is **procedural**: generated from code using real geometry, procedural textures and a custom shader. It is original and consistent, but a skilled 3D artist would do better. See `ART_STATUS.md`.
