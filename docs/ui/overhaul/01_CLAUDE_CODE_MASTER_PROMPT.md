# SHINOBI DOMINION — MASTER UI/UX VISUAL OVERHAUL DIRECTIVE

**Owner instruction to Claude Code | November 2026 | Game engine: Unity 6 / URP / existing uGUI**

> THIS IS A FULL PRODUCTION EXECUTION PROMPT FOR A GAME IN AN EXISTING REPOSITORY. READ ENTIRELY BEFORE CHANGING CODE. IT IS NOT A REQUEST TO REBUILD ANOTHER GAME, GENERATE A LANDING PAGE, OR GIVE ME ANOTHER GENERIC DESIGN SUGGESTION.

---

# SECTION 00 — ROLE, MISSION AND NON-NEGOTIABLE OUTCOME

You are now the **principal game UI/UX art director, experienced mobile strategy-game interface designer, Unity uGUI engineer, asset-pipeline engineer, QA automation specialist, and severe internal visual critic** for **Shinobi Dominion**.

The game is an original, shinobi-themed mobile strategy/base-building/hero-collection game. It already has substantial functional code, a server, buildings, combat modes, heroes, and working screen navigation. **The problem is presentation.** It looks visually unfinished: enormous flat blue rectangles, thin gold borders, tiny text, long dead space, repeated generic buttons, weak silhouettes, undersized character renders, placeholder player faces, mechanically stacked rows and backgrounds without purpose. The screenshots reveal an interface made as a collection of CRUD panels rather than as a crafted fantasy game. We must change that.

My **quality reference** is the tactile, characterful, highly readable, heavily illustrated but functional presentation of **classic early-era Castle Clash** and the broader 2013–2016 hand-painted mobile base-building tradition. The goal is **not to clone Castle Clash**. We will borrow generic genre qualities: impactful game buttons, rich framed panels, appealing hero cards, compact resource bars, personalized menus, collectible hero reveals, scene-specific scenic art, and fun responsive interactions. We will create ORIGINAL designs in Shinobi Dominion's unique feudal-shinobi identity. **Do not trace, reproduce, extract or ship Castle Clash artwork, logos, icon shapes, animations, frame layouts, textures or protected characters.** Do not use Naruto/Uchiha symbols, village crests, signature eye patterns or other protected art. Keep our own identity, compositions, wood/metal geometry, icon silhouettes and decorative motifs.

**This is an IMPLEMENTATION assignment.** You must actually inspect the repository, identify the relevant UI code/assets, generate production code and assets where possible, place them into the Unity project, launch or batch-run the actual scene, capture screenshots, compare screenshots, identify defects, fix those defects, and demonstrate objective progress. Deliver a tested first vertical slice and plan the remaining screens with dependencies. Do not return a polished Markdown plan and call it complete. Do not invent screenshots or claim that art exists if it doesn't.

**Absolute priority**: actual visual polish from consistent assets, art direction and measured iteration is more important than writing another thousand lines of generic UI rectangles. If a beautiful nine-slice image is missing, STOP and flag the missing **ART ASSET** rather than drawing another default blue box with a single gold border and calling it premium.

**The project is substantial and has its own authoritative gameplay rules.** Your UI overhaul must keep them working, not overwrite them.

---

# SECTION 01 — READ-ONLY REPOSITORY DISCOVERY: DO THIS FIRST

1. Find the Git root. Do not initialize a new project or overwrite existing assets. Record `git status`, current branch and exact Unity project path; identify any uncommitted changes and do not discard them.
2. Read `CLAUDE.md` in full.
3. Read `docs/spec/Shinobi_Dominion_Claude_Code_Master_Prompt.md` in full.
4. Read `docs/DECISIONS.md` **including newer ADRs**. **Latest documented owner approval overrides earlier superseded design**. Flag any conflicts; DO NOT change gameplay to resolve them.
5. Read `docs/ui/UI_TOKENS.md`, `docs/ui/HUD_SPEC.md`, `docs/ui/CATALOG_SPEC.md`, `docs/ART_STATUS.md`, `docs/VISUAL_BASELINE.md`, `docs/THIRD_PARTY_NOTICES.md`.
6. Read this pack's `02_UI_DESIGN_SYSTEM.md`, `03_SCREEN_ACCEPTANCE_MATRIX.md`, and `04_REPOS_AND_TOOLS.md`.
7. Inspect screenshot reference images in `docs/ui/overhaul/references/current/` (owner's actual screen contact sheets). Inspect concept imagery in `references/redesign_concepts/`, but treat it as *mood/quality direction* only; it contains invented sample reward values and is not functional authority. Inspect `docs/screenshots/` and `docs/ui/v*` to establish development history.
8. Inventory the source files, relevant `.prefab`, `.unity`, `.asset`, `.png`, `.meta` and `manifest.json` entries. Start with:
   - `unity/Assets/_Game/Scripts/Game/UI/VillageUi.cs`
   - `unity/Assets/_Game/Scripts/Game/UI/HudArt.cs`
   - `unity/Assets/_Game/Scripts/Game/UI/UiMotion.cs`
   - `unity/Assets/_Game/Scripts/Game/UI/HeroArt.cs`, `HeroConcepts.cs`
   - `unity/Assets/_Game/Scripts/Game/VillageController.Home.cs`
   - `VillageController.HeroScreens.cs`
   - `VillageController.ActionBar.cs`
   - `VillageController.Campaign.cs`
   - `VillageController.Raid.cs`
   - `unity/Assets/_Game/Scripts/Editor/UiSpriteImporter.cs`
   - `unity/Assets/_Game/Scripts/Editor/ProjectSetup.cs`
   - `tools/art/ui_kit.py`
   - `unity/Assets/_Game/Resources/UI`, `Assets/_Game/Art/UI` if present
   - Unity project package and assembly definition configurations.
9. Determine how uGUI canvases are currently generated, fonts loaded, modal state managed, data bound, event callbacks connected, and screenshots captured. Map each screen entry point and its dependencies. No speculative rewrites until you know the exact call graph.
10. Create `docs/ui/overhaul/AUDIT_00.md`: file/line locations, owners, list of generated vs authored assets, screen-by-screen screenshot links, warnings, theme drift, missing assets, resolution behavior, and unverified states.

## Important current repository facts to verify rather than overwrite

- The project uses **uGUI**, and `tools/art/ui_kit.py` creates many current UI sprites.
- Approved HUD and catalog spec files already exist. Preserve their working layout and accepted components unless explicitly improved and visually validated.
- The older master prompt initially describes six simultaneously deployed heroes; the later **ADR-030** says the owner moved to **one active hero** and the game's current code likely follows it. **DO NOT reinstate six heroes during a UI pass.** Note the contradiction and preserve implemented behavior. If the owner later changes the rule, that's a separate gameplay change.
- The later **ADR-033** changes home HUD to Attack at bottom left with left-side circular shortcuts and current currency design. This supersedes older attack-only diagrams. Preserve latest actual approved layout; do not add a second navigation bar or permanent Build/Hero/Summon button.
- **No energy/stamina bar** as a top-level mechanic. Do not invent one.
- Summoning has independent RNG, **no pity** and no guaranteed rarity from multi-pulls; a UI animation must not imply guaranteed outcomes.
- Legendary evolution is Base + three transitions. Evolution stars represent **actual achieved evolutions only**, 0–3 cyan stars, not Evo tabs. Do not change evolution costs, levels, or power rules.
- No hero pets/companions. An animal-shaped motif in a still effect must not become a second controllable creature.
- **No new currencies.** The owner's present economy uses Gold, Mana and Gems; repository documents contain legacy Crystal/Chakra terms. Inspect authoritative live resource definitions and owner-approved naming. Map correct icons and labels; do not invent a wood, food, stamina, or fourth premium resource.
- The game includes Hero Base, Summoning Gate, Builder Hall, Hero Altar and building-context navigation. Do not put permanent screen shortcuts where gameplay requires using buildings.
- **Grid world** is a 50×50 buildable isometric village. UI may not obstruct selection and building placement; keep UI canvas and world input coordinated.
- There are current code decisions regarding 10 vs 15 building visual/functional levels. Do not secretly alter levels or stats while updating UI; read the current live configuration and document discrepancies for a separate approval.

## Baseline report MUST contain

A) A screen map showing current scene/controller methods for **Village Home, building actions, Build Catalog, Hero Roster, Hero Detail, Evolution, Summoning Gate, Summon Reveal, Summon Results, Battle Menu, Raid, Dungeon/Campaign, Village Siege, Siege Result, Arena, Arena Result, Rankings, Daily Quests, Daily Gift, Guild Browse, Guild Detail/Chat** and any other screens you discover.

