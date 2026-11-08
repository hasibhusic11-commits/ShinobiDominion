# Shinobi Dominion: building art brief

Following the create-game-assets workflow: one approved visual target, then families built from it.

## Game frame
- Fantasy: a feudal-Japanese ninja fortress you build up and defend. Original designs only (no Naruto,
  Clash of Clans or Castle Clash assets or symbols).
- Engine: Unity 6 URP, buildings are upright sprites on a 50 x 50 grid (`BuildingSprites`).
- Camera: orthographic, 30 degrees down, 45 degrees turned (`IsoCamera.Pitch`). The footprint is a 2:1 diamond.
- Native view: 1280 x 720 reference; home zoom shows a 3x3 building about 140 px wide, a 2x2 tower about 95 px.

## Visual system
- Shape: chunky and toy-like. Heavy stone bases, oversized roofs with upturned corners, thick posts.
- Silhouette: each building is recognisable from its outline alone at 90 px (tower = tall and narrow with a
  crossbow; turret = round with an orb; mine = mound with a tunnel; vault = box with gold).
- Value: three bands. Dark roof and outline, mid wood and stone, bright gold, banners and glows.
- Palette roles: roof slate #2E3A5C, lacquer crimson #A8262A, timber #7A4A28, stone #8C8478, trim gold
  #E2B04A, banner red #C0302B with gold crest, lantern glow #FFC870, magic cyan #5FE0FF / violet #9A6BFF.
- Edges: dark outline about 2 px at native size; soft bevels on every edge.
- Light: warm key from the upper left, cool fill, gentle rim on the right. No cast shadow in the sprite (the
  game draws the ground shadow).
- Detail: concentrated on the front-left and front-right faces and the roof; nothing small enough to vanish.
- Levels: 10 pictures spread over 15 game levels. Tiers add height, gold, lanterns, crystals (see prompts).

## Technical contract
- Canvas 1024 x 1024, transparent, front corner of the footprint at the bottom centre.
- Base diamond width written to `Resources/Buildings/art_meta.json` by `tools/art/import_buildings_v2.py`.
- Imported with mipmaps and trilinear filtering (`UiSpriteImporter`).
- Names: `<id>_level_NN.png` from source, `<id>_NN.png` in Resources.

## Visual target
- Approved: the owner's Defense Tower levels 1, 5 and 10 (ChatGPT, 2026-11-07).
- Under test: Blender-rendered models (`tools/art/blender_buildings.py`), checked side by side in the game.
