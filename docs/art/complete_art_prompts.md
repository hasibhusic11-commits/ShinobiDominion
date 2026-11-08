# Shinobi Dominion: the complete ChatGPT art pack

Everything the game still needs, in the order to make it. Each part says **what** to make, **how many images**,
the **exact file names**, and the **prompt** to paste. Replace anything in `{curly brackets}`.

Goal: the polish and *feel* of Clash of Clans and Castle Clash: chunky, tall, bright, toy-like, instantly readable
on a phone. Never copy their buildings, characters, symbols, UI or logos, and never use Naruto symbols (no Leaf
spiral, no headband symbols from the show). Everything is an original feudal-Japanese ninja-fortress world.

---

## Part 0: how to work (read once)

1. **One image per message.** ChatGPT keeps quality and framing far better one image at a time than in sheets.
2. **Start every new chat with the matching "rules" block** (Part 1 for buildings and props, Part 8 for heroes,
   Part 11 for UI). If ChatGPT starts drifting (angle, shadow, missing base), open a new chat and paste the rules
   again.
3. **Checkpoints:** for every family, make levels **1, 5 and 10 first**, send them to Claude to check in the game,
   then make the rest. This saves you redoing 10 images.
4. **Transparent PNG** whenever the prompt asks for it. If ChatGPT gives a checkerboard *painted into* the image
   instead of real transparency, say: "The background must be truly transparent (alpha), not a drawn checkerboard."
   If it still cannot, ask for a flat **#00FF00 green** background and no green on the subject.
5. **Delivery:** put each part in its own folder in Downloads with the folder name given, e.g.
   `Downloads\SD_Buildings\dojo_hq\dojo_hq_level_01.png`. Tell Claude the folder; Claude imports, crops, sizes and
   checks everything in the game.
6. **Priority order:** Part 2 (towers, already started), Part 4 (walls), Part 3 (the other buildings), Part 6
   (ground and scenery), Part 7 (battle effects), Part 9 (hero cards), Part 10 (hero sprites), Part 11 and 12 (UI
   and screens), Part 13 (monsters, for the later defence mode).

---

## Part 1: rules block for buildings, walls and props (paste first in each chat)

> You are making game sprites for an original mobile base-building strategy game called **Shinobi Dominion**: a
> feudal-Japanese ninja fortress village, painted in a polished, chunky, colourful mobile-game style, at the same
> quality bar as the best base-building games on the market, but with completely original designs.
>
> Follow every rule below for every image in this chat:
>
> 1. **Camera:** classic 2:1 isometric game view. The camera looks down at 30 degrees and is turned 45 degrees, so
>    a square floor tile appears as a diamond exactly twice as wide as it is tall. Never a front view, top-down
>    view or perspective view: lines that are parallel in the world stay parallel in the image. No lens distortion,
>    no tilt, no dutch angle. The building's front corner points straight down at the viewer.
> 2. **Footprint:** every building stands on a square or rectangular floor footprint given in tiles (for example
>    3x3). Its base fills that diamond exactly, edge to edge: no grass, dirt, rocks or extra floor sticking out
>    beyond the footprint, and none of the base missing or cut off.
> 3. **Framing:** one subject, centred, on a 1024 x 1024 canvas. The front (lowest) corner of the footprint sits
>    at the horizontal centre, about 3% above the bottom edge. Make the footprint diamond as wide as possible
>    (between 55% and 80% of the image width) **while the whole building, including its top, fits inside the
>    image with a small empty margin**. Nothing may touch or be cut by the edges.
> 4. **Background:** truly transparent PNG (real alpha). If impossible, flat pure green #00FF00 with no green on
>    the subject.
> 5. **Light:** soft warm sunlight from the upper left. Left faces lit, right faces in cool shade. A gentle rim
>    light on the right edges so the shape pops from the grass. **No cast shadow on the ground**; the game adds it.
> 6. **Style:** thick dark outlines (2 to 3 px at this size), chunky simplified shapes with slightly exaggerated
>    proportions (big roofs, heavy bases, toy-like), bold saturated colours, crisp clean edges, no blur, no noise,
>    no photo texture. Readable when shown only 80 to 120 px wide on a phone: big shapes and strong contrast, not
>    fine detail.
> 7. **Palette:** dark slate-blue tiled roofs; crimson lacquer wood; red cloth banners; warm gold and brass trim;
>    grey and charcoal stone; ivory paper lanterns with warm glow; and, for magic, glowing cyan-blue or violet
>    crystal. The only emblem allowed is the game's own crest: a **gold three-comma tomoe on red**.
> 8. **Never:** text, letters, numbers, UI, logos, watermarks, signatures, people (unless asked), ground shadows,
>    scenery around the building, or symbols from other games or anime.
> 9. **Levels:** all levels of one building keep the exact same footprint, base size, framing and camera, so the
>    game can swap them in place. Each level must look clearly grander than the one before.
>
> Reply "Ready" and wait.

