# Sprite hero pipeline

**Status: one working prototype, Solar Beast Sage base form, on branch `feature/hero-sprites`. Not approved.** Heroes with no sprite entry still use the old generated figure, which stays only as a development fallback.

## How it works

| Piece | Where |
|---|---|
| Concept and layers | `art/heroes/<hero>/`, with assembly checks in `form0/_review/` |
| Layer tool | `tools/art/sprite_hero/solar_beast_sage_layers.py` |
| Layers in Unity, atlas, clips, Animator | `unity/Assets/_Game/Art/HeroSprites/<hero>/form<n>/` |
| Build step | `Scripts/Editor/HeroSpriteBuilder.cs` (menu: Shinobi Dominion > Build Sprite Heroes) |
| Prefab | `Resources/HeroSprites/<hero>/form<n>.prefab` |
| List of sprite heroes | `Resources/Config/hero_sprites.json` |
| Runtime | `HeroSprites.cs`, `HeroSpriteActor.cs` |
| Tests and captures | `Tests/PlayMode/HeroSpriteTests.cs`, `tools/unity/run_hero_sprites.ps1` |

Each hero is a root with a flat soft shadow on the ground and a `Visual` that turns to face the camera. `Visual` holds a `SortingGroup` and a hierarchy of 16 layers, each with its own `SpriteRenderer` and bone. An Animator plays Idle, Run and Attack.

Hero IDs, stats, levels, rarities, evolution rules, assignments and battle results are untouched. `BattleStage` and `VillageView` only read from them and choose how to draw the hero.

**Evolutions:** each form is its own entry in `hero_sprites.json`, with its own art and animations. Adding a form needs no gameplay change. A form with no entry uses the fallback.

## Decisions

- **Rigid layers, no mesh skinning.** Each layer rotates as one piece on a transform bone. `com.unity.2d.animation` and the PSD Importer are not installed. Skinning needs its weights painted in Unity's Skinning Editor, and rigid layers are cheaper on phones. Elbows and knees are hidden by overlap rather than bending. Skinning is the upgrade to try if a cloth or limb needs to bend smoothly.
- **Hand-keyed clips, created by the build step.** Timing can be changed in `HeroSpriteBuilder`.
- **One atlas per form.** All 16 layers pack into one texture, up to 1024 pixels. The project's sprite packer setting is now Always Enabled.
- **Unlit sprites.** They don't take the world's light or cast real shadows. The ground shadow is a soft sprite.

## How the art was made

The layers come from the approved concept, not new art. Script-drawn outlines split it into parts, and colour was grown into areas other layers hid: thighs under the tunic, cloth behind the hand and sash, the neck under the chin, and overlap at each joint. 78,287 of about 455,000 layer pixels at full size are rebuilt this way.

Those rebuilt areas are plain grown colour, not painted anatomy or cloth folds. Most stay hidden, but they can show when a limb swings far.

This does not fully meet the brief's art steps:
- The concept is close to front-on, not a clean three-quarter view. Sideways swings on a front-on drawing look less natural than they would on three-quarter art, most of all in the run.
- A proper version needs each part drawn complete by an artist or an image tool. The layout and tools here can take that art with new outlines.

## Test results (2026-11-07)

Run in a separate copy of the project on this branch, using the editor's graphics. All images are in `docs/heroes/sprite_prototype/`.

- **Tests:** 61 of 61 PlayMode tests passed on the final code: the village, raid, hero, input, visual and sprite suites. The separate capture run also passed. `ServerIntegrationTests`, `ReleaseModeTests` and `ScreenCaptureTests` were not run. One raid test counted 3D meshes, so it now counts units, with the same expected total.
- **Village:** the hero stands on a real Hero Base and the old figure is gone.
- **Battle:** the hero runs along the battle's path, faces its direction and plays Attack on hit events. The log decides everything.
- **Occlusion:** with 12 heroes around a building, the roof correctly hides those behind it, and those in front draw over it. Only this building and this camera angle were checked.
- **Crowd:** 30 heroes with half running: 510 sprites from 1 atlas, 9.2 ms a frame in the editor on this PC. That is not a phone figure.

## Not verified

- Android or iOS: performance, draw calls, texture compression and memory.
- Real-time motion as seen by a person. Reviews used still frames.
- The hero portraits, which still use the old figure.
- Zooming in and out with heroes next to other building shapes.
- Other camera positions on tall buildings. The sprite leans with the camera, so a hero pressed against a wall could clip into it.
- Later battle moments such as being hit or dying. Only Idle, Run and Attack exist.
- The large orange hit effect in `battle_attack_2.png`. This work did not change that effect, and it looks oversized beside the sprite.

## Package audit (2026-11-07)

| Package | Licence | Verdict | Status |
|---|---|---|---|
| LitMotion 2.0.2 | MIT | Useful for UI motion. Adds Burst, Collections and Mathematics. README doesn't state Unity 6 or uGUI support | Installed, not used yet |
| UIEffect 5.11.8 | MIT | Useful for uGUI panels and buttons. README states URP support | Installed, not used yet |
| SoftMaskForUGUI 3.6.6 | MIT | Useful for soft edges on the game's scrolling lists. README warns about URP render scale and dynamic resolution | Installed, not used yet |
| Unity-MCP | Apache-2.0 | Not safe to connect as it stands | Not installed |

The owner approved the three UI packages on 2026-11-07. Each was added on its own with its official git URL and a pinned version, and each compiled with no errors. The same 61 PlayMode tests still pass with all three installed. Nothing in the game uses them yet, so their look and phone cost are untested. UIEffect and SoftMaskForUGUI added their default settings in `Assets/ProjectSettings/` and set them to preload in builds. Nothing about the sprite hero needs these packages.

**Unity-MCP:** its README requires a project path without spaces, and this project is under `Game Design`. It also exposes a tool that compiles and runs any C# code, defaults to no authorization, and can use a cloud endpoint. To use it, the project would have to move first, and the tools and cloud endpoint would need restricting.

**Skills:** neither a custom Shinobi Dominion skill nor UI UX Pro Max is installed for this session, and UI UX Pro Max isn't in the plugin catalog.

`docs/package_backups/` holds the package files from before this branch.
