# VRoid hero builds

**Status: refined drafts, built 2026-11-06 at the owner's request, without check-ins. None is approved or finished, and nothing has been added to the game.**

Files are in `_tools/vroid_heroes/` (`<hero_id>.vroid` to edit, `<hero_id>.vrm` as exported). First-pass copies are in `_tools/vroid_heroes/_first_pass/`. Renders are in `docs/heroes/vroid_models/<hero_id>_refined/`. Concepts are in `art_incoming/`.

Overview images in `docs/heroes/vroid_models/`:
- `roster_refined_vs_concepts.png`: each concept beside its refined front render
- `roster_refined_poses.png`: seven heroes (not Thunder Empress) in eight poses in Unity, before the second pass
- `roster_refined_legs_closeups.png`: lower legs in the same poses, before the second pass
- `roster_models.png`: the first pass, now out of date

They were built by Claude in VRoid Studio from presets, sliders and flat paint on exported textures (`_tools/vroid_refine/`). No custom hair or clothing was modelled. Colours, faces and worn details can get close to the concepts. Silhouettes can't.

## Results

Triangles and materials were measured from the exported files in Blender. Bones are as Unity reported them. Closeness is judged by eye against the concept.

| Hero | Triangles | Materials | Bones | Closeness | Biggest gaps |
|---|---|---|---|---|---|
| Solar Beast Sage | 17,959 | 2 | 90 | Medium | Baggy knee trousers over painted shins. Gap at the ankles above low shoes. Brow wrap has no knot or tails. Knot is spiky |
| Thunder Empress | 17,619 | 2 | 163 | Medium to high by colour and face, lower by clothing shape | Open coat, not a closed high collar. No hairpin |
| Stone Titan | 16,624 | 2 | 132 | Medium by clothing, low to medium by build | Arms and chest still far slimmer than the concept. Trouser hems flare. No maul. Looks young |
| Moonfang Oracle | 16,904 | 2 | 136 | Medium | Over-robe and pleats are paint on one robe, so no wide layered sleeves. No charm. Looks young |
| Crimson Eye Prodigy | 16,783 | 2 | 171 | Medium | No cape or blade. Short hair. Gem-like eyes |
| Void Sovereign | 15,481 | 2 | 146 | Low to medium | No orb or cape. Layers are paint, so the silhouette is slim. Deliberately different face and hair |
| Ashen Flame Lord | 16,715 | 2 | 143 | Low to medium | Beard and scar are flat paint. No pole. Rags are paint. Trench lapels |
| Storm Crow Shinobi | 16,908 | 2 | 132 | Low to medium | Mask is flat paint. No scarf, sash or blades. Sci-fi bodysuit and vest. Sleeves and harness stayed black |

## Seven-hero refinement (2026-11-06)

Each hero got a new face set, eyes, skin, outfit and hair from presets, then recolouring or paint through exported texture layers. Skin hidden under clothes was masked out where it could poke through.

- **Solar Beast Sage:** wrap top and knee trousers in navy, orange collar, sash and bow, amber eyes. Navy shins, and orange bindings with wrap lines at the ankles and wrists, painted on skin (`sage_legs.py`).
- **Stone Titan:** the cardigan is now a leather vest (`stone_titan_paint.py`): slate sleeves cut at the forearm with a rolled cuff, painted shirt front with lacing, belt and buckle, pocket flaps and stitched repairs. Patched brown trousers, brown boots with the shafts and cuffs blended away. The skin mask is opened at the forearms (`open_forearms.py`).
- **Moonfang Oracle:** grey upper, white collar and sash, pale blue eyes. Painted open over-robe with white borders, pleated blue skirt, white sleeve ends (`hero_details.py`).
- **Crimson Eye Prodigy:** long coat in crimson and gold, dark vest and trousers, crimson plain eyes.
- **Void Sovereign:** plum kimono, crimson collar and sash, torn hem, short ash hair, violet eyes. Painted dark torn outer layer, crimson trim, cords and ring (`hero_details.py`).
- **Ashen Flame Lord:** coat in soot grey, linen sash with an ember stripe, torn hem, gauntlets, ember eyes. Painted worn cloth, patches, stitched tears, torn lighter layer, ember strip, bound sleeve ends (`hero_details.py`).
- **Storm Crow Shinobi:** tactical bodysuit in slate, blue gloves, blue eyes. The sleeve and harness imports never showed, so those stayed black.

Painted layers have no depth, so up close and from the side they read as a flat print on one garment.

