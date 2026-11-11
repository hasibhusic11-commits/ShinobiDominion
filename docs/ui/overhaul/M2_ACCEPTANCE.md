# M2: Battle menu acceptance (self-review, 2026-11-11)

**Evidence**
- `BEFORE_AFTER_BATTLE_MENU.png`
- `screenshots/after/battle_menu_{default,nohero}_{1280x720,1600x900,2340x1080,1600x1200}.jpg`
- `M1_COMPONENT_GALLERY.png`

All of these are real Unity renders, made against the local server by `UiOverhaulTests.CaptureBattleMenuAndKit`.

These scores are my subjective review of those screenshots (rubric in `02_UI_DESIGN_SYSTEM.md`). They are not a measurement, and the screen still needs the owner's approval.

| Criterion | Max | Before | After | Notes |
|---|---|---|---|---|
| Visual system consistency | 2.0 | 1.0 | 1.6 | One kit throughout: frame, ribbon, cards, plates, buttons. Other screens are still v2, so the game as a whole is not yet one system. |
| Hierarchy / readability | 2.0 | 1.1 | 1.7 | Title, then the hero note, then five equal cards, each with one verb. Body text is 17 to 18 ref px, and nothing drops below 13. |
| Art quality | 1.5 | 0.7 | 1.0 | Every mode has its own scene now. Dungeons and Trials are stand-in crops, and the source images are soft at 2340 wide (`ART_REQUIRED.md`). |
| Context | 1.0 | 0.5 | 0.8 | The dimmed village stays visible, and the HUD with its gold, Mana and gems is no longer covered. |
| Motion / response | 1.0 | 0.6 | 0.7 | Window opens over 0.2 s; cards pop in staggered; presses give feedback; locked buttons shake when tapped. No reduced-motion switch yet. |
| Functional integrity | 1.0 | 0.9 | 1.0 | Same five routes and same open rules; the button names are kept. The dungeon stars are real (0 / 45), and the locked cards say why. |
| Adaptivity | 1.0 | 0.6 | 0.8 | Reviewed at all four shapes. At 4:3 the window does not use the extra height. Real device safe areas are not tested. |
| Performance | 0.5 | n/a | n/a | Not measured on a device. |
| **Total** | 10 | ~5.4 | **~7.6** (of 9.5 scored) | Below the pack's 8.5 bar, mainly because of the art: the two stand-in paintings and low-resolution sources. |

## Defects found in screenshots and fixed
1. **Thin frame.** The window frame rim was thin and its corners barely showed. It was redrawn as `sd_frame_window_s96`, a heavier brass band with a groove, a recess and angular corner fittings with crimson gems, drawn at 1.5×.
2. **Avatar overlap.** The player's portrait ring overlapped the window's top-left corner. Fixed by raising `HudBand` from 104 to 118.
3. **Dead space** between the ribbon and the note. The row offset was tightened from 44 to 34.
4. **Long lines.** The Siege line wrapped to three lines, and the Arena line did too at 4:3. Both lines were shortened.
5. **Faint gold button text.** The light edge washed out the label on the gold face. The label is now dark brown with no edge.
6. **Gallery label wrap.** A gallery label wrapped; it was widened.
7. **Route test.** The test waited for "Rankings" while the Arena shows "RANKINGS" in capitals. It now waits for the real `ArenaRankingsButton` and `TrialFloor1` buttons.

## Known issues, not fixed in M2
- **4:3 HUD overlap.** At 1600×1200 the gold counter overlaps the player name plate. The HUD was like this before the overhaul (`before/15_aspect_1600x1200.jpg`), and it is outside the Battle menu.
- **Gallery underlay.** On the gallery capture only, a dark strip from the home HUD shows behind the title ribbon.
- **Art.** See `ART_REQUIRED.md`.

**Verdict:** ready for owner review as the first v3 screen. Do not roll v3 out to other screens until the owner has looked at this one.
