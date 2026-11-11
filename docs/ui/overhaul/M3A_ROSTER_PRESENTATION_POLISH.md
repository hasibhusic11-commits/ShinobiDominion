# M3A — Hero roster presentation consistency

**Status:** source change committed; Unity visual verification outstanding.

## Problem corrected
Before this pass, Hero Detail chose an authored evolution painting when present (with the base painting as fallback), but the Altar roster rendered a procedural figure for every evolved hero. This visually disconnected the same hero across two screens. Roster cards also displayed a separate gold Roman-numeral progression badge while the Hero Detail used the approved cyan earned-star language.

## Implementation
- Every illustrated hero's roster card now calls `HeroConcepts.AvailableForm` and `HeroConcepts.Card` with the selected visual form, just as Hero Detail does.
- A missing Evo I/II/III painting falls back to **that hero's own base painting**, not an unrelated proxy. Missing visual stages remain launch art blockers.
- Roster cards now display one cyan star per **earned** evolution, zero through three; remove the conflicting Roman numeral badge.
- Keep existing rarity frame, level, role, name, selected state, and callback names unchanged. This does not change hero stats, evolution eligibility, summons, server behavior, or economy.
- Added regression coverage to `HeroFlowTests.EvolutionFlow_FromAltar_ConsumesDuplicate_AndKeepsMain`: after evolution, return to Altar and verify the artwork and exactly one earned star.

## Responsive Altar shelves (follow-up)
- Altar shelves adapt from five columns to four, three, two or one according to their actual window width; the fixed five-column arrangement could overlap busts on narrower aspect ratios.
- The scroll height and slot positions now use the same calculated column count.
- A filtered roster with zero matches now displays **No heroes match these filters** rather than a silent empty cabinet.
- Added direct PlayMode-assembly tests for the column-count layout decisions at widths 972, 780, 600, 430 and 300 Unity canvas units. These tests do not replace visual screenshots.

## Acceptance checks (not yet run in Unity)
1. Open Hero Altar at 1280×720, 1600×900, 2340×1080 and 1600×1200; capture Common/Elite/Legendary, locked and assigned cards.
2. Evolve a Legendary through the existing development test flow. Compare the same hero in the Altar and Hero Detail: identity, available form and star count must match.
3. In a controlled art test, temporarily provide a real `{heroId}_evo_1.png`, reimport assets, verify both views use that form; remove the temporary art afterward.
4. Verify cards with no painted concept still use the existing fallback. Inspect level text, badges, stars and frame for clipping, especially the bottom 105 px.
5. Run Unity `HeroFlowTests`, `UiOverhaulTests`, and all EditMode/PlayMode tests before approval.
6. Compare screenshots and test on an actual landscape phone; do not claim visual approval on the basis of these code edits.

## Next presentation milestones
- Hero Altar/Detail screenshot QA, then Battle menu and home HUD polish.
- Build Catalog and Summoning Gate: original, illustrated purchase/action cards with real server-state values.
- Raid preparation, win/loss results, other activity screens: one consistent framed presentation language.
- Replace missing form art, procedural figures, and low-resolution cropped mode imagery with original production illustrations.

**Game identity:** original feudal-shinobi lacquer/brass treatment, tactile classic-mobile-strategy interaction patterns, no extracted Castle Clash assets. Currency bars show Gold, Mana and Gems only; no energy/wood resource.