B) A reusable UI asset map: which frames/buttons are real PNGs, procedural sprite textures, shader/composite, code-built filled rectangles, and textual effects.

C) A measurable visual debt list ordered by impact: e.g. too much empty area; cramped text; all rows look identical; empty rewards data; hero portrait too small; inconsistent resource icons; flat default backgrounds; scroll clipping; false button affordances; insufficient touch area.

D) A one-screen **before screenshot** with saved PNG (not only a verbal description), resolution and pixel dimensions, obtained from the actual Unity game if runnable. Include a frank note if running Unity isn't available.

---

# SECTION 02 — WORK SAFELY: BRANCH, INTEGRATION BOUNDARY, STAGE GATES

1. Create a reversible feature branch, e.g. `feature/ui-art-direction-overhaul`, **only if working tree state allows**. If there are unsaved owner changes, keep them intact and ask only if truly destructive action is unavoidable. No force pushes, no wipes.
2. Work inside Game/UI, art and tests. Keep **Core** mechanics, backend/Nakama, database migrations, hero RNG, economy and battle simulation untouched. If a UI bug requires changing core code, write the minimal interface adapter and explain it, never change core semantics.
3. Keep old UI assets reachable for rollback until redesigned screens pass tests and screenshots. Avoid wholesale deleting existing assets.
4. Do not replace uGUI with UI Toolkit. The existing project is already uGUI; Unity 6.6 official docs list uGUI as recommended for runtime. A migration would introduce risk and cost without solving poor art direction.
5. Prefer small, named, reusable prefab/component classes over another giant procedural screen class. Use scalable nine-slice assets and containers; no hardcoded pixel rectangles for all pages.
6. Break the overhaul into milestones. Complete and test first milestone before broad migration. A single screenshot of one excellent screen is more useful than 20 mediocre new screens.
7. Test current code before and after: use `.NET` core tests, Unity editor compile, scene play test and screenshot capture. Keep server UI state authorized and traceable. Don't claim tested without logs.
8. Owner retains approval of new art styles, purchases, externally authenticated tools and irreversible content changes.

## Milestone schedule

- **M0 — Audit:** no UI rewrites, deliver inventory, screenshot baseline, audit note, design gap checklist.
- **M1 — Shared visual foundation:** introduce or improve four art families: master window frame, title ribbon, button plates, currency medallions. Include scalable nine-slice borders, shared theme assets, typography and icon rules; test in a debug gallery.
- **M2 — Single screen vertical slice:** redesign **Battle Menu** first, because it simultaneously tests full-screen frame, menu cards, original artwork, button family, navigation and global HUD. If current owner-approved menu mockup already exists and is better, evaluate before implementing. Then obtain real Unity screenshot and critique.
- **M3 — Responsive screen family:** Battle Menu → Siege list → Arena → Rankings → Daily Quests. Reuse shared primitives, do not re-author similar components.
- **M4 — Modal and result family:** Arena Result, Siege Result, Daily Gift, Raid Result. Implement overlays and deterministic reward/result binding.
- **M5 — Collection family:** Summoning Gate, Summon Reveal, Summon Results, Hero Roster, Hero Detail, evolution stars. Prioritize real hero art, rare animation, and presentation.
- **M6 — Social and build family:** Guild Browse/Detail, Build Catalog, building actions, and remaining secondary panels. Refresh existing approved HUD gently, not a blind overhaul.
- **M7 — Device/visual QA and polish:** all aspect ratios, long strings, minimum text, safe areas, selected/pressed/disabled/loading states, mobile performance, audio, motion preferences, and package notices.

Order may change after code audit, but **at least one representative screen must be truly excellent before cloning the new system everywhere.**

---

# SECTION 03 — WHY THE CURRENT SCREENSHOT SET LOOKS LIKE AI SLOP (ACTIONABLE AUDIT)

The two owner-supplied contact sheets show:
- **Village Siege**: a thin top bar and a nearly full-screen dark navy panel; minuscule descriptive text; dozens of same-width plain bars; locked stages visually dead; rewards coded as inline prose rather than designed objects; huge negative space at the bottom. This reads like a debug selection panel.
- **Arena overview**: a massive empty navy rectangle with only `Fight` and `Rankings` strips at the top, no arena identity, no featured hero, poor contextual clarity and no delight.
- **Arena defeat**: a tiny defeat panel on the left with the rest of the game world totally blank. Modal is in the wrong visual scale; rank/points do not have enough emphasis; stars are tiny dark outlines.
- **Rankings**: leaderboard displays plain text rows on a nearly empty panel. Rank badges, portraits, featured hero identity, row hierarchy, loading/empty states, and player-self sticky identity are absent or poor.
- **Daily Quests**: seven thin bars squeezed near the top, little progress visualization, no illustrated quest icons/rewards, gigantic blank bottom half; no clear `Claim` distinction.
- **Daily Gift**: looks like a plain navy pop-up with one button; no tactile physical 'gift' object, insufficient emphasis on the actual three reward currencies, no opening moment.
- **Summon Reveal and Summon Results**: a tiny mannequin-like hero inside a miniature purple card, in a vast empty background. Rarity typography dominates character identity; no collectible fantasy.
- **Guild Browse**: a dull stacked list of generated short names without emblem identity or social atmosphere. Little differentiation between rows; no useful discovery filters.
- **Guild Detail/Chat**: a half-empty blank scene with a tiny right-aligned chat form. The guild identity is entirely missing.
- **Siege Result**: a tiny panel and large empty village; feedback too quiet to feel like a victory and too small to read clearly.
- **Battle Menu**: five tall text-only columns with generic colored backgrounds and tiny action buttons; no real representative battle-mode imagery, no differentiation or premium composition.

This is not fixed by just changing a hex value, adding an ornate gold border to existing rectangles, installing a design skill, or increasing font sizes slightly. **Fix composition + art + component anatomy + feedback + production verification.**

## Diagnostic anti-patterns we are banning