**Unity test (2026-11-06), throwaway project copy only:** script `_tools/eval/run_unity_all_heroes.ps1`. It used the test cut-out shader with the rim mask on, and existing test clips posed frame by frame. Each hero's images and report are in `<hero_id>_refined/unity/`. This covers the exports before the second pass below.
- **Passed for all seven:** Humanoid avatar valid, 43 of 43 clips human, 57 blendshapes imported. Both tests passed, but those only check that import and capture run.
- **By eye:** they idle, jog, punch, take a hit, fall and dance without broken limbs. No skin shows through clothing in the front lower-leg close-ups of those poses.
- **Faults seen:** Moonfang's and Void's long robes stretch into a sheet between the legs in the wide jog stride, which also stretches their paint. Void's torn hem is jagged up close.
- **Only partly checked:** back views were saved but not studied closely, and upper bodies were only seen at full-figure size.

## Second pass towards the concepts (2026-11-06, stopped partway)

The owner asked for the models to look more like the concepts, then asked to stop part way through, so this pass covers some heroes only. **None of these exports has been tested in Unity.** The Unity results above are for the exports before this pass, and body and clothing shape changed since then.

- **Solar Beast Sage:** head smaller, shoulders wider, fringe removed, brow wrap painted on the forehead.
- **Stone Titan:** head smaller, 2.1 m tall, thicker neck, bigger hands and feet, bulkier vest and wider trousers, stubble.
- **Ashen Flame Lord:** head smaller, taller, wider shoulders, painted beard and scar.
- **Storm Crow Shinobi:** head smaller, wider shoulders, painted cloth mask.
- **Crimson Eye Prodigy:** head smaller, wider shoulders.
- **Not done:** Void Sovereign, Moonfang Oracle and Thunder Empress keep their earlier proportions. Void's body changes were interrupted, may not be saved, and aren't in his export.
- **Known fault:** Sage has a gap at the ankles above his low shoes. Ankle boots should cover it, but this wasn't tried.

Face paint uses `_tools/vroid_refine/face_paint.py`. VRoid's colour calibration turns paint into skin tones, so the skin colour is baked into the texture and calibration is switched off. The face skin no longer follows the Skin colour settings on those heroes.

Hair Smoothness reduction is now 85 on Titan, Ashen, Storm Crow and Crimson, and 60 on Sage. Other reductions are Hair 60, Face 52, Body 52, Outfit 60. Hair may look rougher up close.

## Thunder Empress refinement (2026-11-06)

**Status: refined draft, not approved or finished.** Comparison: `docs/heroes/vroid_models/thunder_empress_refined/compare_concept_first_refined.png` (concept, first pass, then refined front, side and back). `thunder_empress_front.png` still shows the first pass. The first-pass files are in `_tools/vroid_heroes/_first_pass/`.

**Changed:**
- Sharper face preset, grey eyes and tan skin
- Centre-parted hair with an open forehead, and a cool hair highlight
- Coat, vest, cravat and lining recoloured to navy and copper by exporting each texture layer, recolouring it in Blender and importing it back (`_tools/vroid_refine/`, textures in `_tools/vroid_heroes/thunder_empress_tex/`)
- Striped copper waist band and round clasp painted on the coat texture
- Thigh strap removed from the trousers, which are now cool near-black
- Tall boots, with their red and gold trim made plain
- Foot, ankle and leg skin removed, because it poked through the shoes and, when the knees bent, the trousers. Body faces below 90 cm were masked out of the skin layer (`_tools/vroid_refine/mask_feet.py`), and Delete Transparent Meshes removes them on export. The trousers reach about 1 m.
- Boot cuffs removed by clearing the boots' second texture layer, because they showed through the trousers near the knees.

**Export:** 17,619 triangles (limit 18,000), 2 materials, 163 bones, 2048 atlas, measured in Blender. Reduction: Hair Smoothness 92, Hair 90, Face 52, Body 52, Outfit 72.

The model has no legs or feet under the clothes, and no boot cuffs, so changing to shorter trousers or footwear means restoring `thunder_empress_tex/skin_main.png` and `boots_layer2_plain.png` first. The previous export is in `_tools/vroid_heroes/_refine_backup/`.

**Still different from the concept:**
- The coat is open, with a vest, cravat and diagonal strap under it, recoloured to blend in. The concept has a closed, asymmetric coat with a high collar and copper buttons.
- No hairpin. VRoid's accessory list has only glasses, ears, tails and hats, so it needs a separate model.
- The trousers cover the boot shafts, where the concept has boots over the trousers.
- Proportions are unchanged from VRoid's default and were not checked against the concept.

**Known faults:**
- The boot pokes through the trouser hem in small pieces, most visibly at the heels.
- Faint marks remain on the back of the coat, and may be inner layers poking through after reduction.
- A pale patch shows on the coat hem in some jog frames. It comes from the shader's rim light, not the model or texture. `ShinobiHeroVRoid.shader` mostly clears it, but no game material uses that shader yet (see below).
- Hair is jagged up close, with stray strands at the sides.

