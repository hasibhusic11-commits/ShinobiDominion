#!/usr/bin/env python3
"""Fail fast on invalid gameplay configuration and unsafe tracked dev settings.

No third-party Python dependencies. This is a guardrail, not a replacement for
Unity PlayMode, backend integration, content review, or real-device QA.
"""
import json
import pathlib
import re
import sys
from collections import Counter

ROOT = pathlib.Path(__file__).resolve().parents[2]
def read(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))

def fail(message):
    raise AssertionError(message)

def main():
    heroes = read("unity/Assets/_Game/Resources/Config/heroes.json")["heroes"]
    by_id = {hero["id"]: hero for hero in heroes}
    if len(by_id) != len(heroes):
        fail("Duplicate hero ID in heroes.json")
    pool = read("backend/nakama/config/summon_pool.json")
    seen = set()
    weights = Counter()
    for item in pool["items"]:
        hero_id, weight = item["archetypeId"], item["weight"]
        if hero_id in seen:
            fail(f"Duplicate summon entry: {hero_id}")
        seen.add(hero_id)
        if not isinstance(weight, int) or weight <= 0:
            fail(f"Invalid summon weight: {hero_id}")
        hero = by_id.get(hero_id)
        if not hero or not hero.get("contentComplete") or hero.get("retired"):
            fail(f"Unfinished, retired, or unknown summoned hero: {hero_id}")
        weights[hero["rarity"]] += weight
    if sum(weights.values()) <= 0:
        fail("Empty summon pool")
    if pool.get("version", 0) < 1:
        fail("Missing pool version")
    for rarity, weight in sorted(weights.items()):
        print(f"[summon] {rarity}: {weight / sum(weights.values()):.2%}")

    buildings = read("unity/Assets/_Game/Resources/Config/buildings.json")["buildings"]
    if len({b["id"] for b in buildings}) != len(buildings):
        fail("Duplicate building ID")
    level_lengths = Counter(len(b.get("levels", [])) for b in buildings)
    if any(level < 1 for level in level_lengths):
        fail("Building without upgrade data")
    print(f"[buildings] {len(buildings)} types, level counts: {dict(level_lengths)}")
    if len(level_lengths) > 1:
        print("[warning] Building level counts differ across types; review intended design.")

    sprites = read("unity/Assets/_Game/Resources/Config/hero_sprites.json")["sprites"]
    enabled = [s for s in sprites if s.get("enabled")]
    if len({(s["heroId"], s["form"]) for s in enabled}) != len(enabled):
        fail("Duplicate enabled hero art mapping")
    for item in enabled:
        if item["heroId"] not in by_id:
            fail(f"Unknown hero model mapping: {item['heroId']}")
        if item["form"] not in (-1, 0, 1, 2, 3):
            fail(f"Invalid evolution form mapping: {item['heroId']}")
    expected = [h["id"] for h in heroes if h.get("contentComplete") and not h.get("retired")]
    represented = {s["heroId"] for s in enabled}
    missing = sorted(set(expected) - represented)
    if missing:
        print("[art warning] No 3D/sprite prefab for these playable heroes: " + ", ".join(missing))
    for hero in heroes:
        if hero["rarity"] == "Legendary":
            if (hero["id"], 1) not in {(s["heroId"], s["form"]) for s in enabled}:
                print("[evolution art warning] Distinct Evo I prefab missing: " + hero["id"])

    compose = (ROOT / "backend/docker-compose.yml").read_text(encoding="utf-8")
    if re.search(r"-\s*-ecx\b|set\s+-x", compose):
        fail("Shell command tracing is forbidden in Docker startup: may expose credentials")
    if (ROOT / "backend/.env").is_file():
        print("[note] Local backend/.env exists; it must remain gitignored.")
    print("Config hygiene passed; art warnings are advisory until required assets exist.")

if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, ValueError) as exc:
        print(f"CONFIG AUDIT FAILED: {exc}", file=sys.stderr)
        sys.exit(1)
