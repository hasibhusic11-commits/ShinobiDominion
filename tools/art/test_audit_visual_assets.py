"""Regression checks for the art inventory using a disposable fake Unity project."""
import json
from pathlib import Path
import tempfile
import unittest

from audit_visual_assets import audit


class VisualArtAuditTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.resources = self.root / "unity" / "Assets" / "_Game" / "Resources"
        (self.resources / "Config").mkdir(parents=True)
        self._write("heroes.json", {
            "heroes": [
                {"id": "legend_one", "rarity": "Legendary"},
                {"id": "common_one", "rarity": "Common"},
                {"id": "retired_one", "rarity": "Common", "retired": True},
            ]
        })
        self._write("buildings.json", {
            "buildings": [
                {"id": "dojo_hq", "function": "Headquarters"},
                {"id": "wall", "function": "Wall"},
                {"id": "bomb_trap", "function": "Trap"},
            ]
        })
        self._write("hero_sprites.json", {
            "sprites": [
                {"heroId": "common_one", "form": -1,
                 "enabled": True, "path": "Heroes3D/common_one"}
            ]
        })

    def _write(self, name, data):
        (self.resources / "Config" / name).write_text(
            json.dumps(data), encoding="utf-8")

    def _asset(self, folder, name):
        path = self.resources / folder / (name + ".png")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"png-stub")

    def test_reports_real_gaps_and_model_bake_candidates(self):
        report = audit(self.root)
        self.assertEqual(["common_one", "legend_one"],
                         report["missing_hero_portraits"])
        self.assertEqual(["common_one"],
                         report["eligible_for_3d_model_bake"])
        self.assertEqual(3, len(report["missing_legendary_evolution_paintings"]))
        self.assertEqual(30, len(report["missing_wall_sprites"]))
        self.assertEqual(["dojo_hq"], report["missing_building_types"])
        self.assertFalse(report["complete"])

    def test_existing_art_is_not_misreported_as_missing(self):
        self._asset("HeroPortraits", "common_one")
        self._asset("HeroConcepts", "legend_one")
        for stage in range(1, 4):
            self._asset("HeroConcepts", f"legend_one_evo_{stage}")
        for wall_type in ("wall_seg_x", "wall_seg_z", "wall_post_v2"):
            for level in range(1, 11):
                self._asset("Buildings", f"{wall_type}_{level:02}")
        for level in range(1, 11):
            self._asset("Buildings", f"dojo_hq_{level:02}")
        report = audit(self.root)
        self.assertTrue(report["complete"])
        self.assertEqual([], report["missing_hero_portraits"])
        self.assertEqual({}, report["missing_building_tiers"])


if __name__ == "__main__":
    unittest.main()
