# Build catalog and shared panel chrome

Status: built after the HUD set the quality bar (owner, 2026-11-07: "Yes, keep going").
Same tokens, fonts and kit as `HUD_SPEC.md`. Gameplay unchanged: the catalog still opens only from the Builder Hall.

## What is wrong today (`before` screenshot: 03_build_catalog)
- The catalog is a full-screen grey page; the village disappears, so it feels like a website, not a window in the game.
- Tabs, cards and cost strips are the same rounded rectangles as every other button.
- "Needs a higher Dojo level" fills the price slot in a blue button, so a locked card reads as a button to press.
- Gold and crystals are repeated as words in the header although the counters are on screen.

## Window
- A framed window over the dimmed village (black at 45%), 1180 x 560, centred 20 below the middle so the counters
  stay visible above it.
- Frame: brass, 12 thick, bevelled, with a riveted corner cap at each corner; lacquer inside. Nine-sliced
  (`frame_s56`), with the key-fret inlay 10 inside the brass.
- Title: a crimson ribbon (`ribbon`, 360 x 62) centred on the top edge with "Build" in Cinzel 28.
- Close: the crimson icon-only medallion (64) over the top-right corner, half outside the frame.

## Tabs
- Five tabs across the top inside the frame, 196 x 58, 10 apart, each with its icon on the left and the label in
  Rubik 18. Selected: gold plate, dark text. Others: lacquer plate, ivory text. Icons: Structures hammer,
  Defense sword, Resources coins, Hero Base figure, Walls bricks.

## Cards
- 208 x 318, 14 apart, laid out in a row from the left; a sixth card wraps.
- Card: lacquer plate with a brass rim; a soft gold glow behind the building preview; name in Cinzel 19 on top.
- Two chips under the preview: footprint ("4 x 3") and count ("0 / 1").
- Price bar at the bottom (gold button plate): a coin and the cost in Rubik 20. Crystals cost adds a crystal.
- States: affordable (gold price bar); unaffordable (price in crimson-light on a lacquer bar); maximum built
  ("Built 1 / 1" on a charcoal bar); locked by Dojo (charcoal card overlay with a lock and "Dojo 3", and the card
  does not respond except a shake).
- Undo (offline only) is a small secondary medallion at the bottom left of the window.

## Shared chrome (all panels and screens)
- `PanelView.Show` (side panels) and `ShowScreen` (full screens) use the frame sprite, a Cinzel title, and the
  medallion Close / Back.
- `Ui.Button` picks its plate from its role colour: accent → gold, attack/vermilion → crimson, everything else →
  lacquer, disabled → charcoal. Text in Rubik bold with a dark outline.
