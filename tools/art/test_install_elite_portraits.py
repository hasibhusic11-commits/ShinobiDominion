"""Tests for secure and predictable Elite portrait ZIP installation."""
import json
from pathlib import Path
import struct
import tempfile
import unittest
import zipfile

from install_elite_portraits import (
    EXPECTED_HERO_IDS, RESOURCE_PREFIX, PNG_MAGIC,
    PortraitPackageError, install,
)


def stub_png(width=390, height=390):
    # Installer checks a PNG signature and IHDR; a tiny test stub is sufficient.
    return PNG_MAGIC + struct.pack(">I", 13) + b"IHDR" + struct.pack(">II", width, height) + b"\x08\x02\x00\x00\x00" + b"\x00\x00\x00\x00"


class ElitePortraitInstallTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name) / "repo"
        project = self.root / "unity" / "ProjectSettings"
        project.mkdir(parents=True)
        (project / "ProjectVersion.txt").write_text("m_EditorVersion: 6000.3.28f1\n", encoding="utf-8")
        self.zip_path = Path(self.tmp.name) / "portraits.zip"

    def make_zip(self, extra=None, corrupt=None):
        with zipfile.ZipFile(self.zip_path, "w") as archive:
            archive.writestr("manifest.json", json.dumps({
                "ids": list(EXPECTED_HERO_IDS),
                "unity_resource_folder": "Assets/_Game/Resources/HeroConcepts",
            }))
            for hero_id in EXPECTED_HERO_IDS:
                name = RESOURCE_PREFIX + hero_id + ".png"
                archive.writestr(name, b"invalid" if hero_id == corrupt else stub_png())
            if extra:
                archive.writestr(extra, stub_png())

    def test_installs_only_expected_files(self):
        self.make_zip()
        self.assertEqual(10, install(self.zip_path, self.root))
        for hero_id in EXPECTED_HERO_IDS:
            self.assertTrue((self.root / (RESOURCE_PREFIX + hero_id + ".png")).is_file())

    def test_never_overwrites_without_explicit_approval(self):
        self.make_zip()
        install(self.zip_path, self.root)
        with self.assertRaises(PortraitPackageError):
            install(self.zip_path, self.root)
        self.assertEqual(10, install(self.zip_path, self.root, overwrite=True))

    def test_invalid_image_fails_without_installing_any(self):
        self.make_zip(corrupt="silver_moon_archer")
        with self.assertRaises(PortraitPackageError):
            install(self.zip_path, self.root)
        self.assertFalse((self.root / RESOURCE_PREFIX).exists())

    def test_extra_png_rejected(self):
        self.make_zip(extra="unity/Assets/_Game/Resources/HeroConcepts/unknown_hero.png")
        with self.assertRaises(PortraitPackageError):
            install(self.zip_path, self.root)
        self.assertFalse((self.root / RESOURCE_PREFIX).exists())


if __name__ == "__main__":
    unittest.main()