- Huge full-screen colored flat rectangle with 10% used, 90% blank.
- Stock gradient from navy to near-black offered as 'premium fantasy'.
- Ten consecutive identical plain rectangle buttons, each 26px high, no icon or reward hierarchy.
- Body copy that is too tiny to read on a phone.
- All nav items, headings, buttons and rewards use exactly the same type size/weight.
- Random ornate flourishes pasted on every surface.
- Arbitrary ring glow around every selected and unselected object.
- Card without physical borders, material, inlaid portrait, rarity and ownership hierarchy.
- Fake '3D' from a two-color flat shader instead of a distinct original sprite/icon.
- A generated illustration whose subject, currencies or buttons are fictional yet presented as the running game.
- Cute cartoon blob heroes in purple squares when approved hero art exists.
- Generic front-end dashboard conventions: web cards, pill navigation, floating sidebars, dense table hairlines, unthemed sliders, CSS-style cheap drop shadows.
- Rebuilding each screen with new local `Color32` values instead of using shared tokens.
- Hardcoded view references that break at 4:3 or screen notch.
- Placeholder click targets; every visible action must do something real or clearly show `Coming Soon` if existing project does.

---

# SECTION 04 — ART-DIRECTION NORTH STAR AND DESIGN LANGUAGE

**Picture an ornate miniature war table within a shinobi fortress.** The screen UI uses crafted objects: lacquered cedar, dark brass, brushed gold edging, subtle paper fibers, sealed scrolls, carved badges, satin crimson, slate stone and cyan spirit-energy accents. It is polished, warm, game-like, collectible, and visually tangible. But it remains legible and compact enough for mobile.

The interface should feel closer to the genre's old-school chunky fantasy strategy games in **weight, tactility, illustrated hero identity, navigation density and responsiveness**, yet its exact silhouette, ornaments and palettes are original Shinobi Dominion art.

### Art DNA
- One shared light source, upper-left warm edge highlights.
- Brass-gold bevels with shaded sidewalls; high contrast between warm metallic frame and deep navy lacquer interior.
- Crimson ribbon plaques with original angular cut ends; small fabric grain, never a flat solid red block.
- Inner screen content uses low-contrast decorative pattern, not dominant scenic wallpaper behind every line of text.
- Stone/cherry blossom/mountain backgrounds only where a screen benefits, cropped to reinforce its mode identity. For text-heavy screens, the illustrated scenery sits on a side strip or behind a low-opacity mask.
- Icons carry recognizable silhouettes, painted material language, strong size hierarchy and coherent internal negative space.
- Mix **one** elegant display font with **one** readable sans for functional text, not five mismatched fonts.
- Rarity and skill colors should have meaning and be consistent across heroes and summoning. Colors are not the only identifier: also use icon shape and labels.
- Use material depth, not noisy skeuomorphism. Do not bury information under 12 bevel strokes.
- Value hierarchy: hero portrait / screen title / main action → contextual current status → rewards and details → decorative flourishes.

### Theme constraints
- No Naruto insignias or tomoe motifs that resemble distinctive franchise imagery. If an old art pack currently uses a three-swirl emblem, do not proliferate it. Propose a new original faction crest and submit before global replacement. Preserve existing data references during this UI pass.
- No direct reuse or close recreations of Castle Clash frame art, title bars, character sprites or screenshots in shipping game. Reference style traits only.
- Do not introduce wood as an ECONOMY resource because we decorate panels with wood. Gold, Mana, Gems are the primary currencies subject to live definitions.

### Visual asset order of operations
1. Gather existing approved transparent UI textures and buttons, identify duplicates, preserve strongest.
2. Build/reuse **actual authored transparent sprites** for master frame, ribbon, button background, coin/mana/gem icons, back/close medallion, selected/locked overlay, rarity cards, progress bar track/fill, one scene-specific hero/mode illustration.
3. Verify alpha edges and nine-slicing; no white jagged halo; no stretch of ornamental corners.
4. Create prefab gallery / sample scene with all variations, actual runtime fonts and 16:9/4:3/wide screenshot.
5. ONLY THEN integrate into functional screens.
6. Never rasterize the text itself into buttons; text remains dynamic in Unity.

### Design tokens
Use `02_UI_DESIGN_SYSTEM.md` for a proposed token family; reconcile with the existing `docs/ui/UI_TOKENS.md`. Create a versioned `UiTheme` asset, NOT copy-pasted values in scripts. The existing values are approved in parts. A thoughtful 10/10 overhaul is an evolution of the design system, not a cavalier replacement.

---

# SECTION 05 — UI ENGINEERING ARCHITECTURE (UNITY 6 uGUI)

## Existing workflow and migration strategy
The project currently builds its runtime UI from scripts. Keep those entry points functional, but extract the *visual templates* into reusable assets/prefabs as appropriate. Prefer consistent `PanelView`, `ShowScreen`, centralized button builder and sprite importer over dozens of one-off cases. The migration may be incremental rather than forcing a scene/prefab rewrite.

### Proposed reusable layers

**Layer A: UI theme data**
- `ShinobiUiTheme` ScriptableObject with serialized sprite references for frames, ribbons, nine-slice tabs, gold/navy/crimson button plates, medallions, modal overlays, progress track/fill, rarity backs, icons and backgrounds.
- Color tokens, type styles, border thickness, padding, animation duration, safe-area mode, shadow intensity.
- Don't use magic hex literals in twenty screens. Allow per-section accent override with a *restricted* dictionary of theme roles.

**Layer B: small components**
- `SdWindowFrame`: title ribbon, patterned body, safe padding, close/back medallion, optional world overlay.
- `SdButton`: one-click handler, icon, label, price, normal/pressed/disabled/loading/locked, optional cooldown.
- `SdCurrencyCounter`: icon, value, label tooltip, optional plus handler, tabular number update.
- `SdTabBar`: selected and inactive states; screen-reader equivalent label if supported.
- `SdHeroCard`: portrait/rarity/name/level/class/element/count and owned/locked state.
- `SdRewardChip`: one icon, amount, category, optional first-clear emphasis.
- `SdQuestRow`: icon, title, progress, reward group, `Go` vs `Claim` state.
- `SdStageRow`: chapter number, status, stars, rewards, locked reason, action.
- `SdRankingRow`: rank badge, avatar, guild/crest, player, points, featured hero portrait.
- `SdModalResult`: title, hero/medal/stars, outcome-specific stats and CTAs.
- `SdIconButton`: back, close, info, menu, unassign, lock, plus.
- `SdDimmer`: consistent dim/blur behind modal; respect world input blocking.
- `SdScrollList`: viewport/clipping, row pooling, smooth inertial drag, visible scroll cues.

All components take their **real application data**, not hardcoded screenshots. Presenters/bindings transform typed view models into UI components. Preserve scene/controller methods as adapters until features are integrated.

**Layer C: screen presenters**
- E.g. `BattleMenuPresenter`, `SiegePresenter`, `ArenaPresenter`, `RankingsPresenter`, `QuestPresenter`, `GiftPresenter`, `SummonPresenter`, `GuildPresenter`, `HeroDetailPresenter`.
- Each presenter has separate `Refresh` / `Bind` methods that accept real data. Screens must not perform network RPCs from `Update()`; existing services provide data events or explicit refresh methods.
- Use testable binding boundaries: screenshot fixture data for Editor only; runtime production uses authoritative live API.

