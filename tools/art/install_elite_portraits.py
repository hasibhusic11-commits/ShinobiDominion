#!/usr/bin/env python3
"""Install the original ten-hero Elite portrait ZIP into the Unity project.

Example:
    python tools/art/install_elite_portraits.py --zip shinobi_elite_portrait_pack.zip

This verifies hero IDs, PNG dimensions, archive checksums and safe paths.
It never overwrites existing artwork unless --overwrite is explicitly supplied.
After copying, open Unity so .meta files are created, review all portraits and
commit PNG + .meta files together.
"""
import argparse
import json
from pathlib import Path
import struct
import sys
import zipfile

EXPECTED_HERO_IDS = (
    "lantern_warden",
    "scarlet_fan_dancer",
    "twin_chain_reaper",
    "frost_lake_monk",
    "boulder_fist_elder",
    "mask_carver",
    "thunder_drum_monk",
    "salt_wind_nomad",
    "silver_moon_archer",
    "cinder_shield_captain",
)
RESOURCE_PREFIX = "unity/Assets/_Game/Resources/HeroConcepts/"
PNG_MAGIC = b"\x89PNG\r\n\x1a\n"


class PortraitPackageError(ValueError):
    pass


def png_size(data):
    """Read dimensions from a validated PNG header, without optional image libs."""
    if len(data) < 33 or data[:8] != PNG_MAGIC or data[12:16] != b"IHDR":
        raise PortraitPackageError("Not a PNG image")
    width, height = struct.unpack(">II", data[16:24])
    if not (256 <= width <= 2048 and 256 <= height <= 2048):
        raise PortraitPackageError(f"Unexpected portrait dimensions: {width}x{height}")
    return width, height


def install(zip_path, repo_root, overwrite=False):
    repo_root = Path(repo_root).resolve()
    version = repo_root / "unity" / "ProjectSettings" / "ProjectVersion.txt"
    if not version.is_file():
        raise PortraitPackageError(f"Unity project not found at {repo_root}")
    zip_path = Path(zip_path).expanduser().resolve()
    if not zip_path.is_file():
        raise PortraitPackageError(f"ZIP package does not exist: {zip_path}")

    with zipfile.ZipFile(zip_path) as archive:
        bad = archive.testzip()
        if bad:
            raise PortraitPackageError(f"Damaged ZIP entry: {bad}")
        members = archive.namelist()
        expected = {RESOURCE_PREFIX + hero_id + ".png" for hero_id in EXPECTED_HERO_IDS}
        actual = {name for name in members if name.endswith(".png")}
        if actual != expected:
            raise PortraitPackageError(
                f"Wrong ZIP portrait list: missing={sorted(expected - actual)}, "
                f"unexpected={sorted(actual - expected)}")
        if "manifest.json" not in members:
            raise PortraitPackageError("Missing manifest.json")
        manifest = json.loads(archive.read("manifest.json").decode("utf-8"))
        if set(manifest.get("ids", [])) != set(EXPECTED_HERO_IDS):
            raise PortraitPackageError("Manifest hero IDs don't match the project")
        if manifest.get("unity_resource_folder") != "Assets/_Game/Resources/HeroConcepts":
            raise PortraitPackageError("Unexpected portrait resource destination")

        # Validate every entry BEFORE writing anything. Reject unsafe paths and
        # invalid PNG headers, rather than half-installing a corrupted package.
        pictures = {}
        for name in sorted(expected):
            data = archive.read(name)
            png_size(data)
            destination = (repo_root / name).resolve()
            if not destination.is_relative_to(repo_root):
                raise PortraitPackageError(f"Unsafe archive path: {name}")
            if destination.exists() and not overwrite:
                raise PortraitPackageError(
                    f"Portrait already exists: {destination}; use --overwrite only after review")
            pictures[destination] = data

    for destination, data in pictures.items():
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
        print(f"Installed: {destination.relative_to(repo_root)}")

    print(f"Installed {len(pictures)} original Elite hero portrait PNGs.")
    print("Next: open Unity, inspect all ten Hero Altar cards and Hero Detail views,")
    print("then commit the PNGs and Unity-generated .meta files.")
    return len(pictures)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--zip", required=True, type=Path,
                        help="Path to downloaded shinobi_elite_portrait_pack.zip")
    parser.add_argument("--repo", type=Path,
                        default=Path(__file__).resolve().parents[2],
                        help="Root of the Shinobi Dominion repository")
    parser.add_argument("--overwrite", action="store_true",
                        help="Explicitly replace existing hero paintings")
    args = parser.parse_args(argv)
    try:
        return 0 if install(args.zip, args.repo, args.overwrite) == 10 else 1
    except (OSError, zipfile.BadZipFile, PortraitPackageError, ValueError) as exc:
        print(f"Portrait installation failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