---

## Part 2: the 10 level tiers (used by every building and the wall)

Paste the matching row into each building prompt as `{tier}`.

| Level | Tier description |
|---|---|
| 1 | Humble start: rough timber, plain grey fieldstone, rope lashings, straw or plain shingle roof, one small red cloth. |
| 2 | Same as 1 but tidier: planed wood, a second banner, a small paper lantern. |
| 3 | Cut-stone base, red lacquered wood, slate-blue tiled roof, bronze corner caps. |
| 4 | As 3, plus carved roof ridges, twin lanterns, a gold tomoe crest on the main banner. |
| 5 | Taller and heavier. Dark dressed stone with gold trim on every edge, crimson banners on both sides. |
| 6 | As 5, plus gold-capped roof tips curling upwards, a brazier with a small flame, gold studs on the base. |
| 7 | Black-lacquered wood and dark granite, heavy gold plating, small glowing cyan crystals set into the structure. |
| 8 | As 7, bigger crystals, flame braziers at the corners, ornamental gold dragons or fins on the roof ridge. |
| 9 | Obsidian and gold, ornate curled roof tips, a floating violet-blue crystal, glowing runes along the base. |
| 10 | The most impressive: everything from 9, larger floating crystal with a soft aura, light sparks, extra gold filigree, banners with gold fringe. Clearly legendary. |

---

## Part 3: every building, 13 families x 10 levels = 130 images