**Layer D: navigation and safe area**
- One screen stack with consistent enter/exit; back returns to prior context.
- Modal blocks world touch; all scroll views properly mask and consume drag input; village pinch/pan must not fire while dragging a list.
- Safe area root respects notches and aspect ratio; area outside can be scenic but must not contain essential controls.

### Rendering implementation rules
- Built-in uGUI `Image` with `Type.Sliced` for suitable rectangular chrome; have correct import slicing borders and pivot.
- TextMeshPro font assets only if available or added with project-compatible licensed fonts; do not silently import entire font packages without checking.
- `CanvasScaler.ScaleWithScreenSize` as existing; test `matchWidthOrHeight` with 16:9, 4:3 and 20:9.
- Avoid stretch distorting circular icons; use fixed aspect and native width where appropriate.
- Pixel-alignment test for thin lines; atlas/texture filtering and compression tuned for target quality.
- Don't over-mask every row; scroll masking should be efficient.
- Reuse global currency/player strip when appropriate; do not duplicate one independently on every screen with slightly different sizes.
- Use one authoritative `UiSprites` source; avoid temporary Resources.Load lookups on each button click.
- Use correct alpha import and premultiplication treatment to avoid glow fringes on transparent hero art.
- Make sure context menus never cover the player's primary tap actions without purposeful design.

### SFX, transition and haptic rules
- All buttons have consistent press audio via existing `GameSounds.cs` service; no three different sound families in similar contexts.
- Optional `UiMotion.cs` for tweens. Prefer built-in code if possible rather than adding a license-complicated library. If PrimeTween is selected, obey its special public-source redistribution restrictions.
- Prevent reentrant duplicate actions: disable one-shot `Open`, `Claim`, `Fight` CTA while request is in flight; show honest loading state; re-enable on failure.
- Timed motion may not alter battle result or other server-authoritative state.

### Data binding and interaction correctness examples
- Siege stage disabled action must show `Locked: hold level 1 first` **from live rule**, not fake placeholder.
- Arena rankings and win/loss counts must be server values; avoid rendering a screenshot's test `Rank #71` as everyone's rank.
- Quest progress and rewards cannot be authored in static PNG. The quest row art is static; values are live.
- Daily gift uses one validated claim; don't present it as available twice by re-opening a modal.
- Hero card in summon reveal must show exact rolled hero ID, tier, existing duplicate count, and correct art. No forced outcome/rarity.
- No fake celebratory victory / stars in a defeat screen.
- Guild list shows actual members/availability; disabled `Join` when full; chat input respects current membership.
- Evolve star indicator: `0→none`, `1→one`, `2→two`, `3→three`, earned only; no ability to click stars to skip levels.

---

# SECTION 06 — SCREEN-SPECIFIC REDESIGNS: IMPLEMENT THESE EXACT INTENTS

Each entry below is a brief for a ***finished Unity screen***. Consult current screen reference images, current route and controller implementation before coding. Preserve text/logic/data that is authoritative to the current game. Screenshot concepts demonstrate aesthetic direction, not source data.

## 06.1 BATTLE MENU — recommended FIRST VERTICAL SLICE

**Current defect:** five tall colored column bars, awkward generic labels/buttons, no real illustrations or mode identities. Looks like five HTML cards stretched to screen height.

**Redesign concept:** a premium illustrated war-planning menu on a handsome navy lacquer war table. Five purposeful **illustrated mode banners/cards** side by side at standard landscape width, each with DIFFERENT real themed original imagery and recognizable silhouette. The cards feel like standing scroll posters or painted lacquer slabs, not colored rectangles with texture filters.

**Must have:**
- Title `BATTLE` framed near upper center, clear back route, global player/resource bar when screen normally provides it.
- Five modes in current game: `Raid`, `Dungeons`, `Village Siege`, `Arena`, `Trial Tower`. Preserve game names and routes; don't add new modes.
- Card art distinct: Raid = stealthy shinobi overlooking a village; Dungeons = cavern/guardian archway; Village Siege = fortified gate with incoming shadows; Arena = dueling courtyard and banners; Trial Tower = moonlit mountain tower.
- Title on a designed tab/plate, short body copy (1–2 lines maximum when possible), progress badge where applicable, and one primary action at the bottom. Action verb adapted to route: `Find a Rival`, `Open the Map`, `Defend`, `Enter`, `Climb`.
- Cards use original subtle pressed/selected states, bordered over different illustrations, with enough dark local backing for type.
- Content is centrally balanced and does not use repeated generic navy boxes.
- At 16:9, five cards must not crush to 100-pixel widths. Use consistent measured widths, a coherent gap and adapt for 4:3 as described below; if five cannot fit, horizontally scroll with visible partial next card/scroll affordance, rather than microscopic text.
- Optional premium touches: subtle torch shimmer in selected mode, floating embers local to Raid, soft waterfall only in Dungeon card; never animate every card at once.

**Reject:** static random colored rectangles; no picture; illegible 10px copy; duplicated card art; five indistinguishable identical buttons; invented currencies or fake counts.

**Acceptance:** Before/after 1600×900 screenshots and at 4:3, all five cards accessible and selectable, all routes work, no cropped CTA, readable at normal phone size.

## 06.2 VILLAGE SIEGE — progression and defense-stage list

**Current defect:** dozens of thin anonymous horizontal bars; text jammed into stage button; huge dark void below; barely distinguishable locked/unlocked.

**Composition:** a strong Siege stage planner with vertical original fortress panorama on the left 20–25% (castle wall, banners, defensive watchfires), main stage list on the right in a framed panel. If aspect ratio is tight, reduce side illustration width; don't shrink row text.

- Title `Village Siege`, visible `X / 30 Stars` from live data.
- Brief game description at top; exact authoritative three-/two-star logic if current game specifies it, not guessed.
- Stage rows: numbered pennant, stage title, secondary mini-description or gating note, three star slots (earned only filled), compact actual reward chips, one clear action on unlocked selected stage.
- Featured current stage visually cream parchment highlighted or warm gold-lined; locked rows darker and their unlock requirement readable.
- Do NOT make the entire 800px row a giant undifferentiated red `Fight` button.
- Info in reward chips must not claim gems if backend only awards first-clear gold/mana. No fake loot.
- Scrollable list with clear item boundaries, sensible row height, optional chapter separation and stage progression path. Empty lower screen eliminated by using a properly sized panel and enough visible content (or intentional illustrated area).

**Acceptance:** can scroll to later stages, row selection and fight action work, lock reasoning correct, star counts current, no reward mismatch, screenshots on phone and tablet.

## 06.3 ARENA OVERVIEW — duel ritual, not empty rectangle

**Current defect:** almost entire frame is blank. One-line description at top; `Fight` and `Rankings` feel like default form controls.

**Composition:** left 55–60% for mode summary, available duels/charges if CURRENT game truly has that mechanic, points/rank and rewards. Right 40–45% for an original, richly painted moonlit duel courtyard with central shrine target. If no charge mechanic exists, show something else real: recent result / featured hero / rank progress; **never invent cooldowns, energy or tickets**.

