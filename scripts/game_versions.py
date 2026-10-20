#!/usr/bin/env python3
"""Report the game versions the addons declare, spelled the way CurseForge names them."""

import argparse
import json
import re
from pathlib import Path


def to_game_version(interface: str) -> str:
    digits = interface.strip()
    if not digits.isdigit() or len(digits) < 5:
        raise ValueError(f"{interface!r} is not a toc interface number")
    return f"{int(digits[:-4])}.{int(digits[-4:-2])}.{int(digits[-2:])}"


def to_interface(build: str) -> str:
    parts = build.strip().split(".")
    if len(parts) != 4 or not all(part.isdigit() for part in parts):
        raise ValueError(f"{build!r} is not a client build such as 12.1.5.70448")
    major, minor, patch = (int(part) for part in parts[:3])
    if minor > 99 or patch > 99:
        raise ValueError(f"{build!r} does not fit a toc interface number")
    return f"{major}{minor:02d}{patch:02d}"


def live_build(versions: str, region: str = "us") -> str:
    """Reads a TACT versions table, as served by us.version.battle.net/v2/products/wow/versions."""
    rows = [
        line.split("|")
        for line in versions.splitlines()
        if line.strip() and not line.startswith("#")
    ]
    if not rows:
        raise ValueError("the versions table is empty")
    columns = [column.split("!", 1)[0] for column in rows[0]]
    if "Region" not in columns or "VersionsName" not in columns:
        raise ValueError(f"the versions table has no Region and VersionsName columns: {columns}")
    matches = [row for row in rows[1:] if row[columns.index("Region")] == region]
    if len(matches) != 1:
        raise ValueError(f"expected one {region} row in the versions table, found {len(matches)}")
    build = matches[0][columns.index("VersionsName")].strip()
    to_interface(build)
    return build


def declared_interfaces(settings: Path) -> list[str]:
    parsed = json.loads(settings.read_text(encoding="utf-8"))
    interfaces = parsed.get("AddOn", {}).get("Interfaces")
    if not interfaces:
        raise ValueError(f"{settings} declares no AddOn.Interfaces")
    return interfaces


def game_versions(settings: Path) -> list[str]:
    versions = {to_game_version(interface) for interface in declared_interfaces(settings)}
    return sorted(versions, key=lambda version: [int(part) for part in version.split(".")])


def replace_once(pattern: str, replacement: str, text: str) -> str:
    replaced, count = re.subn(pattern, replacement, text)
    if count != 1:
        raise ValueError(f"expected one match for {pattern!r} in the settings, found {count}")
    return replaced


def follow(settings: Path, build: str) -> str | None:
    """Rewrites the two values in place so the rest of the settings keep their formatting."""
    interface = to_interface(build)
    if int(interface) <= max(int(declared) for declared in declared_interfaces(settings)):
        return None

    version = to_game_version(interface)
    text = settings.read_text(encoding="utf-8")
    text = replace_once(r'"Interfaces": \[[^\]]*\]', f'"Interfaces": [ "{interface}" ]', text)
    text = replace_once(r'"Version": "[^"]*"', f'"Version": "{version}"', text)
    settings.write_text(text, encoding="utf-8")
    return version


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--settings", type=Path)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--highest", action="store_true")
    mode.add_argument(
        "--follow",
        metavar="BUILD",
        help="move the settings to this live client build and print the new game version",
    )
    mode.add_argument(
        "--live-build",
        type=Path,
        metavar="VERSIONS",
        help="print the us build named by this TACT versions table",
    )
    args = parser.parse_args()
    if args.live_build is None and args.settings is None:
        parser.error("--settings is required")
    return args


def main() -> int:
    args = parse_args()
    if args.live_build is not None:
        print(live_build(args.live_build.read_text(encoding="utf-8")))
        return 0

    if args.follow is not None:
        version = follow(args.settings, args.follow)
        if version is not None:
            print(version)
        return 0

    versions = game_versions(args.settings)
    print(versions[-1] if args.highest else ",".join(versions))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
