#!/usr/bin/env python3
"""Point the generated TOCs at a new addon version without reformatting the settings."""

import argparse
import json
from pathlib import Path


def read_version(settings: Path) -> str:
    parsed = json.loads(settings.read_text(encoding="utf-8"))
    version = parsed.get("AddOn", {}).get("Version")
    if not isinstance(version, str) or not version.strip():
        raise ValueError(f"{settings} has no AddOn.Version to replace")
    return version


def set_version(settings: Path, version: str) -> bool:
    version = version.lstrip("v")
    if not version.strip():
        raise ValueError("refusing to write an empty version")

    current = read_version(settings)
    if current == version:
        return False

    text = settings.read_text(encoding="utf-8")
    old = f'"Version": "{current}"'
    if text.count(old) != 1:
        raise ValueError(
            f"{settings} spells {old} {text.count(old)} times; "
            "rewriting it in place would be a guess"
        )

    settings.write_text(
        text.replace(old, f'"Version": "{version}"'), encoding="utf-8"
    )
    if read_version(settings) != version:
        raise ValueError(f"{settings} still reads {read_version(settings)}")
    return True


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--settings", type=Path, required=True)
    parser.add_argument("--version", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if set_version(args.settings, args.version):
        print(f"Addon version set to {args.version.lstrip('v')}")
    else:
        print(f"Addon version already {args.version.lstrip('v')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