- `ARENA` banner, live rank and points in compact prestige plaque.
- A small icon infographic explaining actual victory condition (hero charges to opposing shrine) rather than paragraph wall.
- Hero portrait or simple silhouette of actual assigned hero if data exists; not a random legendary illustration.
- Primary large crimson `Fight`; secondary navy/gold-framed `Rankings`—same button kit as other screens.
- If duel uses charges, display charge count and recharge timer from actual code; if not, omit those entirely.
- Provide `No hero assigned` / `Not enough ...` disabled clarity if live game requires it.

**Acceptance:** screen no longer has big void; clear action in first scan; rank and win/loss correct; user can enter Arena and Rankings; visual background looks original and purposeful.

## 06.4 ARENA RESULT — modal, loss OR victory

**Current defect:** tiny modal on far left leaving giant unused village world, barely readable score/rank, vague dark stars, no conclusion impact.

**Composition:** intentional center dialog occupying approx 55–70% of reference landscape width, dimmed world clearly visible behind. Not an overlay the size of a tooltip. Use a title ribbon and a large expressive `VICTORY` or `DEFEAT` state graphic, no false triumph on loss.

- Opponent name in readable position; live score/rank delta and post-result rank.
- If no points change, display `+0` truthfully and explain no reward; do not use hardcoded `−10` merely because art mockup included it.
- Dim/earned stars if this mode actually grades stars. If not, remove stars and use duel result emblem.
- `Fight Again` primary when eligible, `Return to Village` or `Back` secondary; show why fight unavailable.
- Snapshot of actual result data. A result modal must never recompute points or fire reward RPC twice.
- Strong result SFX and restrained entrance animation; defeat is sober, victory celebratory. Background visible but subdued; close button consistent.

**Acceptance:** tested both win and loss, point-change correctness, action routes, modal centering for aspect ratios, no transparent overlays intercept unexpected taps.

## 06.5 RANKINGS — leaderboard worth competing in

**Current defect:** spreadsheet of unstyled strings, no columns, portraits, hierarchy or readable rank identity; far too much repeated tiny text.

**Composition:** broad framed leaderboard; small stylized region banner or original crest on one edge; primary area uses tight readable list table with clear `Rank`, `Player`, `Points`, `Featured Hero` columns. Rows are visually compact but designed.

- Top three use distinctive gold/silver/bronze ranking shields and restrained highlights; don't create oversized podium that hides most rows.
- Real player avatar or approved simple original fallback; featured hero icon uses actual hero art and name; do not invent hero rarities or levels.
- **Current player's rank** anchored in a sticky bar or top-right compact crest; emphasize if visible in list.
- Alternate very subtle row tones or separators; not zebra-striped bright blue cards.
- Scroll performance and network paging; avoid spawning hundreds of rows at once.
- Loading, error, empty, tied rank, missing avatar, long player name, 0 points, #1000 rank states.

**Acceptance:** readable at 1280×720, visual distinction of top three, no data substitution, player rank accurate and sticky during scroll, portrait cells never jump when loaded.

## 06.6 DAILY QUESTS — legible checklist with incentives

**Current defect:** seven tiny monochromatic bars near top and giant unused canvas.

**Composition:** page scroll area with illustrated quest rows in warm parchment wells inside a dark framed window. A modest vertical scenery strip (shrine courtyard, village) is appropriate, but list remains dominant.

- `Daily Quests` header, `0 / 7 claimed today` or actual dynamic number, refresh timing accurately localized or server UTC as current game does.
- Each quest has a signature icon, title, very short action description, progress bar with numbers, 1–3 compact reward chip(s) *from server definitions*, action state.
- States: not started → `Go` (navigates to proper feature); in progress → `Go` (when meaningful); complete and claimable → distinct green/gold `Claim`; already claimed → muted completed check; disabled/unavailable → clear reason.
- Include current named quests: resource collection, troop training, upgrade a building, raids/dungeons, Arena, Village Siege, summon a hero. Do not invent daily tasks.
- The `Go` button may open an existing screen or provide approved contextual explanation; it may NOT trigger fake event completion.

**Acceptance:** quest status update after return, `Claim` only once, all text readable, reward amounts correct, consistent button/sprite styles, no blank expanse or clipped end items.

## 06.7 DAILY GIFT — a tiny event deserving polish

**Current defect:** small bland modal with an `Open` button and a sentence.

**Composition:** centered tactile gift panel with a signature physical wrapped present/chest/scroll in **the game's original style**, and a parchment reward strip. Actual village softly dimmed behind. Compact enough not to monopolize the screen, large enough to invite touch.

- Header `Daily Gift`, actual user eligibility and reward list.
- Large hero prop illustration with soft light behind, not 100 glowing particles.
- Reward chips: gold, mana, gems with true amounts. Important: actual reward eligibility is server-authoritative; do not clone screenshot sample reward values.
- `Open` / `Claim` main action only if available, disabled `Claimed Today` when already done, exact returned payout on success, failure/retry message.
- After opening: a short controlled burst, content resolves into a receipt/claimed state. Close and tap handling do not let it claim twice.

**Acceptance:** works when eligible and already claimed, server reconciliation, no duplicate reward, modal centered on all devices.

## 06.8 SUMMON REVEAL — make collection feel meaningful

**Current defect:** minuscule bald placeholder character in an underdesigned card, dead purple void, effect not tied to rarity/character, name cut or too small.

**Composition:** cinematic but clean full-screen rarity reveal with an actual large *hero portrait or full-body cutout* occupying a meaningful part of screen, or a beautifully illustrated hero card centered over a rarity-specific original aura.

- `Summon Results`; real rarity (`Common`, `Elite`, `Legendary`) with appropriate frame/illumination and clear name.
- Use actual hero artwork loaded by ID; no generic code-generated figures if approved illustration exists. If missing show placeholder clearly labeled for development, not shipped.
- Animate controlled reveal (e.g. card rises/scales, one light arc, title appears) but limit effect durations and allow skip.
- **No pity meter, no guaranteed result language.** Do not alter the independent RNG algorithm, server transaction, or 10-pull semantics.
- Duplicate indicator only after result is confirmed from server; show count correctly.
- Common results should be pleasing without legendary VFX; Elite visibly brighter; Legendary exciting and unique.

**Acceptance:** all rarities tested, identity/rarity correct, transparent art no halo, animation skippable, accessible reduced-motion mode, no false guarantees.

## 06.9 SUMMON RESULTS — confirmation, duplicates and exit

**Current defect:** card appears tiny at top and screen wastes most of its area.

**Composition:** paired with reveal, but calmer: one large preview card center or central results grid for ten-pulls, counter text beneath, primary `Done` at an obvious location. A deep lacquer frame with original tiny painted accents, not a vacuous dark navy plane.

- One-pull: card centered at ~a quarter to third screen height with readable name; existing ownership count.
- Ten-pull: responsive 2×5 card grid or safe scroll alternative, each card has actual tier and identity; no forced rarity slot.
- Include final gem delta from actual server state only, with no fake animation affecting balance.
- If full hero artwork isn't loaded, show graceful owned state icon, not a broken image.
- `Done` always works, no accidental double summon; support preview selection if existing game allows.

**Acceptance:** one- and ten-pull screenshots, correct duplicate count and rarity, all items visible, stable on 4:3, back navigation and ownership counts correct.

## 06.10 GUILDS BROWSE — social fantasy, not strings

**Current defect:** flat list of opaque generated strings and tiny text, no guild personality or sort controls.

