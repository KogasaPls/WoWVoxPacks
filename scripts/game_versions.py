#!/usr/bin/env python3
"""Report the game versions the addons declare, spelled the way CurseForge names them."""

import argparse
import json
from pathlib import Path


def to_game_version(interface: str) -> str:
    digits = interface.strip()
    if not digits.isdigit() or len(digits) < 5:
        raise ValueError(f"{interface!r} is not a toc interface number")
    return f"{int(digits[:-4])}.{int(digits[-4:-2])}.{int(digits[-2:])}"


def game_versions(settings: Path) -> list[str]:
    parsed = json.loads(settings.read_text(encoding="utf-8"))
    interfaces = parsed.get("AddOn", {}).get("Interfaces")
    if not interfaces:
        raise ValueError(f"{settings} declares no AddOn.Interfaces")

    versions = {to_game_version(interface) for interface in interfaces}
    return sorted(versions, key=lambda version: [int(part) for part in version.split(".")])


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--settings", type=Path, required=True)
    parser.add_argument("--highest", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    versions = game_versions(args.settings)
    print(versions[-1] if args.highest else ",".join(versions))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
