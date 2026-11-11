# First-pass original Elite hero portraits — image production handoff

**Produced 2026-11-11.** This pack is a new set of ten original rendered character images, not screenshots of the running game. The originals are delivered as a separate downloadable ZIP in the ChatGPT conversation because the connected GitHub contents interface accepts UTF-8 text but does not provide a binary-file upload handoff from the image-generation runtime.

## Pack contents and target import path
Unzip `shinobi_elite_portrait_pack.zip` at the **repository root**. Its PNG files are placed under:
`unity/Assets/_Game/Resources/HeroConcepts/{heroId}.png`

| Artist-character key | Design identity |
|---|---|
| `lantern_warden` | Mountain-road lantern keeper in heavy metal armour |
| `scarlet_fan_dancer` | Fire-court performer wielding red iron fans |
| `twin_chain_reaper` | Masked steel assassin with paired chained sickles |
| `frost_lake_monk` | Serene blue-white healer beside frozen water |
| `boulder_fist_elder` | Elder with white beard and heavy stone-bound fists |
| `mask_carver` | Hermit in purple carrying carved spirit masks |
| `thunder_drum_monk` | Lightning monk with ceremonial storm drum |
| `salt_wind_nomad` | Desert traveller with curved glaive and windswept robes |
| `silver_moon_archer` | Silver-clad moonlit archer |
| `cinder_shield_captain` | Scarred veteran with ember-cracked tower shield |

Every file is an individual ~390x390 PNG crop from the original 5×2 illustration sheet. The image files have **not** been pushed to GitHub through the connector; this Markdown describes the required integration, not a claim that game assets exist in the branch.

## QA and integration
1. Extract the ZIP at the repository root. Do not rename hero IDs.
2. Open the Unity project at the documented version, allow import and save generated `.meta` files. The resource loader finds `HeroConcepts/{heroId}` and prefers it over any procedural or model-based preview.
3. Verify each portrait belongs to its described hero and doesn't clip heads. The crop helper in this PR supports square art without changing tall Legendary cards.
4. Open Hero Detail for all ten heroes, and check portrait scale and aspect, and artwork visibility at 16:9 and 4:3. Reject inconsistent illustrations rather than misrepresenting them as final character assets.
5. Run the visual art audit (`python tools/art/audit_visual_assets.py`), Unity PlayMode tests, then add/commit all validated PNG files and Unity `.meta` files.
6. No evolution forms, in-world 3D animation or skill icons are created by these portrait PNGs.

## Copyright, gameplay and signoff
All images were originally created for Shinobi Dominion's named characters; do not use existing franchise symbols, UI assets or licensed characters. Portraits do not change hero ownership, rarity, summon odds, abilities or economy. These are **first-pass** paintings, not finished 10/10 approval. Owner inspection in the actual Unity scene is the release gate.

## Validated installation command

Download the `shinobi_elite_portrait_pack.zip` attachment and run the installer from the repository root:

```powershell
python tools/art/install_elite_portraits.py --zip "$HOME\\Downloads\\shinobi_elite_portrait_pack.zip"
```

The installer checks the ten canonical hero IDs, ZIP CRC, manifest, PNG headers and minimum image dimensions, rejects unexpected images, and **refuses to overwrite** an existing file without `--overwrite`. It installs all ten or fails before copying any images when validation fails. These new first-pass illustrations have a painted background rather than transparent cutout alpha; HeroConcepts treats them as full card paintings.

GitHub Actions runs four standalone installer tests plus two art-inventory tests on the pull request. This validates the packaging logic, **not** the Unity image quality. After extraction, Unity should generate `.meta` files; inspect the actual UI before committing both the PNG and meta files.

The downloadable archive is supplied in the ChatGPT conversation, not by the GitHub repository. GitHub connector access does not currently support binary-asset upload; this branch contains the installer code and mapping but **not** the ten final PNGs.
