# UI kit v3: the design system as implemented

The code lives in `unity/Assets/_Game/Scripts/Game/UI/SdKit.cs`:
- `SdTheme` holds the tokens.
- `SdKit` holds the components.

The art lives in `Resources/UI/sd_*` and is drawn by `tools/art/sd_ui_kit.js`. The importer reads 9-slice borders from the file name, as `_s<N>` or `_s<LR>x<TB>`.

**Status:** v3 is used by the Battle menu only. Older screens keep v2 (`docs/ui/UI_TOKENS.md`) until they are migrated one at a time. Nothing global was recoloured.

## Token diff against UI_TOKENS.md v2
| Role | v2 | v3 (`SdTheme`) | Why |
|---|---|---|---|
| Ink | #0C0806 | #0B0B12 | the pack's night ink; cooler under navy |
| Lacquer | #16203A | #1A2741 | slightly lighter face so the pattern reads |
| Lacquer deep | #0C1222 | #0E1527 | as above |
| Brass | #B08A47 | #B08A47 | unchanged (approved) |
| Brass light | #F2D28A | #F2D28A | unchanged |
| Crimson | #8E2A26 | #92332F | the pack's crimson lacquer, a hair warmer |
| Crimson dark | #4A110E | #4A110E | unchanged |
| Ivory / muted | #EFE6D0 / #B9AF98 | #F4E9D2 / #C6B9A3 | brighter for small text over paintings |
| New: Jade, Slate, LockedTint, Dimmer (62%) | none | added | success, locked and modal states |

## Type (reference units at 1280×720)
| Role | Size |
|---|---|
| Title | 34 |
| Card titles | 22 (best-fit down to 14) |
| Buttons | 21 |
| Body | 17 (best-fit down to 13) |
| Notes | 18 |

Fonts are unchanged: Cinzel for display text and Rubik for numbers and body text. Text gets one ink outline, never more.

## Spacing
- 8-unit rhythm.
- `Gap` 16 between cards.
- `HudBand` 118: a window never covers the top HUD row, so gold, Mana and gems stay visible.
- `FrameInset` 40.

## Motion
- A window opens over `OpenSeconds` 0.2 s, scaling from 0.96 to 1 with a fade.
- Cards pop in with a 40 ms stagger.
- Presses use `UiMotion.Pressable`. A denied press shakes.

## Components
| Component | Call | Anatomy |
|---|---|---|
| Window | `SdKit.Window` / `PanelView.ShowThemedWindow` | 62% dimmer over the village; drop shadow; brass frame (`sd_frame_window_s96` at 1.5×: groove, recess, angular corner fittings with crimson gems and rivets); low-contrast lacquer lattice; crimson title ribbon; red close medal; optional Back medal |
| Title ribbon | `SdKit.Title` | crimson cloth with brass edges and ties; width follows the title |
| Button | `SdKit.Button(kind)` | War (crimson, fights), Gold (affirm), Navy (secondary); when disabled it turns slate, with a padlock if a reason is given; best-fit label |
| Badge | `SdKit.Badge` | small brass-edged plate with an icon and a live number; never shows invented data |
| Plate | `SdKit.Plate` | dark translucent plate so text reads over any painting |
| Framed picture | `SdKit.FramedPicture` | painting cropped to fill (EnvelopeParent) inside the brass card frame; greyed when locked |

The gallery is `M1_COMPONENT_GALLERY.png`, captured in Unity by `UiOverhaulTests.CaptureBattleMenuAndKit`.

## Rules for migrating the next screen
1. Open it with `ShowThemedWindow`. Do not hand-place a panel.
2. No colour literals in the screen file. Add a role to `SdTheme` if one is missing.
3. Keep every button name the tests use.
4. Capture at 1280×720, 1600×900, 2340×1080 and 1600×1200, review the shots, and fix what is wrong before moving on.
