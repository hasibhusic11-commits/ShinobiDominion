# AUDIT_00: the UI as found, before the overhaul (2026-11-11)

Branch `feature/art-packs`. Unity 6 (6000.3.28f1), uGUI, URP. Before screenshots were captured from the real game
against the local server by the existing capture tests (`ScreenCaptureTests`). They are in
`screenshots/before/`. The Battle menu one is `screenshots/before/40_battle_menu.jpg`.

## How the UI is built
- No prefabs and no scenes for UI. Every screen is built in code each time it opens:
  - `VillageBootstrap` creates one Canvas, with CanvasScaler at 1280×720 and match 0.5, unchanged.
  - `VillageController.*.cs` partials hold one screen each and call `PanelView` in `Scripts/Game/UI/VillageUi.cs` (`ShowWindow`, `ShowScreen`, `ShowListWindow`, `ShowStage`, `ShowScene`).
- Shared helpers:
  - `Ui` for rects, anchors and backgrounds
  - `HudArt` for labels, pictures, sprites and drop shadows
  - `UiSkin` for the older runtime-drawn skin
  - `Medal` for round icon buttons
  - `ScreenKit` for list rows and tabs
  - `UiMotion` for PopIn, FadeIn, Pressable and DeniedShake
- Text is `GameText`, a TextMeshPro SDF wrapper. It uses Cinzel for display text and Rubik for numbers.
- Modal state is one `PanelView` per screen layer. `Hide()` tears down, and `IsOpen`/`IsFullScreen` gate input to the village.
- Data binding is direct. Each `Show*` reads `Village`, `Roster`, server lists such as `_raidList`, and `LastMessage`. Screens are drawn again when server data arrives.
- Sprites come from `Resources/UI`. `Editor/UiSpriteImporter.cs` reads 9-slice borders from the file name (`_s<N>`, and now `_s<LR>x<TB>`).
- Most older UI sprites are drawn by `tools/art/ui_kit.py`. The rest are the owner's painted packs.

## Theme status (before)
- There was no single theme class. More than 80 local `Color` literals were spread over the screen files.
- `docs/ui/UI_TOKENS.md` (v2) is the approved written spec, but the code only partly follows it.
- Fonts are consistent: Cinzel and Rubik.
- Frame thickness differs from screen to screen. Windows are mostly flat navy panels with a thin gold line.

## Five highest-impact problems on the Battle menu (from `before/40_battle_menu.jpg`)
1. **Building pictures, not mode scenes.** Each mode showed a building icon, so nothing said "duel", "siege" or "tower" at a glance.
2. **Navy on navy.** The cards were navy on a navy window, so they had no depth and no material.
3. **The window covered the resource bars.** The HUD was hidden while choosing a battle.
4. **Stale text.** It said "Your hero leads every battle", but ADR-048 now allows up to six heroes.
5. **Live data on one card only.** The rest were label-only tiles, with little hierarchy between them.

## Preserved decisions and conflicts (left unchanged)
- **Heroes, 6 vs 1.** This pack (written before ADR-048) says to keep one hero (ADR-030). The owner later chose up to six (ADR-048, 2026-11-09), and the code does six. The overhaul keeps the code as it is and only fixes the wording to match the current rule.
- **Building levels, 10 vs 15.** Older docs say 10 levels and later ones 15. The UI pass does not touch levels.
- Other preserved rules:
  - ADR-033 home layout: ATTACK at the bottom left and the left column of shortcuts.
  - No energy bar.
  - Summoning has no pity and is server-rolled.
  - Evolution stars show only evolutions actually earned.
- The button names the tests drive stay the same: `FindRivalButton`, `OpenDungeonsButton`, `OpenSiegeButton`, `OpenArenaButton` and `OpenTrialsButton` (used by ModeTests and ServerIntegrationTests).

## Resolution behaviour (before)
- At 1600×1200 (4:3) the window fills the width and the tiles shrink.
- At 2340×1080 the content stays at 16:9 width, centred, with empty sides.
- The safe area is not applied to windows. It is only applied to the HUD.

## Generated vs authored art
- Generated (`ui_kit.py`): frames, buttons, counters, medals and icons.
- Generated, new in this overhaul (`tools/art/sd_ui_kit.js`): every `sd_*` sprite.
- Authored by the owner: building packs, the village map and the scene paintings (`scene_*`). The five `mode_*` paintings are cut from these; see `ART_ASSET_REGISTER.csv`.

## Not verified in this audit
- Real-device safe areas such as notches.
- Frame time and GC on a device.
- Controller or keyboard focus.
