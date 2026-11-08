# Shinobi Dominion: ChatGPT prompts for towers and walls

These prompts make the defence towers and walls fit the game exactly. Make one image per prompt, and save every
image as a PNG with the file name given. The game crops, cleans and sizes the images itself, so the important
things are the **camera angle**, the **footprint**, the **framing** and the **same look across all 10 levels**.

The goal is the *feel* of Clash of Clans and Castle Clash: chunky, tall, readable, bright, toy-like
buildings packed into a tight base. Do **not** copy their buildings, characters, symbols or logos. Do not use any
Naruto symbol (no Leaf spiral). Everything must be an original ninja-fortress design.

---

## Part 1: rules for every image (paste this first, once per chat)

> You are making game sprites for an original mobile strategy game called Shinobi Dominion: a feudal-Japanese
> ninja fortress village, painted in a polished, chunky, colourful mobile-game style (the same quality bar as top
> base-building games, but completely original designs).
>
> Follow these rules for every image I ask for in this chat:
>
> 1. **Camera:** classic 2:1 isometric game view. The camera looks down at 30 degrees and is turned 45 degrees, so
>    a square floor tile appears as a diamond exactly twice as wide as it is tall. Never a front view, never a
>    top-down view, and never a perspective view: lines that are parallel in the world stay parallel in the image.
>    No lens distortion.
> 2. **Footprint:** every building stands on a square floor footprint that I give in tiles (for example 2x2). The
>    building's base must fill that diamond exactly, edge to edge: no grass, dirt, rocks or extra floor sticking out
>    beyond the footprint, and no part of the base missing.
> 3. **Framing:** one building, centred. The front (lowest) corner of the footprint diamond sits at the horizontal
>    centre of the image, 4% above the bottom edge. The footprint diamond spans 80% of the image width. The
>    building rises upwards from there; leave a little empty space above the top.
> 4. **Background:** fully transparent PNG. If transparency is not possible, use a flat pure green background
>    (#00FF00) and use no pure green anywhere on the building.
> 5. **Light:** soft sunlight from the upper left. Left faces are lit, right faces are in shade. Warm highlights,
>    cool shadows. Do **not** paint a cast shadow on the ground; the game adds the ground shadow.
> 6. **Style:** thick dark outlines (2 to 3 px at 1024 px size), chunky simplified shapes, bold saturated
>    colours, clear materials (stone, lacquered wood, gold trim, glowing crystal). It must still read clearly when
>    shown only 80 px wide on a phone, so big shapes and strong contrast, not fine detail.
> 7. **Palette:** dark slate-blue tiled roofs, crimson lacquer and red cloth banners, warm gold and brass trim, grey
>    stone, ivory paper lanterns, and glowing cyan-blue or violet crystal for magic. Banners may carry only the
>    game's own three-comma tomoe crest in gold on red. No text, no letters, no numbers, no UI, no logos.
> 8. **Upgrades:** all 10 levels of one building keep the exact same footprint, base size, framing and camera, so
>    they line up perfectly when the game swaps them. Each level must look clearly better than the one before:
>    taller, richer materials, more trim, more glow.
> 9. **Size:** 1024 x 1024 pixels.
>
> Reply "Ready" and wait for my first building.

---

## Part 2: the level tiers (all three buildings use these)

| Levels | Materials and look |
|---|---|
| 1 to 2 | Rough timber and plain grey stone, rope bindings, one small red banner, simple shingle roof. Modest height. |
| 3 to 4 | Cut stone base, lacquered red wood, slate-blue tiled roof, bronze corner caps, paper lantern. |
| 5 to 6 | Taller. Dark stone with gold trim on every edge, crimson banners on both sides, twin lanterns, carved roof ridges. |
| 7 to 8 | Black-lacquered wood and dark granite, heavy gold plating, glowing cyan crystal set into the structure, flame braziers. |
| 9 to 10 | Obsidian and gold, ornate curled roof tips, large floating violet-blue crystal, glowing runes along the base, small energy sparks. Level 10 is the most impressive. |

---

## Part 3: Defense Tower (2x2 tiles), 10 images

File names: `defense_tower_level_01.png` to `defense_tower_level_10.png`

> Draw the **Defense Tower, level {N}** (replace {N} with 1 to 10, and use the tier description for that level from
> the table above).
>
> Footprint: **2x2 tiles**. It is a tall ninja archer watchtower: a square stone base filling the footprint, a
> tall tapering tower body about **3 tiles high** (roughly three times as tall as the footprint is wide on
> screen), and an open fighting platform on top under a small pagoda-style roof with upturned corners. A large
> mounted repeating crossbow (or a ballista with a quiver of glowing arrows) points out of the platform towards
> the lower right. Arrow slits on the tower body. Banners hang from the platform rail on the lit (left) side.
>
> The silhouette must read as "defence" instantly: tall, narrow at the top, heavy at the base, with the weapon
> clearly visible against the sky.
>
> Level {N} tier: {paste the tier text}.
>
> Keep the exact rules from the start of this chat: 2:1 isometric at 30 degrees, footprint filled exactly, front
> corner at the bottom centre 4% up, footprint 80% of the width, transparent background, no ground shadow, no text.

---

## Part 4: Spirit Turret (2x2 tiles), 10 images

File names: `spirit_turret_level_01.png` to `spirit_turret_level_10.png`

> Draw the **Spirit Turret, level {N}**.
>
> Footprint: **2x2 tiles**. It is a magic defence: a round stone dais filling the footprint, four short carved
> pillars around it holding a rotating ring of brass, and inside the ring a large glowing spirit orb (violet-blue
> energy with swirling light). Total height about **2.5 tiles**. Small charms and paper talismans hang from the
> ring. The orb is the brightest thing in the image; it should glow so it stands out on a busy village.
>
> The silhouette must read as "magic tower": round, glowing core, clearly different from the Defense Tower.
>
> Level {N} tier: {paste the tier text}. As levels rise, the ring gets more ornate, the orb bigger and brighter,
> and floating crystal shards orbit it from level 7.
>
> Keep the exact rules from the start of this chat.

---

## Part 5: walls (1x1 tiles), 3 pieces x 10 levels = 30 images

The game builds walls out of separate pieces on a grid, one tile at a time, so every wall run, corner and end
joins up. It needs **three pieces per level**, all drawn at exactly the same scale and camera:

1. **Post:** a square pillar standing on one tile.
2. **Segment going up-right:** a straight wall section running from one tile's centre to the next tile's centre
   towards the upper right of the screen (the world's +X direction).
3. **Segment going up-left:** the same, running towards the upper left (the world's +Z direction).

File names: `wall_post_level_01.png` to `wall_post_level_10.png`, `wall_segment_right_level_01.png` to
`wall_segment_right_level_10.png`, `wall_segment_left_level_01.png` to `wall_segment_left_level_10.png`.

Wall framing is different from buildings. Use this instead of rule 3:

> **Wall framing:** the image is 1024 x 1024. Imagine a floor grid of diamond tiles, each diamond **600 px wide and
> 300 px tall** (2:1). Stepping from one tile's centre to the next tile's centre moves **300 px sideways and 150 px
> up**. The tiles are not drawn.
>
> - **Post:** the post stands on the centre of one tile. Its square base is **60% of a tile** wide (a 360 x 180 px
>   diamond), with the base's front corner at the horizontal centre of the image, 120 px above the bottom edge.
>   The stone body is about **400 px tall**, with a cap on top (see tiers).
> - **Segment going up-right:** a straight wall **one tile long**. The middle of its base at the front-left end sits
>   at pixel (362, 862), and at the back-right end at pixel (662, 712): it runs exactly 300 px right and 150 px up.
>   Wall thickness **40% of a tile**, height about **330 px**, a little lower than the post. Both ends are cut off
>   flat and plain, because a post will stand over each end.
> - **Segment going up-left:** the mirror image: front-right end at pixel (662, 862), back-left end at (362, 712).
>
> Every level's pieces use exactly these positions and sizes, so they line up when the game swaps levels.

Prompt for each piece:

> Draw the **wall {piece} for wall level {N}**, using the wall framing above.
>
> It is the outer wall of a ninja fortress: thick, chunky blocks, clearly readable from far away. Strong contrast
> between the stone and its trim, so a long wall run reads as a solid bold line around the base, like the walls in
> top base-building games, but an original design.
>
> Level {N} wall tier:
> - 1 to 2: rough timber palisade logs lashed with rope on a low stone footing.
> - 3 to 4: grey cut-stone blocks with a small slate-blue tiled cap.
> - 5 to 6: larger dressed stone with gold-capped tops and a red lacquered rail.
> - 7 to 8: dark granite with heavy gold plates and small glowing cyan crystal studs.
> - 9 to 10: obsidian blocks with gold borders and glowing violet runes along the top; level 10 has floating sparks.
>
> The post matches the segments of the same level, and is the tallest piece, with a decorated cap (a gold
> spike at levels 5 and up, a small glowing crystal at levels 7 and up).
>
> Transparent background, no ground shadow, no text, 2:1 isometric at 30 degrees.

---

## Part 6: tips that save time

- Do Defense Tower level 1, level 5 and level 10 first, and send them to Claude to check in-game before making the
  other seven. Then do the same for the Spirit Turret and the wall.
- If ChatGPT drifts (a different angle, a missing base, a ground shadow), start a new chat and paste Part 1 again.
- If a level looks too similar to the one before, ask: "Make level {N} clearly grander than level {N-1}: taller,
  more gold, more glow, same footprint and framing."
- Save the files into one folder, e.g. `Downloads\ShinobiDominion_Towers_Walls`, and tell Claude where it is.
  Claude imports them with the same tool used for your last two packs.
