# Shinobi Dominion — coherent classic mobile fantasy art direction system

## North Star

**A handcrafted shinobi village command interface**: compact and tactile like early mobile base-building strategy games, rich with layered materials, deliberate affordances and collectible-hero drama, but with ORIGINAL architecture, insignia, icons, frames and compositions. The user should say: *this feels like a beautiful fantasy strategy game*, not *this looks like a website* and not *this is a trace of Castle Clash*.

## Pillars
1. **Tactile craft**: lacquer, carved timber, brass, stone, parchment, embossed cloth; depth via silhouettes, border bevels, gloss, inner shadows, not excessive filters.
2. **Readable from two feet**: content and numerals win over ornament; mobile-first thumb-size actions; crisp scaling at screen sizes actually supported.
3. **One visual language**: same corner caps, inner border bevel, frame inlays, icons, tab shapes, progress bars, nameplates and close buttons on EVERY screen.
4. **Contextual navigation**: a build window looks like a building interface; hero page like a premium character collection; combat screen like a tactical war menu, using a unified family of materials.
5. **Visible game world**: windows should sit above a credible blurred or darkened world, or a thoughtfully illustrated panorama, not float in developer-grade empty blue rectangles.
6. **Interaction feedback**: touch response within 1 rendered frame when possible, subtle depressed face and SFX; meaningful rarity reveals; no constant flash everywhere.
7. **Originality**: borrow broad genre conventions, not protected title typography, copyrighted 2D sprites, exact border art, recognizable Naruto symbols, or logo designs.

## Base palette (starting tokens — tune with real screenshot testing)
| Name | Value | UI role |
| --- | --- | --- |
| Night ink | `#0B0B12` | strongest outline and shadow |
| Deep lacquer | `#11182B` | dark window base |
| Lacquer | `#1A2741` | main panel face |
| High lacquer | `#314363` | face highlights, active hover |
| Aged brass | `#B08A47` | main frames |
| Brass shadow | `#60471F` | chamfer recess |
| Brass highlight | `#F1D58C` | metal edge highlights |
| Crimson lacquer | `#92332F` | primary battle action and headers |
| Ember vermilion | `#D35743` | brighter red highlight only |
| Ivory | `#F4E9D2` | primary text and parchment |
| Muted ivory | `#C6B9A3` | secondary text |
| Warm parchment | `#E9D4A7` | readable light detail region |
| Positive jade | `#44B17F` | claim/success, sparingly |
| Storm cyan | `#48C8EE` | mana, selected, live magic details |
| Gem violet | `#9D54DE` | premium crystal / Elite reveal |
| Locked slate | `#515867` | inactive states |

***Important:*** existing `docs/ui/UI_TOKENS.md` is already a working spec and approved UI elements exist. Use this palette as a conceptual refinement, NOT as permission to silently override the repo's approved base values. Build an explicit token diff and get visual agreement before globally changing old screens.

## Typography
- **Display:** project's existing licensed Cinzel or equivalent licensed custom fantasy serif; used for category and screen headers, hero names, rarity banners. NOT for every tiny number.
- **Operational:** project's existing Rubik family with tabular numbers for currency, level, cooldown, costs, percentages, progress and regular body text. Avoid the built-in Arial look.
- Suggested reference sizes at 1280×720: 33–38 main screen title, 22–26 major subheading, 18–22 buttons, 17–20 body, 15–16 support text; never rely on an 11 px description as the sole way to understand action. Adapt sizes to actual device density and visual testing.
- Avoid dense double-stroked text. Use max ONE dark stroke on busy background and maybe a subtle shadow. Preserve native glyph rendering and no blurry scaling.
- Headers use coherent title case, stable spacing and 1–2 font families max. Prevent all-caps decorative serif overload.

