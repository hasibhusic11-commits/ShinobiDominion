# Repository audit

Audited 2026-11-05 through the GitHub REST API and raw license files. **No third-party code has been cloned, copied or run.** Licenses come from each repo's own `LICENSE` file; where GitHub reported `NOASSERTION` the file itself was read.

| Repo | Status | License | Pin | Last push | Why |
|---|---|---|---|---|---|
| heroiclabs/nakama | **INTEGRATE** (Phase 3, server container) | Apache-2.0 | release `v3.42.0` | 2026-11-05 | Core backend. Released two days ago; re-check for a patch release when Phase 3 starts. |
| heroiclabs/nakama-unity | **INTEGRATE** (Phase 3) | Apache-2.0 (`Packages/Nakama/LICENSE.md`) | release `v3.22.1` | 2026-11-03 | Official SDK. Not installed until Phase 3 needs it. |
| heroiclabs/nakama-project-template | **REFERENCE ONLY** | Apache-2.0 | `6cab10c292049449d41882081a732c3bda6a1c3c` | 2026-05-13 | Pattern source for Go RPC modules. Nothing merged wholesale. |
| manlaig/object_placement_unity | **REFERENCE ONLY** (not used so far) | MIT | `677f3b98a4c884895a0d7ccec8a672c048cd4696` | 2020-02-20 | Stale, last pushed 2020. Grid logic was written in-house, so no code was taken. |
| KarahanKARA/rts-demo | **REJECT for code reuse** | **None found** | `449d574a39d411274fcadb25b5855559f987cb2f` | 2025-12-24 | No license file at root and GitHub reports no license, so default copyright applies. Do not copy from it. |
| PhysaliaStudio/Flexi | **DEFER** (spike in Phase 4) | MIT | release `v0.14.1` | 2026-03-16 | Pre-1.0. Only worth evaluating when hero abilities exist, and combat must stay deterministic. |
| Unity-Technologies/com.unity.toonshader | **DEFER** (Phase 5 benchmark) | Unity Companion License | registry `0.16.1-preview` | 2026-11-02 | Still a preview package. Compare on a real device before choosing. Sample assets are not to be imported. |
| ColinLeung-NiloCat/UnityURPToonLitShaderExample | **DEFER** (Phase 5 alternative) | MIT | `fbe276e0df1d1f9379e13581c20b6a080a1a6977` | 2026-07-02 | Lightweight comparison shader. Only one toon pipeline will be adopted. |
| annulusgames/LitMotion | **DEFER** (Phase 3 reveal animation) | MIT | release `v2.0.2` | 2026-09-18 | Not needed for Phase 1. |
| Unity-Technologies/com.unity.cinemachine | **DEFER** | Unity Companion License | registry `3.1.7` | 2026-11-04 | The custom isometric camera covers Phase 1. Reconsider for battle camera transitions. |
| Unity-Technologies/Addressables-Sample | **REFERENCE ONLY** | **No license file at checked root paths** | `3112f7dc30f30d4c118a03de9c3d7ccc56e89bb2` | 2026-05-06 | Read for patterns only. Real package comes from the registry (`4.1.2`) when hero skins need it. |
| Unity-Technologies/InputSystem | **INTEGRATE** via registry only | Unity Companion License | see `Packages/manifest.json` | 2026-11-05 | Installed as a package, never from the `develop` branch. |
| Cysharp/UniTask | **DEFER** | MIT | release `2.5.11` | 2026-10-06 | Optional in the brief. Standard async is enough until networking proves otherwise. |
| Unity-Technologies/VisualEffectGraph-Samples | **REFERENCE ONLY** | Unity Companion License | release `14.0.8-rev1` | 2024-11-19 | Old and heavy for mobile. Do not import. |
| Unity-Technologies/com.unity.multiplayer.samples.coop | **REFERENCE ONLY** (future) | Unity Companion License | release `v3.0.0` | 2026-09-13 | Raids are asynchronous, so this second networking stack is not wanted. |
| IvanMurzak/Unity-MCP | **DEFER** — needs your approval | Apache-2.0 | release `1.7.0` | 2026-11-05 | README states the project path cannot contain spaces, and this project sits under `Game Design`. Its setup also uses an OAuth sign-in to `ai-game.dev`, an external account. Released today, so too fresh to pin anyway. |
| Unity-Technologies/NavMeshComponents | **REJECT** | MIT | none | 2023-05-17 | Obsolete; the brief forbids it. Replaced by `com.unity.ai.navigation`. |

## Registry packages in use for Phase 1

| Package | Why |
|---|---|
| `com.unity.render-pipelines.universal` | URP, required by the brief |
| `com.unity.inputsystem` | Touch and mouse parity |
| `com.unity.ugui` | Runtime UI (see ADR-004) |
| `com.unity.test-framework` | EditMode and PlayMode tests |
| `com.unity.nuget.newtonsoft-json` | Save and config JSON shared with the .NET test harness (see ADR-003) |

`com.unity.ai.navigation` and `com.unity.addressables` are held back until a phase needs them, because the brief asks for minimal dependencies. See ADR-006 for pathfinding.

## Checks not yet done

- Third-party *contents* were not reviewed, only metadata and license files. A code and security read is required before any DEFER item is adopted.
- Package compatibility with Unity 6000.3 is proven only once the Unity Editor resolves `manifest.json`.
