#!/usr/bin/env python3
"""Resolve retail or Forever game versions to the IDs CurseForge's upload API takes.

A release for a patch CurseForge has not added yet must not upload: a file with no game version
is hidden from every player. Matching follows BigWigs' packager: the flavor's version type, and
the exact name.
"""

import argparse
import json
import os
import sys
import urllib.request

VERSIONS_URL = "https://wow.curseforge.com/api/game/versions"
RETAIL = 517
FOREVER = 88568
TYPES = {"retail": RETAIL, "forever": FOREVER}
# Distinct from the 1 an uncaught error exits with, so a caller can wait on a missing version
# and still fail on a broken lookup.
MISSING = 3


def version_ids(
    wanted: list[str], listed: list[dict], type_id: int = RETAIL
) -> tuple[list[int], list[str]]:
    """The version IDs of one type for the wanted names, and the names CurseForge does not list."""
    named = {version["name"]: version["id"] for version in listed if version.get("gameVersionTypeID") == type_id}
    return [named[name] for name in wanted if name in named], [name for name in wanted if name not in named]


def fetch_listed(token: str) -> list[dict]:
    request = urllib.request.Request(VERSIONS_URL, headers={"X-Api-Token": token})
    with urllib.request.urlopen(request, timeout=30) as response:
        listed = json.load(response)
    if not isinstance(listed, list) or not listed:
        raise ValueError("CurseForge returned no game versions")
    return listed


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--versions", required=True, help="comma-separated game versions, e.g. 12.1.5")
    parser.add_argument("--type", choices=tuple(TYPES), default="retail")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    wanted = [version for version in args.versions.split(",") if version]
    if not wanted:
        raise ValueError("no game versions to resolve")
    ids, missing = version_ids(
        wanted, fetch_listed(os.environ["CURSEFORGE_API_TOKEN"]), TYPES[args.type]
    )
    if missing:
        print(
            f"CurseForge does not list {args.type} game version {', '.join(missing)} yet",
            file=sys.stderr,
        )
        return MISSING
    print(json.dumps(ids))
    return 0


if __name__ == "__main__":
    sys.exit(main())
