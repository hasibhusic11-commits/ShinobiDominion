#!/usr/bin/env python3
"""Static safety checks for the shipped content registry.

Prototype mode checks referential integrity without changing gameplay rules or
silently enabling incomplete heroes. Run with --release to additionally demand
all launch Legendaries and evolved art resources.
"""
import argparse
import json
import pathlib
import sys
from collections import Counter

ROOT = pathlib.Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "unity/Assets/_Game/Resources"
POOL = ROOT / "backend/nakama/config/summon_pool.json"

def read(path):
    return json.loads(path.read_text(encoding="utf-8"))

def fail(message):
    raise ValueError(message)

def validate(release=False):
    heroes = read(RESOURCES / "Config/heroes.json")["heroes"]
    buildings = read(RESOURCES / "Config/buildings.json")["buildings"]
    summon = read(POOL)
    sprite_map = read(RESOURCES / "Config/hero_sprites.json")["sprites"]
    ids = [h["id"] for h in heroes]
    if len(ids) != len(set(ids)):
        fail("Duplicate hero ids in heroes.json")
    by_id = {h["id"]: h for h in heroes}
    b_ids = [b["id"] for b in buildings]
    if len(b_ids) != len(set(b_ids)):
        fail("Duplicate building ids in buildings.json")
    for building in buildings:
        levels = building.get("levels", [])
        if not levels:
            fail(f"Missing upgrade levels for {building['id']}")
        if len(levels) != len(buildings[0].get("levels", [])):
            fail(f"Uneven building upgrade levels: {building['id']}")
    if summon.get("priceCrystalsPerRoll", 0) <= 0:
        fail("Summoning must have a positive price")
    weights = Counter()
    listed = set()
    for entry in summon["items"]:
        hero_id = entry["archetypeId"]
        if hero_id in listed:
            fail(f"Duplicate hero in summon pool: {hero_id}")
        listed.add(hero_id)
        hero = by_id.get(hero_id)
        if not hero:
            fail(f"Unknown hero in summon pool: {hero_id}")
        if not hero.get("contentComplete", False):
            fail(f"Unfinished hero in summon pool: {hero_id}")
        if hero.get("retired", False):
            fail(f"Retired hero in summon pool: {hero_id}")
        if not isinstance(entry["weight"], int) or entry["weight"] <= 0:
            fail(f"Invalid summon weight: {hero_id}")
        weights[hero["rarity"]] += entry["weight"]
    total = sum(weights.values())
    if total <= 0:
        fail("Summon pool is empty")
    evolution = read(RESOURCES / "Config/heroes.json").get("catalystByTransition", [])
    if len(evolution) != 3:
        fail("Hero evolution must have exactly three catalysts/transitions")
    for entry in sprite_map:
        if not entry.get("enabled", False):
            continue
        path = RESOURCES / (entry["path"] + ".prefab")
        if not path.exists():
            fail(f"Enabled in-world hero prefab missing: {entry['heroId']}: {path}")
        if entry["heroId"] not in by_id:
            fail(f"Enabled prefab for unknown hero: {entry['heroId']}")
    legendaries = [h for h in heroes if h["rarity"] == "Legendary"]
    incomplete = [h["id"] for h in legendaries if not h.get("contentComplete", False)]
    missing_art = []
    for hero in legendaries:
        for evolution_stage in range(1, 4):
            art = RESOURCES / f"HeroConcepts/{hero['id']}_evo_{evolution_stage}.png"
            if not art.exists():
                missing_art.append(str(art.relative_to(ROOT)))
    print(f"Heroes: {len(heroes)}; Legendaries: {len(legendaries)}")
    print("Summon rarity odds:", ", ".join(f"{rarity} {100 * weight / total:.3f}%" for rarity, weight in sorted(weights.items())))
    print(f"Buildings: {len(buildings)}; levels per building: {len(buildings[0]['levels'])}")
    print(f"Enabled in-world hero prefabs: {sum(bool(e.get('enabled')) for e in sprite_map)}")
    if incomplete:
        print("LAUNCH BLOCKER: incomplete Legendary content:", ", ".join(incomplete))
    if missing_art:
        print(f"LAUNCH BLOCKER: {len(missing_art)} evolution art resources missing")
    if release and (incomplete or missing_art):
        fail("Release validation failed: incomplete hero content or evolution art")
    print("Catalog references are internally consistent.")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--release", action="store_true", help="Fail if launch art/content is still incomplete")
    args = parser.parse_args()
    try:
        validate(args.release)
    except (ValueError, KeyError, json.JSONDecodeError, OSError) as exc:
        print(f"CONTENT VALIDATION FAILED: {exc}", file=sys.stderr)
        sys.exit(1)