## Geometry and sizing system
- Reference design canvas: `1280×720` landscape, CanvasScaler `Scale With Screen Size`; inspect project current match setting (existing HUD_SPEC says 0.5). Do not change by accident.
- Establish safe-area-aware root. Handle 16:9, 19.5:9 or 20:9 wide phones, 4:3 tablets and small supported resolutions.
- Edge safe margin: ~16–24 reference units inside OS safe bounds, plus device-specific notch/gesture margins.
- Spacing: coherent 8-unit grid, with 8 / 16 / 24 / 32 / 48 purposeful rhythm; no arbitrary 13 and 27 mixtures.
- Touch targets: prefer 72–96 reference units for hero medallions; ordinary navigation/close actions minimum ~44–48 device-independent px physical equivalence, subject to device test. The repo HUD_SPEC uses 88×88 ref targets for key actions: preserve/evaluate instead of shrinking.
- Main content must remain accessible without edge clipping; lists scroll, not squeeze text to near zero.
- HUD readable in landscape on a phone; favor larger labels and fewer simultaneous items.

## Material stack / anatomy
A lacquered window is **not just a flat navy rectangle with a gold stroke**. Build it from named and reusable artwork layers, e.g.:
1. 4–6 px dark silhouette / cast shadow behind window.
2. softly shaded brass external rim with four original angular corner fittings or rivets.
3. inset darker bevel and secondary inner line.
4. deep lacquer center with subtle grain / brush / embossed wave pattern (low contrast).
5. clear crimson title plaque overlapping the top edge with designed endcaps and tiny ornamental ties.
6. section divider line, sometimes with a centered diamond knot.
7. content as readable separate surface (dark lacquer for high-density lists; warm parchment for text-heavy selected detail).
8. explicit x/back medallion with 3 states.
9. optional ambient highlight behind modal, not generic glows around every corner.

**Render assets as original transparent PNGs**; use `Image.Type = Sliced` and importer borders to protect engraved corners during resizing. A round medallion is rendered as a separate circular asset, not stretched from a 9-slice panel. Keep original art palette and lighting direction consistent (upper left).

## Button component family
- Action Medal: oversized attack button red lacquer + prominent crossed weapon mark; only one permanently docked major attack affordance on home.
- Primary Gold Plate: affirmative `Claim`, `Open`, `Upgrade` when affordable, `Continue`, context as approved.
- Crimson War Plate: combat commitment (`Fight`, `Raid`, `Defend`) or major destructive emphasis if deliberate.
- Secondary Navy Plate: `Rankings`, `Back`, `Details`, `Cancel`, navigation.
- Danger: red close / remove / quit where semantics justify.
- Tab: inlaid angular frame; selected=gold or heated red, inactive=dim navy; selected indicator must be visible without just color.
- Locked: dark slate art, lock pictogram, clear short gating note, not an active bright CTA.
- Purchase: gold/blue/gems icon, amount and affordance, disabled reason.

Button state matrix: normal, pressed, selected, disabled, locked, loading, success, focus/hover where relevant. Pressed face darkens and translates 2–3 units for 80–120ms; ring and label remain legible; tap feedback shouldn't require glitter. Disabled actions should remain distinguishable. UI must not generate sound/haptics repeatedly in idle states.

## Resource tokens — NO NEW ECONOMIES
- User's game primarily uses gold, mana and gems; project older documents use variable terms such as crystals/chakra. Audit live configuration and approved latest glossary, bind actual values and icons; do not insert wood, elixir, energy or made-up currencies.
- Icon badge partially overlaps one end of the dark counter; number uses tabular numerals; + action physically separate and clearly clickable.
- Never show a default `0` as fake live data when real account value is unavailable; use loading or authorized fallback.
- Reuse same three icon silhouettes on every window, price tag, quest reward and summon display.

## Rarity effects
- Common: restrained slate / bronze borders, modest texture, no constant glow.
- Elite: cool violet edging, one small glow, high-contrast distinctive hero illustration; do not fake legendary-level overdesign.
- Legendary: elaborate but tasteful gold border; disciplined rare particle burst, unique reveal audio and a larger hero portrait. Use original cutout art with verified transparency.
- Heroes must retain same actual identity across details and summoning; do not show placeholder mannequin if hero portrait exists. If missing, use an honest `ART PENDING` development state instead of a false asset.

