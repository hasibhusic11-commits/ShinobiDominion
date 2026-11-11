# Art still required (honest blockers)

The v3 kit chrome (frames, ribbon, buttons, plates, badge) is original and drawn by `tools/art/sd_ui_kit.js`. It is good enough to ship for now.

The **mode paintings are stand-ins**. They are cropped from the owner's existing art because no dedicated illustrations exist. They read correctly at a glance, but a professional pass would replace them.

| Asset | Why it is not final | Brief for replacement | Used by |
|---|---|---|---|
| `mode_dungeons` | A crop of the village map's mountain corner. It says "terrain", not "descent into a fort". | 800×1000 portrait PNG, no alpha needed. A mountain pass with a cavern or fort gate, torchlight, path leading in. Upper-left light, the palette in `02_UI_DESIGN_SYSTEM.md`. The bottom 25% stays calm because a text plate sits there. | Battle menu Dungeons card |
| `mode_trials` | Now a real painting: the lit pagoda on a waterfall cliff under a purple spirit swirl, cut from the owner's Summon Reveal mockup. The source is only 194 px wide, so it is upscaled 2x and looks soft on big screens. | Same scene at 800×1000 or larger, so the cut is sharp. | Battle menu Trial Tower card |
| `mode_raid` | A crop of the village map. It shows a village but no raid. | 800×1000. A shinobi squad on a ridge over a rival village at dusk, with banners. | Battle menu Raid card |
| `mode_siege`, `mode_arena` | Crops of the owner's own screen mockups. They are good, but low resolution (about 300 to 520 px source), so they look soft at 2340×1080. | Higher-resolution source of the same scenes, 800×1000. | Battle menu |
| Icon family | The icons (`icon_*`) predate v3 and mix line weights. | One master set as listed in `02_UI_DESIGN_SYSTEM.md` §Icon system, at 128 px with transparent alpha. | All screens |

None of these block the Battle menu from working. They limit its art-quality score; see `M2_ACCEPTANCE.md`.
