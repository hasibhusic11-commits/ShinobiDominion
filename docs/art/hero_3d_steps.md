# Shinobi Dominion: turning the hero paintings into animated 3D heroes

Use an image-to-3D service: **Meshy** (meshy.ai) or **Tripo** (tripo3d.ai). Both turn one picture into a textured
3D model and can rig it (add a skeleton) and animate it. Plans and prices change, so check their pricing page; a
paid plan is needed for downloads you can use in a commercial game, and for auto-rigging.

**Start with ONE hero: Solar Beast Sage.** Send it to Claude, check it in the game, then do the other seven.

## 1. Make the model

1. Sign in, choose **Image to 3D**.
2. Upload the transparent painting from `Downloads\SD_Heroes_final\solar_beast_sage.webp`.
   (If it refuses .webp, open it and save as .png first.)
3. Settings, where the service offers them:
   - **Pose: A-pose or T-pose** (arms out from the body). Animation needs this; the painted action pose will not
     animate well. If there is no pose option, generate first, then use its "re-pose" / "A-pose" tool.
   - **Symmetry: on.**
   - **Topology: quad** (or "game ready"), **target about 10,000 to 20,000 polygons** (it runs on phones).
   - **Texture: on**, PBR on, **resolution 2048**.
   - Art style: **stylised / cartoon** if offered, not realistic.
4. Generate. If the face, hands or weapon come out melted, press regenerate (you get several tries), or try the
   other service with the same picture.

The flaming lion, wolf, moons, crows and magic swirls will not become part of the model. That is fine: Claude adds
them in the game as glowing effects around the hero.

## 2. Rig and animate

1. Open the model, choose **Rig / Animate** (Meshy: "Animate"; Tripo: "Rig" then "Animation").
2. Character type: **humanoid** (two arms, two legs).
3. Place the markers on chin, wrists, elbows, knees and groin if it asks.
4. Add these animations (names may differ slightly):
   - **Idle** (standing breathing)
   - **Walk**
   - **Run**
   - **Attack** (a punch for Solar Beast Sage and Stone Titan; a slash for Crimson Eye Prodigy and Storm Crow
     Shinobi; a spell cast for Thunder Empress, Void Sovereign, Ashen Flame Lord and Moonfang Oracle)
   - **Hit** (flinch), **Death** (fall) if available
   - A **special / skill** move (a big swing or cast) if available

Stone Titan (giant rock body) and Void Sovereign (long robe) may rig badly. If they do, tell Claude; there are other
ways for those two.

## 3. Download

- Format: **FBX** (best for Unity). If FBX is not offered, GLB.
- Download the model **with each animation** (some services give one file per animation; that is fine).
- Save everything for one hero into one folder:

```
Downloads\SD_Heroes_3D\solar_beast_sage\
    model.fbx
    idle.fbx
    walk.fbx
    run.fbx
    attack.fbx
    hit.fbx       (if you have it)
    death.fbx     (if you have it)
    special.fbx   (if you have it)
```

Then tell Claude the folder. Claude imports it, sets up the animation controller (idle, walk, attack, hit, death),
puts the hero on the Hero Base and in raids, sizes it to match the buildings, adds its glowing effects, and sends
screenshots.

## Hero folder names

solar_beast_sage, thunder_empress, stone_titan, moonfang_oracle, crimson_eye_prodigy, void_sovereign,
ashen_flame_lord, storm_crow_shinobi
