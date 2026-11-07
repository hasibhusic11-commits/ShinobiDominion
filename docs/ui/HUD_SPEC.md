# Village HUD redesign: Attack button, resource counters, building actions

Status: approved by the owner on 2026-11-07 and built. The art is drawn by `tools/art/ui_kit.py` into
`unity/Assets/_Game/Resources/UI`; the importer `UiSpriteImporter` sets the nine-slice borders from the file names.
Differences from this text: the Kenney UI Pack is not used (its buttons are flat); one Kenney border is used as a
key-fret inlay on the building name plate; ATTACK glows rather than pulsing in size.
Scope: only these three parts. Gameplay, progression and the rules (ATTACK is the only thing docked to the bottom of
the idle screen, no energy counter, the build catalog opens only from the Builder Hall) are unchanged.

## What is wrong today (inspected in Unity, `before_home.png`, `before_building_panel.png`)
- Every control is the same code-drawn rounded rectangle in a different tint, so Attack, Upgrade, Move and Close
  have no hierarchy. The only "special" shape is the red disc behind ATTACK.
- The counters are long empty bars with the number pushed to the far right; the icon is a flat disc.
- Tapping a building covers a third of the village with a tall list panel, mostly empty, where the classic pattern
  is a small row of actions near the bottom with the village left in view.
- Text is Unity's default Arial with an outline.

## Canvas contract
Reference 1280 x 720, Scale With Screen Size, match 0.5 (as today). Everything sits inside the safe area. Checked at
16:9, 4:3 and 20:9. Touch targets at least 88 x 88 reference units (about 9 mm on a phone).

## Palette (tokens)
| Token | Hex | Use |
|---|---|---|
| lacquer | #16203A | panel and button faces |
| lacquer-deep | #0C1222 | shadows, insets, bar tracks |
| lacquer-light | #2B3B63 | top light on faces |
| brass | #B08A47 | frames, rings, rivets |
| brass-light | #E8C77E | bevel highlight, gold numbers |
| brass-dark | #6B4F22 | bevel shadow, outlines |
| charcoal | #2A2A30 | neutral buttons, disabled base |
| crimson | #8E2A26 | ATTACK, destructive actions |
| crimson-light | #C2483C | crimson highlight |
| crimson-dark | #4E1310 | crimson shadow and text outline |
| ivory | #EFE6D0 | text and icons |
| ivory-muted | #B9AF98 | secondary values |

## Typography
- Display (ATTACK, building name): a bundled serif display face with an open licence (proposal: **Cinzel**, SIL
  OFL, bold), ivory with a 2 px crimson-dark or lacquer-deep outline and a 2 px drop shadow.
- Numbers (counters, costs): a bundled heavy sans with tabular figures (proposal: **Rubik Bold**, SIL OFL), ivory,
  1.5 px lacquer-deep outline.
- Sizes: ATTACK 30, building name 24, counter values 22, action labels 16, cost tags 16. Nothing under 14.

## Button hierarchy (shared by every screen later)
| Kind | Shape | Face | Use |
|---|---|---|---|
| Hero | 168 round medallion, thick brass ring, 8 rivets | crimson lacquer | ATTACK only |
| Primary | 96 round medallion, brass ring | brass-gold face, ivory icon with dark outline | the building's own action (Build, Train, Collect) |
| Secondary | 96 round medallion, brass ring | lacquer face, ivory icon | Upgrade, Move |
| Destructive | 96 round medallion, brass ring | crimson face | Remove |
| Icon-only | 72 round medallion, thin brass ring | lacquer face | Info, Close, Menu |
States for every kind: normal; pressed (scale 0.93 for 60 ms, face 15% darker, ring keeps its colour); disabled
(face desaturated to charcoal, icon at 45% opacity, a lock pip if the reason is a level requirement; tapping plays
the denied sound and a small shake); hover on desktop only (scale 1.04, ring brightens). Focus ring for controller
later.

## 1. ATTACK
- 168 x 168, anchored bottom-right, 18 from the right and 14 from the bottom of the safe area.
- Layers, back to front: soft shadow (ellipse, 30% black, 8 down); brass ring 18 thick with a bevel lit from the top
  left and 8 rivets; crimson lacquer face with a glossy highlight across the top third; an ivory crossed-blades
  emblem (two kunai) in the upper half; "ATTACK" in display 30 across the lower half.
- Idle: a slow glow around the ring every 2.4 s (not a size pulse, so it never looks like it moved). Pressed and
  disabled as above. Hidden during raids, as today.

## 2. Resource counters (top right)
- Two counters, gold then crystals, each 188 x 46, 10 apart, 14 from the top and right of the safe area.
- Plate: lacquer face, 3 px brass rim, rounded 10, inner shadow along the top, nine-sliced.
- Icon medallion 54 round, brass ring, overlapping the plate's left end by 18: gold is a small stack of coins,
  crystals is a cut crystal, both painted, not flat discs.
- Value: Rubik 22, right-aligned, 14 from the plate's right edge. Up to 9,999,999 fits.
- Gold only: a 4 px capacity track along the inside bottom edge of the plate (lacquer-deep), filled brass-light to
  the vault fill fraction; turns crimson-light above 95% so a full vault reads at a glance.
- Change: the number rolls up over 0.4 s and the medallion bounces (already there); a loss flashes crimson once.

## 3. Building actions (shown while a building is selected)
- Replaces the tall left panel for the common case. The village stays in view.
- Name plate at bottom centre, 64 above the action row: 420 x 46 lacquer plate with brass rim; building name in
  display 24 on the left, "Lv 3 / 15" in Rubik 18 on the right.
- Action row at bottom centre, 14 from the bottom: medallions 96 round, 18 apart, in this order: Info (icon-only,
  72), the building's own action (primary), Upgrade (secondary), Move (secondary), Remove (destructive, only if
  removable). Each has a 16 label under it. Upgrade also carries a cost tag: a small lacquer pill with a coin icon
  and "800", crimson text when unaffordable.
- Row width is computed from the action count; never more than 5 actions, so it always fits between the left edge
  and ATTACK, including at 4:3.
- Info opens the existing details panel (description, stats before and after upgrade), which keeps the new frame.
- Opening: plate and row rise 24 and fade in over 180 ms, medallions 40 ms apart; closing is 120 ms.
- Tapping the ground closes it, as today.

## Assets
- Frames: Kenney **Fantasy UI Borders** (CC0): white ornamental border shapes, tinted brass in Unity and layered over
  our lacquer faces. Licence text kept in `unity/Assets/_Game/Art/UI/Kenney/License.txt` and
  `docs/THIRD_PARTY_NOTICES.md`.
- Button bases: drawn by us as nine-sliced sprites (lacquer face, bevel, gloss), since the Kenney UI Pack buttons are
  flat and would need repainting anyway; the pack is still useful for arrows and small glyphs.
- Icons: original pictograms (kunai pair, coins, crystal, info, up-arrow, four-way move, hammer, cross) drawn as
  white shapes and tinted.
- Fonts: Cinzel and Rubik, both SIL OFL, licence files kept beside them.

## How it will be checked
Screenshot in Unity at 1600 x 900, 4:3 and 20:9; compare against the mockup; check every state (normal, pressed,
disabled, unaffordable, full vault, 5 actions, 2 actions, longest building name, 9,999,999 gold); existing tests stay
green with the same button names.