Folder: `Downloads\SD_Buildings\{id}\`. File names: `{id}_level_01.png` to `{id}_level_10.png`.

Prompt template (one per image):

> Draw the **{name}, level {N}**.
>
> Footprint: **{footprint} tiles**. Height: about **{height}**.
> What it is: {description}
> Must read instantly as: {read}.
>
> Level {N} tier: {tier from Part 2}. {extra level notes}
>
> Follow all the rules from the start of this chat: 2:1 isometric at 30 degrees, footprint filled exactly, front
> corner at the bottom centre, whole building inside the image, transparent background, no ground shadow, no text.

Fill the template from this table:

| id | name | footprint | height | description | must read as | extra level notes |
|---|---|---|---|---|---|---|
| `dojo_hq` | Dojo HQ | 5x5 | 3 tiles | The heart of the village: a grand multi-tier pagoda castle keep on a raised stone platform with a front staircase, a big central banner with the tomoe crest, braziers flanking the stairs. The largest and tallest building in the village. | "the main castle" | Each level adds a roof tier or a wider skirt roof: 2 tiers at L1, 3 at L4, 4 at L7, 5 with a golden spire at L10. |
| `builder_hall` | Builder Hall | 3x3 | 1.8 tiles | A carpenters' workshop: open-fronted timber hall with stacked lumber, crates, a big mallet and saw on the wall, a red awning with the crest, a small crane arm. | "where you build" | The crane and scaffolding grow with level; L7+ gold tools glow. |
| `hero_altar` | Hero Altar | 4x4 | 1.8 tiles | A sacred round stone shrine platform with stairs on the front, three tall banner poles at the back, a circular summoning floor with carved rings. Heroes are kept here. | "hero shrine" | Rings on the floor glow brighter each level; L7+ floating crystals above the banner poles. |
| `summoning_gate` | Summoning Gate | 4x4 | 2.5 tiles | A massive red torii-style gate on a stone platform, with a swirling blue-violet portal filling the space between the posts, stone lanterns at the front corners. | "magic portal" | The portal swirl grows and gains stars and lightning; L10 gold-plated gate with a floating crest. |
| `training_grounds` | Training Hall | 4x3 | 1.8 tiles | A ninja training hall: a red lacquered dojo building at the back, an open yard at the front with straw training dummies, an archery target, a weapon rack with spears and swords. | "where troops train" | More dummies and weapons with level; L7+ glowing practice targets. |
| `troop_camp` | Troop Camp | 3x3 | 1.5 tiles | A field camp: a big red war tent with the crest, a ring of spears and banner poles, a small campfire, weapon stands. Where trained troops wait. | "army camp" | L5+ a second tent layer and gold tent poles; L9+ glowing campfire. |
| `research_hut` | Research Hut | 3x3 | 1.8 tiles | A scholar's observatory hut: a small tiled building topped by a brass armillary sphere, scroll shelves visible through an open front, ink pots and books on a table outside. | "research and knowledge" | The sphere gets bigger and glows; L7+ floating scrolls. |
| `defense_tower` | Defense Tower | 2x2 | 3 tiles | A tall ninja archer watchtower: square stone base, tall tapering body with arrow slits, open fighting platform on top under a small pagoda roof, a big mounted repeating crossbow pointing to the lower right. | "defence, shoots arrows" | **Levels 1, 5 and 10 are already approved in style**; keep the same design for 2 to 4 and 6 to 9. |
| `spirit_turret` | Spirit Turret | 2x2 | 2.5 tiles | A magic defence: a round stone dais, four short carved pillars holding a ring of brass, and inside the ring a big glowing spirit orb of violet-blue energy, with paper talismans hanging from the ring. | "magic defence", clearly different from the tower | The orb grows and brightens; L7+ crystal shards orbit it. |
| `gold_mine` | Gold Mine | 3x3 | 1.5 tiles | A mine entrance cut into a rocky mound, a timber-framed tunnel mouth glowing gold inside, a mine cart full of gold nuggets on rails at the front, torches either side. | "makes gold" | More gold in the cart and on the rocks; L7+ gold veins glow in the rock. |
| `gold_vault` | Gold Vault | 3x3 | 1.8 tiles | A fortified treasury: a heavy stone-and-timber storehouse with an iron-banded door, a tiled roof, gold coins visibly piled in open chests and barrels at the sides. | "stores gold" | Piles grow with level; L7+ gold-plated roof. |
| `crystal_mine` | Crystal Mine | 3x3 | 1.5 tiles | A cluster of large glowing violet crystals bursting out of a stone outcrop, a small wooden pick-axe station and a cart of crystal shards. | "makes crystals" | More and bigger crystals each level; L10 a huge central crystal with an aura. |
| `hero_base` | Hero Base | 2x2 | 0.8 tiles plus banners | A low round stone pedestal where one hero stands guard, with three short banner poles at the back. **The top of the pedestal must be flat, open and empty**: the game stands the hero on it. | "a hero stands here" | Rings and trim glow brighter; never put anything on the top surface. |

---

## Part 4: walls, 3 pieces x 10 levels = 30 images

Folder: `Downloads\SD_Walls\`. Files: `wall_post_level_01.png` to `_10`, `wall_segment_right_level_01.png` to
`_10`, `wall_segment_left_level_01.png` to `_10`.

The game builds walls from separate pieces placed one tile at a time, so every run, corner, end and junction joins
up. Paste this framing once, after the Part 1 rules:

> For the wall pieces, use this framing instead of rule 3.
>
> The image is 1024 x 1024. Imagine a floor grid of diamond tiles, each diamond **600 px wide and 300 px tall**
> (2:1). Stepping from one tile's centre to the next tile's centre moves **300 px sideways and 150 px up**. The
> tiles themselves are not drawn.
>
> - **Post:** a square pillar standing on the centre of one tile. Its base is **60% of a tile** wide (a 360 x 180 px
>   diamond), with the base's front corner at the horizontal centre of the image, 120 px above the bottom edge.
>   The stone body is about **400 px tall**, topped with a cap.
> - **Segment going up-right:** a straight wall **exactly one tile long**. The middle of its base at the
>   front-left end is at pixel (362, 862), and at the back-right end at pixel (662, 712): it runs exactly 300 px
>   right and 150 px up. Thickness **40% of a tile**; height about **330 px**, a little lower than the post. Both
>   ends are cut flat and plain, because a post stands over each end.
> - **Segment going up-left:** the mirror image: front-right end at (662, 862), back-left end at (362, 712).
>
> All levels use exactly these positions and sizes.

Per-piece prompt:

> Draw the **wall {post / segment going up-right / segment going up-left}, level {N}**, using the wall framing.
> It is the outer wall of a ninja fortress: thick chunky blocks, very readable from far away, with strong contrast
> between the stone and the trim, so a long wall run reads as one bold line around the base.
> Level {N}: {wall tier}. The post matches the segments of the same level and is the tallest piece, with a
> decorated cap: a plain stone cap at levels 1 to 4, a gold spike at 5 and up, and a small glowing crystal at 7
> and up. Transparent background, no ground shadow, no text.

Wall tiers:

| Level | Wall look |
|---|---|
| 1 | Sharpened timber palisade logs lashed with rope on a low fieldstone footing. |
| 2 | Thicker logs with iron bands. |
| 3 | Grey cut-stone blocks with a small slate-blue tiled cap. |
| 4 | As 3, with a red lacquered rail under the cap. |
| 5 | Large dressed stone, gold-capped tops, red rail. |
| 6 | As 5, plus gold corner plates and small lanterns on the posts. |
| 7 | Dark granite with heavy gold plates and glowing cyan crystal studs. |
| 8 | As 7, bigger studs, gold dragon-scale pattern along the top. |
| 9 | Obsidian blocks with gold borders and glowing violet runes along the top. |
| 10 | As 9, brighter runes, floating sparks, gold filigree on the posts. |

---

## Part 5: building states, 5 sizes x 2 = 10 images

Folder: `Downloads\SD_States\`. These are shown while a building is being upgraded or after it is destroyed in a
raid. Make one for each footprint size used in the game: **2x2, 3x3, 4x3, 4x4, 5x5**.

**Construction** (`construction_{size}.png`, e.g. `construction_3x3.png`):

> Draw a **construction site, footprint {size} tiles**: wooden scaffolding and bamboo poles around a half-built
> stone base, a pulley with a hanging beam, stacked planks and stone blocks, a hammer and a saw, a small red
> banner with the crest. Height about 1.5 tiles. Follow the building rules (2:1 isometric, footprint filled,
> transparent background, no ground shadow, no text).

**Rubble** (`rubble_{size}.png`):

> Draw **the ruins of a destroyed building, footprint {size} tiles**: a low pile of broken stone blocks, snapped
> timber beams, scattered roof tiles, a torn red banner, a few small embers and a wisp of smoke. Low: no taller
> than half a tile. Same rules.

---

## Part 6: ground, scenery and the map border

### 6a. Ground textures, 8 images (seamless, top-down)

Folder: `Downloads\SD_Ground\`. These are **not isometric**: they are flat textures the game lays on the ground.

> Paint a **seamless, tileable, top-down game texture, 1024 x 1024**, of **{material}**. Hand-painted mobile-game
> style matching a bright, colourful fantasy village: soft painted detail, no photo texture, no strong single light
> direction, no large features that would repeat obviously. **The left edge must continue into the right edge and
> the top edge into the bottom edge with no visible seam.** No objects, no shadows, no text.

| File | {material} |
|---|---|
| `ground_grass.png` | lush short green grass with soft light and dark patches |
| `ground_grass_flowers.png` | the same grass with scattered tiny white, pink and yellow flowers |
| `ground_dirt_path.png` | warm packed-earth path with small pebbles |
| `ground_base_floor.png` | sandy-brown packed earth for inside the village walls, slightly lighter than the path |
| `ground_stone_paving.png` | grey irregular stone paving slabs with thin moss in the cracks |
| `ground_sand_shore.png` | pale riverbank sand with small shells and pebbles |
| `ground_cliff_rock.png` | grey-brown layered cliff rock face |
| `ground_water.png` | clear teal river water with soft ripples and light sparkles |

### 6b. Scenery props, 22 images (isometric, transparent)

Folder: `Downloads\SD_Props\`. Use the Part 1 rules, except props have **no footprint diamond**: they stand on
the ground point at the bottom centre of the image.

> Draw a **{prop}** as an isometric game prop (2:1 view, 30 degrees down, 45 degrees turned), standing on the
> bottom centre of a 1024 x 1024 transparent image, the whole prop inside the image. Same style, light and outline
> rules as the buildings. No ground, no ground shadow, no text.

| File | {prop} |
|---|---|
| `tree_pine_1.png`, `tree_pine_2.png`, `tree_pine_3.png` | Japanese black pine tree, chunky stylised cloud-shaped foliage; three different shapes and sizes (small, medium, large) |
| `tree_blossom_1.png`, `tree_blossom_2.png`, `tree_blossom_3.png` | Cherry blossom tree full of pink blossom, a few falling petals; small, medium, large |
| `tree_maple_red.png` | Japanese maple with bright red-orange leaves |
| `bamboo_cluster.png` | A tall cluster of green bamboo stalks |
| `bush_1.png`, `bush_2.png` | Round leafy bush; one plain green, one with small flowers |
| `rock_1.png`, `rock_2.png`, `rock_3.png` | Mossy grey boulder; small, medium, large |
| `stone_lantern.png` | Traditional stone lantern (toro) with a warm glowing light inside |
| `fox_statue.png` | Stone fox guardian statue on a plinth, wearing a small red bib (original design) |
| `small_shrine.png` | Tiny wayside shrine with a red roof and offering bowl |
| `wooden_bridge.png` | Short arched red wooden footbridge, running up-right, for crossing a stream |
| `reeds.png` | Clump of riverside reeds and grass |
| `waterfall.png` | A small cliff waterfall with white foam at the bottom, for the map edge |
| `cliff_edge_right.png`, `cliff_edge_left.png` | A long piece of rocky cliff edge dropping away from a grass top, running up-right (and the mirror running up-left), for framing the edge of the island |
| `cloud_1.png` | A soft fluffy stylised cloud, seen slightly from above, for drifting over the map edge |

---

## Part 7: battle effects and markers, 18 images

Folder: `Downloads\SD_Effects\`. Transparent PNG, 512 x 512, subject centred, same painted style, bright and
readable, no text.

| File | Prompt subject |
|---|---|
| `arrow.png` | A single glowing ninja crossbow bolt, pointing to the right, with a short light trail |
| `spirit_bolt.png` | A ball of violet-blue spirit energy with a comet tail, pointing right |
| `kunai.png` | A spinning kunai with motion lines, pointing right |
| `fireball.png` | A round fireball with an orange-red tail, pointing right |
| `lightning_bolt.png` | A vertical jagged cyan lightning strike |
| `hit_spark.png` | A bright white-gold impact star burst |
| `hit_magic.png` | A violet-blue magic impact burst with small runes |
| `explosion.png` | A cartoon explosion puff, orange core and grey smoke ring |
| `smoke_puff.png` | A soft grey-white smoke puff |
| `dust_cloud.png` | A low brown dust cloud for footsteps and landings |
| `heal_glow.png` | A green-gold rising heal sparkle column |
| `shield_bubble.png` | A translucent blue-gold dome shield, seen from the 30 degree camera |
| `level_up_burst.png` | A golden ring burst with rays and stars, for upgrades finishing |
| `collect_coin.png` | A single shiny gold coin with the tomoe crest, slightly tilted |
| `collect_crystal.png` | A single glowing violet crystal shard |
| `collect_bubble_gold.png` | A round speech-bubble-style marker with a gold coin inside, for "ready to collect" over a mine |
| `collect_bubble_crystal.png` | The same marker with a crystal inside |
| `target_ring.png` | A flat circular target ring on the ground seen from the 30 degree camera, red-gold, for showing a tower's range |

---

## Part 8: rules block for heroes (paste first in each hero chat)

> You are designing heroes for **Shinobi Dominion**, an original mobile strategy game set in a feudal-Japanese
> ninja world. Art style: polished, colourful, high-quality mobile-game character art with **chunky stylised
> proportions** (head about one fifth of the body height, large expressive eyes, bold shapes), dynamic poses, thick
> clean outlines, strong rim light, saturated colours, painterly but crisp. Think premium hero-collector game
> quality, with completely original characters.
>
> Rules:
> 1. Every hero is original. No characters, costumes, headbands, eyes or symbols from Naruto or any other anime or
>    game. No real people.
> 2. Each hero's **colour scheme, silhouette and weapon must be unique** and readable at small size.
> 3. Their **affinity** shows in effects and colour: Fire (orange-red flames), Water (teal flowing water), Earth
>    (brown-gold stone), Lightning (cyan-white electricity), Storm (grey-blue wind and feathers), Lunar
>    (silver-violet moonlight), Solar (gold-white sunlight), Void (black-purple rifts), Steel (silver metal and
>    chains).
> 4. Their **rarity** shows in how elaborate they are: **Common** (simple clothes, one weapon, plain colours),
>    **Elite** (armour pieces, a signature prop, one glowing accent), **Legendary** (ornate costume, flowing
>    elements, aura, a truly memorable design).
> 5. No text, numbers, logos or UI in any image.
>
> Reply "Ready".

---

## Part 9: hero cards, 30 heroes + legendary evolutions

Folder: `Downloads\SD_Heroes\{hero_id}\`.

### 9a. Card art (every hero): `{hero_id}_card.png`

> Draw the **card art for {name}**, a {rarity} {role} with the {affinity} affinity.
> Concept: {concept}.
> Composition: **portrait 3:4 (768 x 1024)**, full body or knees-up, in a dynamic action pose that shows their
> role, facing three-quarters to the right, centred, the head in the upper third. Background: a simple
> atmospheric backdrop in their affinity colours (a sky, mist, embers or moonlight), darker at the edges so a gold
> card frame can sit on top. Their affinity effect wraps around them. No frame, no text.

### 9b. Legendary evolutions: `{hero_id}_form2_card.png`, `{hero_id}_form3_card.png`

The 8 Legendaries evolve. Form 1 already exists. Make forms 2 and 3 with the same character, face, colours and
silhouette, each more powerful:

> Draw **{name}, evolution form {2 or 3}**, the same character as form 1 (attach the form 1 image). Keep the face,
> hair, colour scheme and weapon type identical so they are instantly recognisable. Form 2: upgraded armour, more
> gold, a stronger affinity aura. Form 3: their ultimate form: transformed costume, glowing markings, wings or a
> spirit manifestation fitting the affinity, the most impressive version. Same 3:4 card composition.

### Hero list (concepts to paste as `{concept}`)

**Legendary**

| id | name | role, affinity | concept |
|---|---|---|---|
| `solar_beast_sage` | Solar Beast Sage | Fighter, Solar | A towering monk-warrior with a golden lion-spirit mane of sunlight, open-palm strikes that blaze, prayer beads, white and gold robes. |
| `thunder_empress` | Thunder Empress | Control, Lightning | A regal empress in layered white and indigo robes, a crown of lightning, a fan that casts chain lightning, floating storm drums. |
| `stone_titan` | Stone Titan | Tank, Earth | A huge armoured giant with a body of carved rock and gold seams, a mountain-shaped shield, moss and crystals on the shoulders. |
| `moonfang_oracle` | Moonfang Oracle | Support, Lunar | A serene seer with a silver wolf spirit companion, a crescent-moon staff, flowing silver-violet robes and moon-phase charms. |
| `crimson_eye_prodigy` | Crimson Eye Prodigy | Assassin, Fire | A young swordsman with a blindfold glowing red, twin short blades, a tattered red scarf, mirror shards floating around him (original eyes, no anime eye patterns). |
| `void_sovereign` | Void Sovereign | Mage, Void | A dark monarch in black and purple armour with a cape of rift-space full of stars, a staff that opens black-purple portals, shards orbiting. |
| `ashen_flame_lord` | Ashen Flame Lord | Mage, Fire | A fire sorcerer in charred black and orange armour, a mask of glowing embers, a burning censer staff, ash falling around him. |
| `storm_crow_shinobi` | Storm Crow Shinobi | Assassin, Storm | A masked ninja with a feathered cloak of storm crows, talon claws, crows bursting from the cloak, grey-blue wind swirling. |

**Elite**

| id | name | role, affinity | concept |
|---|---|---|---|
| `lantern_warden` | Lantern Warden | Tank, Steel | A heavy guard in iron armour carrying a giant glowing paper lantern as a shield. |
| `scarlet_fan_dancer` | Scarlet Fan Dancer | Control, Fire | A graceful dancer with twin red war fans that throw flame gusts. |
| `twin_chain_reaper` | Twin Chain Reaper | Assassin, Steel | A hooded fighter with two chained sickles spinning around her. |
| `frost_lake_monk` | Frost Lake Monk | Support, Water | A calm monk standing on a disc of still water, ice crystals in his beard. |
| `boulder_fist_elder` | Boulder Fist Elder | Fighter, Earth | An old master with huge stone gauntlets, cracking the ground. |
| `mask_carver` | Mask Carver | Control, Void | A mysterious artisan surrounded by floating carved masks with purple eyes. |
| `thunder_drum_monk` | Thunder Drum Monk | Control, Lightning | A muscular monk carrying a ring of war drums crackling with lightning. |
| `salt_wind_nomad` | Salt Wind Nomad | Mage, Storm | A desert wanderer with a wide straw hat and a staff that whips up sand winds. |
| `silver_moon_archer` | Silver Moon Archer | Mage, Lunar | An elegant archer with a crescent longbow firing moonlight arrows. |
| `cinder_shield_captain` | Cinder Shield Captain | Tank, Fire | A captain with a tower shield glowing like hot iron, a red plumed helmet. |

**Common**

| id | name | role, affinity | concept |
|---|---|---|---|
| `river_scout` | River Scout | Assassin, Water | A light scout in teal with a short blade and a water-skin. |
| `ember_recruit` | Ember Recruit | Fighter, Fire | A young recruit with a glowing fist wrap and a training gi. |
| `reed_archer` | Reed Archer | Mage, Storm | A marsh archer with a reed bow and wind-blown hair. |
| `stone_apprentice` | Stone Apprentice | Tank, Earth | A stocky apprentice with a stone shield. |
| `talisman_novice` | Talisman Novice | Support, Lunar | A shrine novice throwing paper talismans. |
| `bell_guard` | Bell Guard | Tank, Steel | A gate guard with a large bronze alarm bell on his back. |
| `mist_courier` | Mist Courier | Assassin, Water | A messenger wrapped in mist, a scroll tube on her back. |
| `spark_tinkerer` | Spark Tinkerer | Control, Lightning | A goggled inventor with a sparking wire contraption. |
| `ash_monk` | Ash Monk | Fighter, Fire | A grey-robed monk with a long staff trailing embers. |
| `moth_herbalist` | Moth Herbalist | Support, Lunar | A herbalist with a basket of herbs and glowing moths. |
| `gale_runner` | Gale Runner | Assassin, Storm | A fast runner with wind ribbons trailing from her ankles. |
| `dusk_lookout` | Dusk Lookout | Mage, Void | A watchman in dark violet throwing shadow knives. |

### 9c. Skill icons: `{hero_id}_skill_{n}.png`, 512 x 512

> Draw a **square skill icon** for the skill **"{skill name}"** of {name} ({affinity}). One bold symbolic image
> that shows what the skill does, filling the square, on a background in the affinity colour with a soft
> vignette. No frame, no text. It must read clearly at 64 px.

Skills to make (n = order shown):

- Solar Beast Sage: Sun Palm, Beast Stride, Radiant Roar, Celestial Guardian
- Thunder Empress: Arc Lash, Chain Stun, Storm Court, Sky Decree
- Stone Titan: Stone Guard, Bulwark, Fault Line, Mountain Heart
- Moonfang Oracle: Moon Salve, Clear Sight, Silver Ward, Tide of Night
- Crimson Eye Prodigy: Blind Cut, Glass Sight, Mirror Step, Hall of Glass
- Void Sovereign: Dark Bolt, Rift Step, Shard Ring, Open Door
- Ashen Flame Lord: Ember Blow, Smoulder, Pyre March, Field of Ash
- Storm Crow Shinobi: Talon Cut, Crow Call, Storm Dash, Thousand Feathers
- Elites (one each): Lantern Guard, Fan Gale, Reaping Chain, Still Water, Mountain Breaker, Borrowed Face, Drum of
  Storms, Dry Gale, Moon Arrow, Red Wall
- Commons (one each): Quick Cut, Burning Jab, Wind Shot, Hold Fast, Paper Salve, Alarm Bell, Slip Past, Snap Wire,
  Sweeping Blow, Moth Dust, Tailwind, Thrown Shadow

---

## Part 10: heroes and troops in the village and in battle

These are the small figures that walk, guard the Hero Base and fight. They are seen from the same 30 degree game
camera as the buildings.

Folder: `Downloads\SD_Sprites\{id}\`.

For each **hero** (start with the 8 Legendaries) and each **troop** below, make **5 images**, all the same
character, same scale, same framing:

> Draw **{name}** as an **in-game unit sprite** for the 2:1 isometric camera (looking down 30 degrees). Chibi
> proportions (head about one third of the body height), the same design and colours as the card art (attach it).
> 1024 x 1024 transparent PNG, the feet on the bottom centre of the image about 8% above the bottom edge, the
> whole figure about 60% of the image height. Thick outline, bright colours, readable at 60 px tall. No ground
> shadow, no text.
> Pose: **{pose}**.

| File | {pose} |
|---|---|
| `{id}_idle.png` | standing ready, facing three-quarters towards the lower right, weapon held |
| `{id}_walk_a.png` | mid-stride walking towards the lower right, left foot forward |
| `{id}_walk_b.png` | mid-stride walking towards the lower right, right foot forward |
| `{id}_attack_windup.png` | winding up their attack, towards the lower right |
| `{id}_attack_strike.png` | the moment of the hit or the shot, with a small affinity effect |

**Troops** (each with the same 5 poses):

| id | name | concept |
|---|---|---|
| `militia` | Village Militia | A farmer-soldier with a bamboo spear and a straw hat, simple brown clothes. |
| `kunai_thrower` | Kunai Thrower | A slim ninja in dark blue with a belt of kunai, throwing one. |
| `shadow_runner` | Shadow Runner | A fast black-clad ninja with a short sword and a long trailing scarf. |
| `shield_bearer` | Shield Bearer | A stocky soldier with a huge round red-and-gold shield and a short spear. |
| `iron_ox_rider` | Iron Ox Rider | A rider on an armoured ox, a big war hammer, heavy and slow. |

Also make one **portrait icon** per troop, `{id}_icon.png`, 512 x 512: the troop's head and shoulders, facing
right, on a plain circle of colour, no frame, no text.

---

## Part 11: rules block for UI art (paste first in each UI chat)

> You are making UI art for **Shinobi Dominion**, an original mobile strategy game. The UI style is already set:
> **deep navy panels, heavy polished gold frames with small red gem corners, crimson accents, ivory text areas**,
> chunky and glossy, with thick dark outlines and a soft drop shadow. Every piece is original and contains no text,
> numbers or letters (the game adds all text). Transparent PNG unless said otherwise. Reply "Ready".

---

## Part 12: UI pieces and screens

Folder: `Downloads\SD_UI\`.

### 12a. Icons and rewards, 512 x 512 each, transparent, centred

| File | Subject |
|---|---|
| `chest_wood_closed.png`, `chest_wood_open.png` | A wooden treasure chest with iron bands and a gold lock; closed, then open with gold light and coins spilling |
| `chest_gold_closed.png`, `chest_gold_open.png` | A gold-plated chest with red gems; closed, then open glowing |
| `chest_crystal_closed.png`, `chest_crystal_open.png` | A violet crystal chest; closed, then open with crystal light |
| `gift_box.png` | A red gift box with a gold ribbon and bow |
| `gold_pile_small.png`, `gold_pile_medium.png`, `gold_pile_large.png` | Gold coins with the tomoe crest: a small stack, a heap, a huge overflowing pile |
| `crystal_pile_small.png`, `crystal_pile_medium.png`, `crystal_pile_large.png` | Violet crystals: a few, a cluster, a huge glowing pile |
| `chakra_orb.png` | A purple flame-like spirit orb (the third currency) |
| `star_full.png`, `star_empty.png` | A chunky gold battle star; and the same star empty, dark grey with a gold rim |
| `trophy.png` | A gold trophy cup with the crest |
| `rank_badge_1.png` to `rank_badge_6.png` | Six league badges, shield-shaped, rising: wood, bronze, silver, gold, crystal, legendary (violet-gold with wings) |
| `affinity_fire.png`, `_water.png`, `_earth.png`, `_lightning.png`, `_storm.png`, `_lunar.png`, `_solar.png`, `_void.png`, `_steel.png` | Nine round affinity emblems, one bold symbol each, in their affinity colours |
| `role_fighter.png`, `_tank.png`, `_assassin.png`, `_mage.png`, `_support.png`, `_control.png` | Six small role symbols: crossed fists, shield, dagger, staff, heart-lotus, chain |

### 12b. Frames and banners, transparent

| File | Size | Subject |
|---|---|---|
| `card_frame_common.png` | 768 x 1024 | A hero card frame for Common heroes: plain bronze border, empty transparent centre, a ribbon space at the bottom for the name |
| `card_frame_elite.png` | 768 x 1024 | Silver frame with blue gems |
| `card_frame_legendary.png` | 768 x 1024 | Ornate gold frame with red gems and small wings at the top corners, a soft glow |
| `banner_victory.png` | 1536 x 512 | A huge celebratory gold-and-red ribbon banner with rays behind it, an empty space for the word |
| `banner_defeat.png` | 1536 x 512 | A torn dark grey-blue ribbon banner, empty space for the word |
| `level_up_banner.png` | 1536 x 512 | A gold ribbon with sparkles for "level up" moments, empty space for text |
| `timer_bar_frame.png` | 1024 x 128 | A slim gold-framed progress bar frame for build timers, empty inside |

### 12c. Screens, not transparent

| File | Size | Subject |
|---|---|---|
| `loading_screen.png` | 1920 x 1080 | Key art: a sunset view of a ninja fortress village on a cliff island over clouds, three Legendary heroes in heroic poses in the foreground (Thunder Empress, Stone Titan, Storm Crow Shinobi), cherry blossoms in the wind. Leave the top centre calm for the logo. No text. |
| `summon_background.png` | 1920 x 1080 | The inside of a sacred summoning shrine at night: a giant glowing portal ring in the centre, lanterns, swirling violet-blue energy, empty floor in front for the hero to appear. No text. |
| `hero_screen_background.png` | 1920 x 1080 | A moody shrine courtyard at dusk, softly blurred, darker on the right where a stats panel will sit. No text. |
| `battle_choose_background.png` | 1920 x 1080 | A painted war-map of nearby islands on old paper, with small fortress drawings, for choosing a raid. No text. |
| `app_icon.png` | 1024 x 1024 | The app icon: a bold close-up of a masked ninja's eyes and a gold tomoe crest behind, crimson and gold, very readable at small size, no text. |
| `logo.png` | 2048 x 1024, transparent | The game logo "Shinobi Dominion": chunky carved-gold letters with a red lacquer inner layer, a katana and a tomoe crest worked into it. **This one is the only image allowed to contain text**, and the spelling must be exactly "Shinobi Dominion". |

---

## Part 13: monsters for the defence mode (later)

Folder: `Downloads\SD_Monsters\{id}\`. Same 5 poses and framing as Part 10, plus a `{id}_card.png` (Part 9a
composition). Original yokai-inspired creatures, menacing but readable, in purples, sickly greens and dark reds so
they contrast with the village.

| id | concept |
|---|---|
| `ash_goblin` | Small fast goblin made of ash and embers, carrying a torch. Swarms. |
| `mud_ogre` | Big slow ogre of swamp mud and roots, swinging a log club. |
| `paper_wraith` | A floating ghost made of torn talisman paper, glowing eyes, flies over walls. |
| `stone_boar` | A charging boar with a rock hide, breaks walls. |
| `oni_brute` | A horned red ogre in broken samurai armour with an iron club. Mini-boss. |
| `shadow_spider` | A giant spider made of black smoke with violet eyes, spits webs. |
| `bone_archer` | A skeleton archer in rotted armour, shoots green arrows. |
| `storm_serpent` | A long flying serpent of storm clouds and lightning. Boss. |
| `lantern_ghoul` | A ghoul carrying a cursed blue lantern that heals other monsters. |
| `iron_golem` | A huge rusted iron golem with glowing cracks. Final boss. |

---

## Part 14: checklist

| Part | Images |
|---|---|
| Buildings (13 x 10) | 130 (3 tower images done) |
| Walls | 30 |
| Construction and rubble | 10 |
| Ground textures | 8 |
| Props | 22 |
| Effects | 18 |
| Hero cards | 30 + 16 evolutions |
| Skill icons | 54 |
| Hero sprites | 30 x 5 = 150 |
| Troop sprites and icons | 5 x 6 = 30 |
| UI icons, frames, banners, screens | about 60 |
| Monsters | 10 x 6 = 60 |

Send each checkpoint to Claude before making the rest of that family.