**Composition:** strong guild discovery window with original banner imagery and crest per guild if available. A search/filter bar at top and readable rows.

- `Guilds` title, brief purpose, query field, clear search affordance, sort or status filter only if implemented.
- Each guild row: emblem, name, members/max, points, short description or tag, right-aligned `Join`/`Request`/`Full` action as live rules dictate.
- Top recommended/open guilds can have warmer parchment background, rest restrained navy.
- Guild names are dynamic: avoid hand-drawn names in background textures.
- Error, empty results, loading, joined already, full guild, high latency, profanity-filter state based on current project rules.

**Acceptance:** search works, no wrong `Join` action state, rows are readable, membership limits correct, no broken scroll inputs.

## 06.11 GUILD DETAIL & CHAT — give the group a home

**Current defect:** mostly empty panel, tiny chat in a corner, no guild presence.

**Composition:** elegant guild hall window with large crest/banner, guild name, membership/arena total, motto and the main chat module. Suggested layout left 30–35% guild identity, right 65–70% chat on wide landscape; alternate stacked section at 4:3 if required.

- Header real guild name and online member count if actual data exists.
- Tabs `Chat`, `Members` optionally `Info` if implemented. No fake placeholders unless clearly marked.
- Chat timeline with sender portrait/role badge/time, content bubble readable, scroll to latest on initial entry, preserving scroll if user reads history.
- Compose field and send button with disabled empty input; server/network error feedback, no silent loss of message.
- `Leave Guild` separated from chat composer as a secondary/danger action requiring confirmation if existing flow does.

**Acceptance:** long texts, emoji/multilingual text supported by font or gracefully handled, no input hidden by keyboard, send/receive update works, leave cannot be accidentally touched.

## 06.12 VILLAGE SIEGE RESULT — triumphant but truthful

**Current defect:** tiny left-side report dialog, beautiful village space unused and no result weight.

**Composition:** centered victory modal, partial scenery visible around frame. Large `VILLAGE HELD!` or exact game outcome, two/three earned stars based on real damage rules, stage name, concise battle details, rewards if any.

- Village damage % prominently visible.
- Town Hall status `Held intact` or destroyed as returned by battle result.
- Attackers defeated actual X/Y.
- Correct result star count; do not use mockup's two stars and 8% for all players.
- `Back to the Siege` and `Return to Village` large enough for thumbs; distinctive primary choice.
- Reward result must match server and be shown once; no repeat payout by replaying animation.

**Acceptance:** all possible victory/defeat/star statuses, live values, cross-device centering, server receipt integrity, screenshot evidence.

## 06.13 HERO ROSTER AND HERO DETAIL — premium collectible identity

**Current defect, as seen in earlier owner screenshot:** 2D legendary splash art pasted over a test-style grey tiled floor, cramped stats, gigantic mostly empty skill panel, navigation arrows intersect character effect, evolution labels `S B R C` confusing and extra Evo I/II/III buttons unwanted.

**Composition:** a considered hero display with large high-quality character art or genuinely rigged model on original ceremonial platform; no random test grid. Side panels flank hero without crushing, right panel contains all **four** abilities, left panel displays essential stats. Hero portrait center has enough breathing room, clear physical shadow and atmospheric lighting. If using static artwork, don't claim it is 3D; integrate it visually with backdrop instead.

- Hero true name, rarity, role, affinity; no wrong alias like `Wandering Sage` for Solar Beast Sage.
- Left panel: level/XP, HP, Attack, Defense, lock status and evolution readiness.
- Right panel: four skill icons with selected detail, actual unlocked state, concise explanation; never empty 80% parchment rectangle.
- Star evolution indicator **only earned bright cyan-blue stars** (0=none, 1=1, 2=2, 3=3), near nameplate. **No extra Evo sections**.
- Keep original all-three evolution costs and duplicate requirements. `Evolve` button only active when actual server says ready; clear missing requirement explanation otherwise.
- Hero-specific color accent in subtle edge props (Solar warm gold, Thunder cool electric indigo, Stone earth, Lunar silver-blue, Fire crimson, Void dusky violet, Storm cyan). Same shared frame language regardless of hero.
- Character designs must be original and no animal companions.
- Hero gallery supports Common/Elite/Legendary tier hierarchy; elite/common readable and intentionally simpler than legendary.
- Hero card art path should resolve actual approved PNG references in game assets, not procedural blobs.

**Acceptance:** all eight legendary portraits resolve to correct identity, 0–3 star states correct, ability info dynamically changes, no hero overlap at 4:3, evolution functionality preserved.

## 06.14 BUILD CATALOG — improve existing approved pass without regressing it

Existing `docs/ui/CATALOG_SPEC.md` and `HUD_SPEC.md` already define framing. **Audit before changing.** The big goal is real building sprite preview with correct footprint and material value, plus consistent art on tabs/buttons. It opens ONLY through Builder Hall / building context.

- Five functional categories per current code: structures, defense, resources, hero bases, walls. If current approved gameplay has one Hero Base or other restrictions, follow live rules; don't invent a new six-base layout.
- Building cards: large clean original isometric art preview, real name, real cost, footprint, built/max, correct Dojo lock reason; price bar.
- The wall segment preview should match actual right/left orientations and correct footprint; don't swap art when building in different directions.
- Size baseline across buildings must honor tile footprints; do not claim equal-sized diamond bases are correct when one building is 2×2 and another 4×4.
- In comparison with original classic base-builder design, use frame weight and tile readability; keep our own building shapes.
- Upgrade level art can add gold/ornament without changing footprint when the approved art system requires it.

**Acceptance:** all tab interactions and placement preserved, cards don't collide, 4:3 safe, costs/limits correct, transparent cutouts no ground baked incorrectly.

## 06.15 VILLAGE HOME HUD — only polish if approved current state requires it

The latest `docs/DECISIONS.md` ADR-033 specifies the current approved placement. The owner values old mobile base-building interface composition; do not create a web navigation bar.

- Player portrait/name/level in top-left; right-aligned actual currency strips gold/mana/gems following current implementation.
- The only **large bottom action** is Attack; latest owner layout places it bottom-left. Keep its medallion identity and strong weight.
- Approved left-side circular shortcut column as in ADR-033; don't insert Build/Summon/Hero buttons in it.
- Building interaction must reveal compact bottom-right or contextual medallions, not cover half the village with a blank inspector.
- Village remains visible; HUD must not hide the map and scenery; camera safe bounds maintained.
- Interactive sound, subtle collect markers, player progress, no superfluous water/energy resources.
- Reuse existing textures/counters where approved rather than wholesale repainting already accepted pages.

**Acceptance:** home screenshot at three aspect ratios, tap a building, open catalog only from Builder Hall, attack works, camera pinch/pan not blocked, no duplicate action bars.

## 06.16 RAID / DUNGEON / BATTLE / RESULTS (rest of audit)

The supplied images show Battle Menu and some results, but locate every other relevant mode in Unity repo. Align gameplay screens with the UI design system; world stays readable with compact damage/star/rank overlay. Ensure live raid points, loot, star rules, charge bar and hero assignment are correct; do not invent new mechanics. Design readable health/charge bars with nuanced progression; don't obstruct targets. Battle result overlays should use the same framed/modal family, with unique victory/loss colors and true returned values.

---

