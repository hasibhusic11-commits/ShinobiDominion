#!/usr/bin/env python3
"""Inventory authored hero, wall and building art; never pretend a fallback is finished art.

Runs with Python 3 stdlib only:
    python tools/art/audit_visual_assets.py
    python tools/art/audit_visual_assets.py --strict
Optional: --root /path/to/ShinobiDominion --output /path/to/report.json
The default report is written outside Unity's Assets folder under artifacts/.
"""
import argparse
import json
from pathlib import Path
import sys

IMAGE_EXTENSIONS = (".png", ".webp", ".tga", ".psd", ".jpg", ".jpeg")
ART_LEVELS = 10


def load_json(path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def has_art(folder, name):
    return any((folder / (name + extension)).is_file() for extension in IMAGE_EXTENSIONS)


def audit(root):
    resources = root / "unity" / "Assets" / "_Game" / "Resources"
    heroes = load_json(resources / "Config" / "heroes.json")["heroes"]
    buildings = load_json(resources / "Config" / "buildings.json")["buildings"]
    registry = load_json(resources / "Config" / "hero_sprites.json")["sprites"]

    hero_concepts = resources / "HeroConcepts"
    baked_portraits = resources / "HeroPortraits"
    building_art = resources / "Buildings"

    models = {
        entry["heroId"] for entry in registry
        if entry.get("enabled")
        and entry.get("form") == -1
        and entry.get("path", "").startswith("Heroes3D/")
    }

    missing_portraits = []
    model_bake_candidates = []
    missing_evolutions = []
    for hero in heroes:
        if hero.get("retired"):
            continue
        hero_id = hero["id"]
        if not has_art(hero_concepts, hero_id) and not has_art(baked_portraits, hero_id):
            missing_portraits.append(hero_id)
            if hero_id in models:
                model_bake_candidates.append(hero_id)
        if hero.get("rarity") == "Legendary":
            for stage in range(1, 4):
                filename = f"{hero_id}_evo_{stage}"
                if not has_art(hero_concepts, filename):
                    missing_evolutions.append(filename)

    missing_walls = [
        f"{piece}_{tier:02}"
        for piece in ("wall_seg_x", "wall_seg_z", "wall_post_v2")
        for tier in range(1, ART_LEVELS + 1)
        if not has_art(building_art, f"{piece}_{tier:02}")
    ]

    missing_building_types = []
    missing_building_tiers = {}
    for building in buildings:
        building_id = building["id"]
        if building.get("function") in ("Wall", "Trap"):
            continue
        missing = [
            tier for tier in range(1, ART_LEVELS + 1)
            if not has_art(building_art, f"{building_id}_{tier:02}")
        ]
        if missing:
            missing_building_tiers[building_id] = missing
        if len(missing) == ART_LEVELS:
            missing_building_types.append(building_id)

    result = {
        "hero_count": len(heroes),
        "building_count": len(buildings),
        "art_tiers_per_building": ART_LEVELS,
        "missing_hero_portraits": sorted(missing_portraits),
        "eligible_for_3d_model_bake": sorted(model_bake_candidates),
        "missing_legendary_evolution_paintings": sorted(missing_evolutions),
        "missing_wall_sprites": missing_walls,
        "missing_building_types": sorted(missing_building_types),
        "missing_building_tiers": missing_building_tiers,
    }
    result["complete"] = not (
        missing_portraits or missing_evolutions or missing_walls or missing_building_tiers
    )
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path, default=None)
    parser.add_argument("--strict", action="store_true",
                        help="Exit nonzero if art is missing; use only for production release gates")
    args = parser.parse_args(argv)
    result = audit(args.root.resolve())
    output = args.output or args.root.resolve() / "artifacts" / "visual_art_inventory.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")

    print("Visual art inventory:", output)
    print(f"Missing hero portraits: {len(result['missing_hero_portraits'])}; " +
          f"3D bake candidates: {len(result['eligible_for_3d_model_bake'])}; " +
          f"Legendary evo paintings: {len(result['missing_legendary_evolution_paintings'])}; " +
          f"wall sprites: {len(result['missing_wall_sprites'])}; " +
          f"building types with incomplete tiers: {len(result['missing_building_tiers'])}")
    if result["missing_hero_portraits"]:
        print("Missing hero portrait IDs: " + ", ".join(result["missing_hero_portraits"]))
    if result["eligible_for_3d_model_bake"]:
        print("Baking candidates: " + ", ".join(result["eligible_for_3d_model_bake"]))
    if result["missing_legendary_evolution_paintings"]:
        print("Missing Legendary evo paintings: " +
              ", ".join(result["missing_legendary_evolution_paintings"]))
    if args.strict and not result["complete"]:
        print("ART AUDIT FAILED: some original assets are missing.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