## Icon system
- One coherent vocabulary: 1.5–3px approximate proportional stroke/shape feel at target sizes, dark border for readability, gold/ivory fill, small controlled shadow; avoid mixing Lucide thin strokes, flat emoji, 3D photoreal icons and low-poly primitives in the same row.
- Create master icon set: back, close, check, lock, info, upgrade, move, collect, build, coin, mana, gem, mail, quests, gift, settings, leaderboards, arena, shields, swords, raid, dungeon, village defense, summoning, star, rank medal, guild, hero.
- Export at high res with transparent alpha and strong negative space, plus consistent internal padding.
- Rarity star evolution indicator uses **only bright blue earned stars** — 0 = no stars, 1–3 = exact count earned — not extra Evo I/Evo II/Evo III buttons. Don't touch the evolution rules.

## Visual background logic
- Screen art is deliberately subordinate to data. Avoid overuse of cherry blossoms, full-screen painting under every text row, gold flourishes at all four corners, and random pagoda silhouette pasted behind controls.
- Full screens: a dim panoramic ninja landscape on one edge or in a top strip; a rich readable central frame.
- Modals over village: keep actual village visible, dim 40–65% depending contrast, optionally modest depth-of-field blur if performance permits (never blur critical exposed HUD); central dialog visible on phone.
- Summon reveal: momentary dramatic rarity burst and then rest. Summon result: calm list/result state. No gratuitous animated gradient.
- Arena: a distinct illustrated duel arena motif; not the same generic temple image used everywhere.
- Siege: fortified gate and banners. Dungeons: descent / mountain cavern. Guild: heraldry, organized banners. Each mode identifiable at a glance.

## Interaction and transition language
- Tap feedback: 60–120ms depression / SFX.
- Window open: 160–220ms scale from 0.96→1 and fade; overshoot 1.01 at most; close 100–160ms.
- Tab change: 120–180ms highlight slide, selected content crossfade; preserve scroll position when warranted.
- Selected list row: warm interior and stronger rim, not disco glow; keyboard/controller focus ring separately.
- Hero summon: rarity-appropriate burst 450–850ms; optional skip; user can tap to advance; no animation blocks server outcome logic.
- Victory/loss: unique SFX and art keyed to outcome; avoid explosion for a loss.
- Reduced-motion option: all meanings are still conveyed in static UI.
- Particle budget: one localized active effect at a time per modal; no full-screen repeated particles on menus.

## Mobile optimization
- Avoid overdraw from seven transparent full-screen layers; use 1–2 main background composites and efficient 9-slice chrome.
- Pack related sprites in atlases where it helps (preserve quality and deterministic import settings).
- No animated blur shader on every row. Test UIEffect or post-processing on representative devices before adopting.
- Avoid creating/destroying dozens of canvases on every UI update; reuse pooled cards and cached sprites.
- Don't regenerate string-heavy TMP components every frame; update when data changes.
- Test sprite compression vs text clarity and bright gem edges, including alpha halo quality.

## Professional critique rubric (10 point target)
- **2.0 pts** Visual system consistency: one art direction across components/screens.
- **2.0 pts** Hierarchy/readability: clear primary task, readable type, relevant data; no blank panels.
- **1.5 pts** Art quality: actual original sprites, material depth, coherent shading, recognizable icon family.
- **1.0 pts** Context: themed specific scene/background enhances rather than obscures game purpose.
- **1.0 pts** Motion/response: clear interactive states, tactility, restrained duration and accessible modes.
- **1.0 pts** Functional integrity: every control is real and data bound, no invented rewards/currencies.
- **1.0 pts** Adaptivity/mobile: fit, safe area, taps, readability at multiple aspect ratios.
- **0.5 pts** Performance: measured frame times, low GC, acceptable GPU UI draw cost.

**Never claim an automatic numeric grade as factual.** Score with screenshot evidence and explicit subjective reviewer notes. Aim 8.5+ on review before broad deployment; refine until owner approval. A visual mockup image alone is NOT implementation.