# SECTION 07 — FULL UI ASSET CATALOG; NO MORE ONE-OFF RANDOM BUTTONS

Create a machine-readable `docs/ui/overhaul/ASSET_INVENTORY.csv` or JSON listing, for every UI sprite:
- semantic ID, source file, native pixel dimensions, alpha status, license/provenance, 9-slice border in pixels if any, intended on-screen sizes, SpriteAtlas affiliation, exact consuming screens, art-review approval status, source of original design.

Required shared assets (do not duplicate if the current production sprite is already good):

**Frame family:** main screen frame, medium dialog frame, compact tooltip, parchment content well, scroll group well, empty state panel, original corner accents, title ribbon, group divider, bottom nameplate.

**Button family:** large Attack medallion, crimson war action, gold affirmative primary, navy secondary, neutral disabled, destructive close/remove, back arrow, selected tab, inactive tab, small chip action, lock overlay, card focus ring, result CTA, compact sortable column header, scroll track.

**Currency and data icon family:** gold coin, mana swirl, purple gem, star full/empty, siege shield, crossed blades, ranked laurel, book/scroll, gift wrapped, guild crest, notification pip, padlock, checkmark, plus, quest, timer, hero icon, element and role glyphs.

**Collection family:** Common frame, Elite frame, Legendary frame, element badges, duplicate counter, evolution earned cyan star, form ribbon, hero art drop shadow, selected skill icon plate, skill locked overlay, summon reveal ring by rarity.

**Mode family:** Raid illustration, Dungeon illustration, Siege gate, Arena courtyard, Trial Tower, Guild heraldic banner, Daily Gift prop, first-clear reward badge, result victory crest, result defeat crest, modal overlay.

**Feedback:** selected glow (small), pressed face, disabled desaturation, parchment shimmer, isolated rarity burst, one optional ability halo, focus ring, notification pip, loading ring and failure hint.

Export source files at sensible high resolution (2× or 4× intended UI size) with transparent alpha, 9-slice-safe edges, same upper-left lighting, preserved source art files if editable. Do NOT stretch raster text from an image. Keep protected edges visually distinct from center fill.

### Asset sourcing and honesty

Before generating anything new, audit `unity/Assets/_Game/Resources/UI`, current art packs and available cutout PNGs. Favor the strongest existing originals. A proper open-licensed base asset may be enhanced; record derivative rights. Do not copy from fandom wikis or commercial game atlas sheets. If asset generation can't be done locally at required quality, add to `ART_REQUIRED.md` with a concrete brief, crop box, palette, resolution, transparency and consuming components. **Never quietly replace missing art with generic solid rectangles and mark the page complete.**

---

# SECTION 08 — REFERENCE IMAGES AND VISUAL COMPARISON METHOD

Two types of references are supplied:

A. `references/current/`: screenshots of our actual current UI. These are the **BEFORE** source and preserve functional labels/screen types; state values are snapshots not permanent truths.

B. `references/redesign_concepts/`: AI-generated mockups previously created for the owner. These show ambitions for material quality, scenic composition, ornate borders, hero card drama and contrast. **Not all mocks are viable UI**: some invent resources, card data, ranks, player levels, tokens, events, or show copyrighted-looking symbols. They are not authority for currency count, game mechanics, art ownership, exact pixel layout or literal text. Take art direction cues and adapt for actual Unity functionality.

C. `docs/ui/target_home_mockup_v2.webp` and current repo screenshots: developer/owner-approved local home-screen baseline.

### First compare before drawing
For every target screen, create a 1-page design specification:
- the screen's **one primary user task**;
- existing screen route and data contract;
- current pain points with screenshot annotations;
- chosen primary focal point;
- broad ASCII layout/wireframe with areas and max text lengths;
- art assets to reuse / missing;
- typography and color state; CTA hierarchy;
- expected hard states; aspect layouts;
- tests and visual gate.

You must show/record comparison that can be objectively judged: actual game screenshot BEFORE and AFTER at same resolution, same test scenario; not composite art alone. Use actual Unity `ScreenCapture.CaptureScreenshot` or repo's existing capture harness, and save paths under `docs/ui/overhaul/screenshots/`.

### Self-review pass
Stand back and inspect the screenshot at ~25% size. Answer:
1. Can a player tell what this screen does in less than two seconds?
2. Is the primary action immediately apparent and unambiguous?
3. Are the three most important values legible without zooming?
4. Does artwork reinforce the game world rather than distract or obscure text?
5. Are there more than three indistinguishable flat colored boxes in a row?
6. Is there an empty region larger than ~30% of useful panel area without intentional art/visual focus?
7. Does this share obvious components with the other already-redesigned screens?
8. Are all actions touch-appropriate and error states honest?
9. Does it look like a polished game screenshot, or like a student web UI mockup?
10. Is the underlying system correctly hooked to real data?

If answer fails, change actual implementation and screenshot again. No self-certification after a mockup.

---

# SECTION 09 — RESPONSIVENESS / VISUAL QA MATRIX

Test at minimum these exact landscape targets in Game View or equivalent simulated device:

- `1280 × 720` (canonical 16:9)
- `1600 × 900` (high-res 16:9)
- `2340 × 1080` (wide phone, ~19.5:9)
- `1600 × 1200` (4:3 tablet)
- Optional `1920 × 1080`, smaller safe-area simulation, and actual target Android/iPhone if accessible.

For each representative screen exercise:
- newly loaded, cached state, returning from child page;
- no money / max money / nine-digit number;
- unavailable / loading / failed network / stale response;
- selected / disabled / hovered where relevant / pressed;
- one short label, longest possible username/guild name, translated string +30% length;
- empty lists, 100+ leaderboard rows, 10-pull grid;
- alpha clipping with hero glow, art fallback if image fails;
- correct OS safe area + left/right corners;
- mouse click / touch drag / scrolling; world camera does not move while UI drag happens;
- all background input blocked when modal is active;
- frame and artwork scaling without aspect distortion;
- no cut-off resource counter, back button, close button or main CTA;
- screenshot at half displayed size for legibility.

## Acceptance metrics (targets, not invented measured results)
- 0 compile errors or UI-related exceptions.
- 0 missing `Sprite`, `Font`, `Image`, `CanvasGroup`, `Button` references in redesigned screen.
- No text overflow, clipping or overlaps on supported device scenarios.
- All tap targets exceed project-adopted minimum physical touch size.
- No incorrect currency/reward/hero labels or fake data outside Editor-specific fixture.
- Input to visual feedback should be immediate (press animation starts on touch).
- Panel open animations end cleanly; transitions not mandatory for functional correctness.
- Compare draw calls, GPU overdraw and memory on available devices; do not state target FPS is achieved without profiling. Optimize for 60fps on intended devices, fail gracefully on lower-end phones.
- Screenshot color contrast under both bright and dark village backdrops.
- Runs with optional Unity-MCP absent. Automated screenshot path should work via existing repo tools or a lightweight Editor script.

---

# SECTION 10 — VISUAL TEST AUTOMATION AND EVIDENCE

Implement a minimally invasive screenshot and QA harness. Check `docs/screenshots`, `ProjectSetup.cs`, `OpenAndPlay.cs`, Unity tests and existing capture commands first, then add only what's missing.