**Unity test (2026-11-06), throwaway project copy only:** images are in `vroid_models/thunder_empress_refined/unity/`, with `legs_closeups.png` as the summary and `legs_closeups_before_leg_fix.png` for comparison. Script: `_tools/eval/run_unity_thunder_empress.ps1`. It used the test cut-out shader and existing test clips, posed frame by frame. Both tests passed, but those only check that import and capture run. The findings below are from looking at the images.

- **Passed:** Humanoid avatar valid, 43 of 43 clips human, 17,619 triangles (Body 11,625, Face 5,994), 2 materials, 57 blendshapes imported. She idles, jogs, punches, takes a hit, falls and dances without broken limbs.
- **Fixed on retest:** the first run showed tan skin through the trousers at the knees and thighs, and boot cuffs as dark squares near the knees. Neither shows in the retest's lower-leg close-ups, which covered mixed poses from front and back, jog from front and side, punch from the side and dance from the front. No foot skin shows either.
- **Only these clips and angles were checked,** so other animations could still expose faults.

**Pale coat patch, tested on two jog frames from front and back, in the test shader:**
- **Rim light off:** the patch becomes normal navy with copper trim. Pale pixels in the worst view fell from 2,739 to 239.
- **Sharpest texture level forced:** no real change, so texture blur is not the cause.
- **Rim skipped on surfaces facing away from the camera:** did not fix it (2,739 to 2,158), so that change was reverted.
- **Rim reduced on dark painted cloth (`_RimDarkMask` in the test shader, off by default):** the navy panel no longer washes out. Pale pixels by view: 2,739 to 483, 464 to 232, 693 to 299 and 892 to 239. Copper trim on that panel keeps some rim and still looks bright. Full poses also show less grey on the trousers and hair. Comparison: `unity/rim_mask_comparison.png` (normal on top, mask below).
- **Moved into a separate shader:** `unity/Assets/_Game/Shaders/ShinobiHeroVRoid.shader` (`ShinobiDominion/HeroVRoid`) has cut-out, both sides drawn, a face light control, and the rim mask on by default. `ShinobiHero.shader` is unchanged, so other heroes look the same.

**VRoid shader test (2026-11-06), throwaway project copy only:** a copy of that file compiled with no shader errors, reported as supported, and its defaults passed their checks. Its images match the earlier mask test: no pixel differed by more than 0.02 on a 0-1 scale across the 6 images compared. Script: `_tools/eval/run_unity_te_project_shader.ps1`.

**Not done or not verified:**
- Nothing uses the shader yet. No material or script references it, so Thunder Empress is not in the game, and Unity would leave it out of a build until a material does.
- The real project hasn't been opened in Unity with this file, and its `.meta` file was written by hand.
- Only tested on Thunder Empress, in single frames, against a plain grey background, on the editor's graphics. Android rendering and phone performance are untested. Cut-out and drawing both sides can both cost more on phones than the current hero shader.
- The face material must set `_LightFlatten`. 0.85 was used in testing.

**Not verified for this build:** hair and coat physics (spring bones aren't active in this test, so both move rigidly), facial expressions, real-time playback, the real game shader, phone performance, and the licence terms of the preset coat, boots and hair. The textures are recoloured from VRoid's preset items, so check that those terms allow it before release.

## Decisions made without the owner

- **Void Sovereign doesn't follow the saved concept's face and hair.** The model has ash-white hair and matching eyes, without the facial scars, spiky black hair or mismatched red and purple eyes, because those were the features flagged as resembling an existing character.
- **Crimson Eye Prodigy has plain eyes,** with no patterned design.
- **File permissions:** all eight allow commercial use by organisations and alterations, with redistribution prohibited and credit required. The creator is entered as "Shinobi Dominion", and this should be checked before release.

## Limits found

- **Hair:** hair is the largest triangle cost, and Solar Beast Sage started at 34,548 triangles. Reduced hair looks jagged up close, and Hair Smoothness at 100 visibly damaged long hair.
- **Topknot:** VRoid's knot and tail presets are mostly mirrored pairs. A proper single topknot needs custom hair.
- **Body size:** sliders can't make Stone Titan's huge build, and there's no arm thickness slider.
- **Clothing:** presets cover T-shirts, school uniforms and fantasy sets, but no gi, scarf, wraps, torn robes or props. Dark textures barely change when tinted.
- **Age:** default faces look young. Moonfang Oracle, Ashen Flame Lord and others need adult faces.

## Not verified

- Preset items' individual licence terms, for all eight. Their textures are recoloured and painted from VRoid presets, so check that the terms allow it before release
- The project shader `ShinobiHeroVRoid.shader` on the seven heroes (they used the test shader with the same rim mask)
- Hair and cloth physics, facial expressions and real-time playback (poses are single frames with spring bones off)
- Phone performance and Android rendering

## What it would take to match the concepts

A person making custom hair, clothing and textures in VRoid and Blender, plus separate weapon and prop models. Claude can't deliver that standard alone.
