#!/usr/bin/env python3
"""Resolve retail game versions to the IDs CurseForge's upload API takes.

A release for a patch CurseForge has not added yet must not upload: a file with no game version
is hidden from every player. Matching follows BigWigs' packager: the retail version type, and
the exact name.
"""

import argparse
import json
import os
import sys
import urllib.request

VERSIONS_URL = "https://wow.curseforge.com/api/game/versions"
RETAIL = 517
# Distinct from the 1 an uncaught error exits with, so a caller can wait on a missing version
# and still fail on a broken lookup.
MISSING = 3


def version_ids(wanted: list[str], listed: list[dict]) -> tuple[list[int], list[str]]:
    """The retail version IDs for the wanted names, and the names CurseForge does not list."""
    retail = {version["name"]: version["id"] for version in listed if version.get("gameVersionTypeID") == RETAIL}
    return [retail[name] for name in wanted if name in retail], [name for name in wanted if name not in retail]


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
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    wanted = [version for version in args.versions.split(",") if version]
    if not wanted:
        raise ValueError("no game versions to resolve")
    ids, missing = version_ids(wanted, fetch_listed(os.environ["CURSEFORGE_API_TOKEN"]))
    if missing:
        print(f"CurseForge does not list game version {', '.join(missing)} yet", file=sys.stderr)
        return MISSING
    print(json.dumps(ids))
    return 0


if __name__ == "__main__":
    sys.exit(main())