Suggested Editor test harness design:
1. Menu item `Shinobi Dominion/UI/Render QA Screen` or batch-mode `-executeMethod` callable from CLI.
2. Enter or load representative UI scene with deterministic **test data in Editor only** (never override server state in production).
3. Set target resolution, await fonts/sprites/layout and one stable rendered frame.
4. Save screen PNG named `{screen}_{state}_{width}x{height}.png`.
5. Save matching JSON manifest: Unity version, git SHA, asset version, screen state, data fixture, timestamp, device/resolution, captured file hash.
6. Compare against baseline: overlay 50% and optional diff heatmap. Pixel diff isn't enough for subjective aesthetic polish, but catches regressions and clipping.
7. Generate optional contact sheet for all states; annotate manual issues in a Markdown report.
8. Record accessible logs and warnings for tests; separate screenshot observation from whether interactions were manually verified.

**Data safety:** test fixtures must never invoke rewarded live transactions or mutate authoritative server records. Use mocked provider objects in Editor-only tests where practical.

Deliver actual screenshots from Unity if possible. If you cannot run the editor, provide source/code/build tasks and clearly state `SCREENSHOT NOT VERIFIED`, with how the owner can capture it. Do not substitute image-generation concept art and call it real game output.

---

# SECTION 11 — QUALITY AND BUDGET REALITIES

There is no universal plugin that turns a developer-looking uGUI into world-class art in one command. A quality UI requires a curated art pack, faithful nine-slicing, strong typography and many reviewed actual screenshots. Several owner-generated art assets already exist, and procedural placeholder art was the source of visual mismatch. Exploit the available licensed/original art first.

The owner has said they prefer free solutions. Do not buy assets, API credits, image tools or Unity plugins without explicit approval. If professional hand-painted sprites are not available, build an exact art brief for that sprite rather than forcing generic geometry into the final game. A single well-designed frame master can improve all screens more than ten unrelated free UI libraries.

Use only a small vetted toolset if needed:
- Unity-MCP optional (see path/spaces, auth notes in `04_REPOS_AND_TOOLS.md`)
- one UI/UX critique skill (e.g. UI/UX Pro Max) for design thinking; not web code injection
- original existing PNG kit and Kenney CC0 borders where stylistically appropriate
- native Unity uGUI, import sprites and TextMeshPro where licensed/available
- existing `UiMotion.cs` or simple tweens; no mandatory external tween package
- screenshot capture and automated tests.

No copying protected assets. No unresolved dependency licensing in the final package. Record notices.

---

# SECTION 12 — EXECUTION COMMANDS, DOCUMENTS AND REQUIRED END REPORT

On Windows in the Git repo root, use safe PowerShell-compatible commands. Before running optional tools, detect exact installed Unity Editor path, package cache, git branch and .NET executable. Existing core test command (from CLAUDE.md):

```powershell
dotnet test tools/core-tests/ShinobiDominion.Core.Tests.csproj
```

Any Unity batchmode command must target the detected Editor path and the existing `unity/` project directory. Check for workspaces with spaces, shell quoting and headless graphics limitations before running. Don't invent a command that you didn't run successfully.

## Required produced files

Inside `docs/ui/overhaul/` create or update:

- `AUDIT_00.md` — before screenshots, code owners, deficits, preserved gameplay decisions, exact theme status.
- `SCREEN_INVENTORY.md` — each screen with route/data contract and done/in-progress status.
- `DESIGN_SYSTEM_v3.md` — reconciled, versioned tokens and components after first screen approval.
- `ART_ASSET_REGISTER.csv` — every UI sprite, licensed provenance, intended screen, alpha, nine-slice borders, QA status.
- `ART_REQUIRED.md` — blocking missing artwork with precise export brief and affected screens.
- `M1_COMPONENT_GALLERY.png` — **actual Unity screenshot** of reusable buttons/frames/states.
- `BEFORE_AFTER_BATTLE_MENU.png` — paired screenshots, actual game render.
- `M2_ACCEPTANCE.md` — manual review scores, issues and fixes.
- `SCREEN_SHOTS/` or `screenshots/` — screenshots across resolutions.
- `QA_RESULTS.md` — commands, log path, passed/failed tests, known limitations.
- `THIRD_PARTY_NOTICES.md` update when relevant.

## Before starting coding, show me a short audit recap
- Exact Unity and uGUI implementation identified.
- Five highest-impact visual problems backed by screenshot observations.
- Theme/prefab plan and art assets identified for the first Battle Menu slice.
- Explicit conflicts in historical docs that will be left unchanged (especially 6 vs 1 hero and 10 vs 15 building levels).
- Which tools/skills will actually be used and why; installation/credential approvals needed.
- Baseline screenshot and test outcome if run.

Then proceed with M1/M2 **without asking me to approve every trivial reversible action**. Ask only for required art/purchases/credentials/destructive changes or a genuine irreconcilable rule conflict. Work now and show concrete output. If blocked, implement the highest-value safe part and document blockers rather than exiting with a plan.

## Final summary format after each milestone

**Changes made** — relevant exact file list and commit(s).

**Screens truly redesigned** — only screens changed in Unity and screenshot-verified; never count mockups as final.

**Screenshots** — direct actual paths for every verified aspect ratio and state.

**Functionality preserved** — navigation/data/transactions tested.

**Art and license provenance** — approved sources and new requirements.

**Visual review** — frank pass/fail against `03_SCREEN_ACCEPTANCE_MATRIX.md`, at least five concrete observations, unresolved items.

**What remains** — prioritized action list with dependencies.

---

# SECTION 13 — EXPLICIT COMPLETION CONTRACT / WHAT I WILL REJECT

I WILL REJECT A 'COMPLETE' ANSWER THAT CONTAINS:

1. Only a textual summary, no changed Unity files.
2. Images generated outside the project but no integrated UI.
3. Recolored default `Button`/`Image` rectangles with one 3px border.
4. Large empty screens and tiny top-left text.
5. More than two unrelated decorative frame families on one screen.
6. Incorrect player's current gold/mana/gem values, invented paid energy, invented feature flags or misleading results.
7. Hero art that is still a bare mannequin when approved art exists.
8. Summon pity/guarantee implied by UI.
9. Extra Evo buttons instead of earned cyan star indicators.
10. New home navigation that bypasses Builder Hall, Hero Base, Hero Altar or Summoning Gate.
11. In-game sources copied from old Castle Clash, Naruto or proprietary screenshots.
12. A full gameplay rewrite hidden behind a 'UI refactor'.
13. A screen that clips at 20:9, 4:3 or standard 16:9.
14. A frame whose ornate corners stretch with width.
15. No actual Unity screenshots or honesty about their absence.
16. UI content that is unreadable at normal phone distance.
17. 'Just install ten more GitHub repos and everything will look good.'
18. A new feature introduced without gameplay authorization merely because a concept image suggested it.
19. An unsupported claim of 10/10 production quality without owner review.
20. A large untested code dump that breaks building placement, campaign, summoning or the backend.

**SUCCESS CONDITION:** at least one actual playable Unity screen looks unmistakably handcrafted, has the visual weight/character/clarity of a premium classic strategy game while remaining original, is fully data-bound, accepts correct inputs and navigation, reuses a stable set of art assets, passes tests and screenshots, and demonstrates a replicable pattern for the other screens.

**Begin with the read-only audit now. Then make the component gallery and the Battle Menu excellent. Quality first, evidence always.**
