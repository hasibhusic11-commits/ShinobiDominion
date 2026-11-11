# Screen-by-screen acceptance matrix — Shinobi Dominion

A screenshot is evidence of *appearance*, not proof that buttons or the server work. For each screen collect both visual and functional verification. Scores are human review aid, not independently verified quality claims.

## Required screenshot nomenclature
`{screen}_{state}_{WxH}_{gitshort}.png`

Example: `battle_menu_default_1600x900_ab12cde.png`.

## Visual review score (weight = 10)
- System-wide consistency 0–2
- Visual hierarchy / text legibility 0–2
- Asset quality / material identity 0–1.5
- Contextual composition / background 0–1
- Interaction / feedback 0–1
- Correct real data / gameplay 0–1
- Responsive / safe area 0–1
- Verified performance 0–0.5

Threshold: target 8.5 or better, with all functional-critical constraints passing. `10/10` requires explicit owner approval, not agent self-certification.

| Screen | Primary task | Essential visual feature | Hard states | Gameplay invariant | Pass evidence |
| --- | --- | --- | --- | --- | --- |
| Village Home | inspect and select village | quiet HUD with original large Attack medallion | selection, alert, low currency | building-driven navigation and correct latest HUD | same village three ratios and selected state |
| Selected Building | choose building action | compact gold-rimmed medallions, nameplate | upgrade unaffordable, move, remove forbidden | tapped building valid and actions routed | screenshot + action tests |
| Build Catalog | choose construction | large original illustrated previews, categorized tabs | locked, max built, unaffordable | Builder Hall only; valid footprint | every tab + placement tests |
| Hero Roster | choose hero | card gallery showing actual portraits and rarity | missing art, duplicate, locked | current number of assigned heroes unchanged | cards + selection tests |
| Hero Detail | inspect/level/evolve | large centered character, 4 skills, cyan earned stars | 0/1/2/3 evo, locked, ready | level-200 + duplicate+catalyst unchanged | full evolution state screenshots |
| Summoning Gate | initiate summon | striking ceremonial summoning chamber | not enough gems, busy | independent RNG, no pity | button state + server transaction |
| Summon Reveal | delight in result | large actual hero + rarity-specific burst | Common/Elite/Legendary | exact result ID, no guarantee | 3 screenshots + skip test |
| Summon Results | review results and leave | comfortable results card, duplicates | 1 pull, 10 pull | no reroll/fake rewards | comparison with ledger |
| Battle Menu | select activity | five unique illustrated game-mode cards | active, locked when real, scroll | actual routes and modes only | screenshots 16:9 and 4:3 |
| Raid Prepare | choose deployment | strong hero / troop hierarchy | no hero, low troops | deployment and server authority | screenshot + route test |
| Raid Battle | watch battle | legible combat + charge/health bars | wide/near, win, loss | don't alter A* or rewards | battle sequence screenshots |
| Raid Result | read battle outcome | framed modal, stars/loot | win/loss/zero loot | exact server receipt | screenshot + receipts |
| Dungeons | select stage | mini illustrated map, clear locked stages | first clear, 3 stars | stage gate | page and interaction tests |
| Village Siege | pick wave | illustrated gate plus clear stage list | locked/unlocked, stars | stage unlocking / rewards unchanged | full list + status screenshot |
| Siege Result | inspect defense result | large centered triumph/defeat modal | stars, village damage | report truth | data-bound screenshot |
| Arena | launch duel | duel shrine panorama and large Fight | ready/unavailable, rank | real charge only if implemented | screenshot + Fight action |
| Arena Result | see rank delta | centered victory/defeat modal | both outcomes, unchanged rank | server-provided result | result fixtures |
| Rankings | review standings | medals, avatars, columns and featured hero | ties, zero, 100+ rows | actual ordering | scroll/long text test |
| Daily Quests | act on tasks | quest icons + progress/reward/action rows | not started, claimable, claimed | no duplicate claim | status transitions |
| Daily Gift | collect daily reward | physical illustrated gift + three reward chips | eligible, claimed, network error | server-authoritative once-only | receipt + state screenshot |
| Guilds Browse | find group | crests and filtered readable guild list | full guild, no matches | membership | search/action test |
| Guild Detail | view group/chat | heraldry, chat timeline, members | empty chat, long names, busy | message send and membership | send+scroll tests |

## Every-screen visual checklist

- [ ] The principal task is visually clear within 2 seconds.
- [ ] Screen's actual user data is legible, correctly labeled and not visually fabricated.
- [ ] No unexplained blank lower/right half of screen.
- [ ] No generic copy-paste bars used for every action.
- [ ] Primary CTA differs clearly from secondary CTA.
- [ ] Active, disabled, locked, loading and success states where relevant.
- [ ] Artwork is relevant, original, consistently lit, not pasted randomly behind text.
- [ ] Typography uses consistent display/body roles and enough size.
- [ ] Screens share the same component frames, gold borders and icon language.
- [ ] Sprite corners not stretched when nine-sliced.
- [ ] Text is dynamic Unity text, not baked into PNG.
- [ ] No clipped text in 16:9, 19.5:9, 4:3.
- [ ] Back/close navigates correctly.
- [ ] Input doesn't leak into village world behind a modal.
- [ ] Rewards, currency, rank, hero and unlock labels derive from real state.
- [ ] Optional motion can be skipped or reduced, and does not block interactions.
- [ ] No unlicensed reused art.
- [ ] Compare saved before/after screenshots at same state/resolution.
- [ ] Missing art, editor limitations and untested interactions reported frankly.

## Target device QA

| Reference | Purpose | Required test |
| --- | --- | --- |
| 1280×720 | reference unity Canvas | text, buttons, visual hierarchy |
| 1600×900 | high-res widescreen | pixel framing, art quality |
| 2340×1080 | landscape phone | corners, safe area, spacing |
| 1600×1200 | tablet / 4:3 | adapt dense cards, scroll |
| physical device if present | validate real input | finger tap, GPU, touch + memory |

## Manual feedback instructions for owner

When you share screenshots, evaluate **only the live Unity game** with this verdict style:
- `PASS` — strong enough to adopt as global pattern.
- `REVISE` — layout concept right, but art/readability/spacing needs work.
- `BLOCKED` — missing crucial authored sprite or external issue; don't replace with fake art.

For each visual issue report the *exact region* (e.g. `top-left rank header`), expected result, observed result, reference screenshot, importance, and acceptance test. Avoid generic feedback like “make it better” without local action.

## Regression guard

When the UI overhaul lands, preserve tests for:
- Builder Hall-only build catalog access.
- Current hero assignment rule (repo ADR-030 vs older master prompt conflict, unchanged).
- Independent RNG summoning; exact rarity distribution settings; no pity or guaranteed ten-pull.
- Evolution at 200 with exact duplicate and catalyst; earned blue stars only.
- No energy/stamina, no new economy.
- Store/player owned counts derived from data.
- On-server results and once-only claim/idempotency.
- Building footprint and placement, walls, mouse/touch selection.
- Scene/back stack not broken by modal rework.
