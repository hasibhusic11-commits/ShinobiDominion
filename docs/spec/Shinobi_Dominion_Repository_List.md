# Shinobi Dominion — Recommended GitHub repositories (November 2026)

**Important:** Review pinned release, license, assets and Unity compatibility before installing. Reference demos must be isolated. User-defined game rules are in `Shinobi_Dominion_Claude_Code_Master_Prompt.md`.

| Repo | Link | Priority | Intended use | Caveat |
|---|---|---|---|---|
| Nakama | https://github.com/heroiclabs/nakama | Core | accounts, matchmaking, social, backend RPCs | custom transaction logic required |
| Nakama Unity SDK | https://github.com/heroiclabs/nakama-unity | Core | Unity game-server connection | install supported SDK release |
| Nakama project template | https://github.com/heroiclabs/nakama-project-template | Core reference | examples: storage, wallets, RPCs | adapt; don't assume built-in all-or-nothing wallet+storage transaction |
| Grid placement example | https://github.com/manlaig/object_placement_unity | Reference | grid footprints/collision/build timing | minimal example; likely older Unity |
| RTS Demo | https://github.com/KarahanKARA/rts-demo | Reference | unit movement, combat, pathfinding | Unity 2021; don't merge entire project |
| Flexi | https://github.com/PhysaliaStudio/Flexi | Optional integration | skills, stats, effects | work in progress, spike first |
| Unity Toon Shader | https://github.com/Unity-Technologies/com.unity.toonshader | Evaluate | original cel-shaded heroes | check URP/device performance; Unity Companion; sample art has distinct terms |
| URP Toon Shader Example | https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample | Alternative | lightweight anime shader code | educational MIT example, not commercial full NiloToon |
| LitMotion | https://github.com/annulusgames/LitMotion | Recommended | summon/evolution/UI tween animation | MIT, confirm package versions |
| Cinemachine | https://github.com/Unity-Technologies/com.unity.cinemachine | Official package | camera composition/transition | install through Unity Registry |
| Addressables samples | https://github.com/Unity-Technologies/Addressables-Sample | Reference | content loading patterns | examples only; install real package via Unity |
| Input System | https://github.com/Unity-Technologies/InputSystem | Official package | touch & mouse | install via Unity Registry |
| UniTask | https://github.com/Cysharp/UniTask | Optional integration | efficient async | MIT; avoid if unnecessary |
| Unity VFX samples | https://github.com/Unity-Technologies/VisualEffectGraph-Samples | Reference | aura/VFX ideas | heavy/older samples, don't blindly import |
| Boss Room | https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop | Future reference | multiplayer RPG networking patterns | not needed for asynchronous MVP raids |
| Unity-MCP | https://github.com/IvanMurzak/Unity-MCP | Optional dev tool | Claude Code controls Unity Editor | review auth/data exposure and local install requirements |

**Official non-repo Unity packages:**
- Unity AI Navigation: `com.unity.ai.navigation` (current registry release compatible with chosen Unity).
- Unity Test Framework (current registry release).
- Unity Addressables, Input System, Cinemachine (only when needed).

**Explicitly avoid as new foundation:** https://github.com/Unity-Technologies/NavMeshComponents (Unity says development moved into AI Navigation); aged Unity 4/5 RTS demos with unsuitable licenses; ripped commercial-game assets.

**Adoption recommendation:** Start with Nakama + Nakama Unity SDK + Nakama project template; native Input System/AI Navigation/Test Framework; build grid logic in-house after studying the placement demo. Add LitMotion, Addressables, toon shading later. Test Flexi in an isolated spike; avoid integrating Boss Room as a second networking stack.
