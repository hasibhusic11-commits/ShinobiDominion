# Third-party notices

**No third-party source code has been copied into this repository.**

Consumed as packages, not vendored:

| Package | Source | License |
|---|---|---|
| Universal Render Pipeline, Input System, uGUI, Test Framework | Unity registry | Unity Companion License |
| Newtonsoft.Json (Unity package and NuGet) | Unity registry, NuGet | MIT |
| Nakama Unity SDK v3.22.1 | GitHub, pinned by tag | Apache-2.0 |
| Nakama 3.42.0 server and nakama-common 1.49.0 (Docker image and Go module) | Docker Hub, Go modules | Apache-2.0 |
| Postgres 16.11 (Docker image, local development) | Docker Hub | PostgreSQL License |
| LitMotion v2.0.2, with Burst, Collections and Mathematics from Unity | GitHub, pinned by tag | MIT |
| UIEffect 5.11.8 | GitHub, pinned by tag | MIT |
| SoftMaskForUGUI 3.6.6 | GitHub, pinned by tag | MIT |
| Kenney Fantasy UI Borders 1.0 (one border, `Resources/UI/kenney_fret_s16.png`, tinted brass) | kenney.nl, downloaded 2026-11-07 | CC0 1.0; licence in `unity/Assets/_Game/Art/UI/Licences/` |
| Cinzel (variable) | Google Fonts, github.com/google/fonts | SIL Open Font License 1.1; licence in `unity/Assets/_Game/Art/UI/Licences/` |
| Rubik (variable) | Google Fonts, github.com/google/fonts | SIL Open Font License 1.1; licence in `unity/Assets/_Game/Art/UI/Licences/` |
| Owner art packs: "ShinobiDominion_UI_Buttons_Icons_Complete" and "ShinobiDominion_All_InGame_Buildings_Levels_1-10" (`Resources/UI/medal_*`, `icon_*`, `hud_*`, `badge_*`, `Resources/Buildings/*`), imported by `tools/art/import_art_packs.py` | Supplied by the owner, 2026-11-07; prototype art, to be reviewed before release | Owner-supplied. Some icon pictograms are renders of Font Awesome 4 glyphs: SIL Open Font License 1.1 (fontawesome.com/v4/license) |
| Owner banner and crystal sheet (`tools/art/sources/owner_p3_crystal_banners.webp`, `Resources/UI/banner_*`, `gem_crystal`) | Supplied by the owner in chat, 2026-11-07 | Owner-supplied |
| 3D heroes (`Art/Heroes3D/*`: the eight Legendary heroes, rigged and animated) | Made 2026-11-08 by Claude with Meshy (meshy.ai, owner's Pro plan, Meshy CLI 0.8.3) from the owner's hero paintings: image-to-image clean references, image-to-3D, auto-rig and the Meshy animation library; packed by `tools/art/pack_hero_3d.py`. Sources and per-hero reports in the owner's `DownloadsSD_Heroes_3D` | Generated on a paid Meshy plan, which grants the owner the rights to the output; check Meshy's current terms before release |
| NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk (`tools/core-tests` only, not shipped) | NuGet | MIT |

Add an entry here whenever code is copied or a package is added.
