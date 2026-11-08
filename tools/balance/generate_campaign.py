"""Writes the dungeon campaign into raids.json. Stages 1 and 2 are hand-made and only gain rewards here.
All numbers and names are proposed test values. Edit the tables below and rerun:
    python tools/balance/generate_campaign.py unity/Assets/_Game/Resources/Config/raids.json
"""
import json
import sys

SIZES = {"dojo_hq": 5, "defense_tower": 2, "spirit_turret": 2, "gold_vault": 3, "gold_mine": 3, "crystal_mine": 3, "builder_hall": 3, "hero_base": 2, "hero_altar": 4, "troop_camp": 3}

CHAPTERS = {1: "Cinder Ridge", 2: "Mist Hollow", 3: "Ashen Peak"}

# Stage: name, description. Stages 1 and 2 already exist.
STAGES = {
    3: ("Charcoal Pass", "A narrow camp that guards the road through the ridge."),
    4: ("Kiln Yard", "Smoke hides the towers here until you are close."),
    5: ("Ember Keep", "The first true fortress. Its keeper holds an Awakening Scroll."),
    6: ("Reed Marsh Camp", "Raiders hide among the reeds at the edge of the hollow."),
    7: ("Lantern Ford", "Spirit turrets light the only crossing."),
    8: ("Fogbound Shrine", "Its guards know every path through the mist."),
    9: ("Willow Stockade", "Thick walls, and a garrison that does not retreat."),
    10: ("Hollow Citadel", "The heart of the hollow. Its lord holds an Ascension Stone."),
    11: ("Slate Stair", "The climb begins, under fire the whole way."),
    12: ("Cold Forge", "Weapons for the peak are made here, and tested on visitors."),
    13: ("Whitecrag Bastion", "Veteran heroes hold the last wall before the summit."),
    14: ("Sky Gate", "Nothing passes the gate without a fight."),
    15: ("Summit Sanctum", "The final stronghold. Its master holds a Sovereign Scroll."),
}

GUARD_ROSTER = ["lantern_warden", "reed_archer", "solar_beast_sage", "talisman_novice", "thunder_empress", "stone_titan"]
BOSS_ITEMS = {5: "awakening_scroll", 10: "ascension_stone", 15: "sovereign_scroll"}


def rewards(stage):
    return {
        "chapter": 1 + (stage - 1) // 5,
        "chapterName": CHAPTERS[1 + (stage - 1) // 5],
        "stage": stage,
        "xpPerWin": 30 + 25 * stage,
        "firstClearCrystals": 50 + 10 * stage + (150 if stage % 5 == 0 else 0),
        "firstClearItem": BOSS_ITEMS.get(stage, ""),
    }


def build_stage(stage):
    half_x = 9 + (stage % 3) + stage // 5
    half_z = 9 + ((stage + 1) % 3) + stage // 5
    cx, cz = 25, 25
    taken = set()

    def place(defn, level, x, z):
        size = SIZES[defn]
        cells = {(px, pz) for px in range(x, x + size) for pz in range(z, z + size)}
        if cells & taken:
            return None
        # Keep one clear cell to the walls so walls never overlap anything.
        if x <= cx - half_x or z <= cz - half_z or x + size > cx + half_x or z + size > cz + half_z:
            return None
        taken.update(cells)
        return {"def": defn, "level": level, "x": x, "z": z}

    building_level = min(15, 1 + stage // 3)
    buildings = [place("dojo_hq", min(15, 2 + stage // 3), cx - 2, cz - 2)]
    near_x, far_x = cx - half_x + 2, cx + half_x - 4
    near_z, far_z = cz - half_z + 2, cz + half_z - 4
    corners = [(near_x, near_z), (far_x, far_z), (far_x, near_z), (near_x, far_z)]
    sides = [(cx - 1, near_z), (cx - 1, far_z), (near_x, cz - 1), (far_x, cz - 1)]

    towers = min(8, 2 + stage // 3)
    turrets = min(4, stage // 4)
    spots = corners[stage % 4:] + corners[:stage % 4] + sides
    for i in range(towers + turrets):
        if i >= len(spots):
            break
        defn = "spirit_turret" if i >= towers else "defense_tower"
        # Towers rise with odd stages only, so each pair of stages shares a tower level (rebalanced for the
        # 5-hit ability bar, 2026-11-08: stage 10 was a level too hot once specials stopped charging while walking).
        buildings.append(place(defn, min(15, (stage + 1) // 2), *spots[i]))

    economy = ["gold_vault", "gold_mine", "builder_hall", "crystal_mine", "gold_mine", "troop_camp"]
    filler = [(cx - 6, cz - 6), (cx + 4, cz - 6), (cx - 6, cz + 4), (cx + 4, cz + 4), (cx - 7, cz - 1), (cx + 5, cz - 1)]
    for defn, (x, z) in zip(economy[:3 + stage // 4], filler):
        buildings.append(place(defn, building_level, x, z))

    guards = 1  # One hero guards a village, as in every village (ADR-030).
    guard_spots = [(cx - 1, cz + 3), (cx - 1, cz - 4), (cx + 3, cz + 1), (cx - 4, cz + 1), (cx + 3, cz - 3), (cx - 4, cz - 3)]
    level = min(200, 8 + stage * 9)
    evolution = 0
    defenders = []
    for i in range(guards):
        for x, z in guard_spots:
            base = place("hero_base", building_level, x, z)
            if base:
                buildings.append(base)
                defenders.append({"heroId": GUARD_ROSTER[i % len(GUARD_ROSTER)], "evolution": evolution, "level": level, "x": x, "z": z})
                break

    wall_level = min(15, 1 + stage // 4)
    x1, x2, z1, z2 = cx - half_x, cx + half_x, cz - half_z, cz + half_z
    walls = [
        {"level": wall_level, "x1": x1, "z1": z1, "x2": x2, "z2": z1},
        {"level": wall_level, "x1": x2, "z1": z1, "x2": x2, "z2": z2},
        {"level": wall_level, "x1": x2, "z1": z2, "x2": x1, "z2": z2},
        {"level": wall_level, "x1": x1, "z1": z2, "x2": x1, "z2": z1},
    ]
    name, description = STAGES[stage]
    raid = {"id": "stage_%02d" % stage, "displayName": name, "description": description, "version": 1, "goldPerStar": 300 + 150 * stage}
    raid.update(rewards(stage))
    raid["requires"] = "cinder_garrison" if stage == 3 else "stage_%02d" % (stage - 1)
    raid.update({"buildings": [b for b in buildings if b], "walls": walls, "defenders": defenders})
    return raid


def main(path):
    with open(path, encoding="utf-8") as handle:
        document = json.load(handle)
    raids = [r for r in document["raids"] if not r["id"].startswith("stage_")]
    for number, raid in enumerate(raids, start=1):
        raid.update(rewards(number))
        raid["requires"] = ""
    raids += [build_stage(stage) for stage in sorted(STAGES)]
    document["raids"] = raids
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(document, handle, indent=2)
        handle.write("\n")
    print("CAMPAIGN_DONE", len(raids))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    main(args[0])
