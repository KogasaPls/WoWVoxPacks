#!/usr/bin/env python3
"""Move install_ffmpeg.sh to a newer BtbN build before BtbN deletes the pinned one.

BtbN keeps each month's last autobuild for 24 months and deletes the rest. A pin a year old is
moved to the last build of the newest complete month, which then has two years left, on the
same release branch when BtbN still builds it.
"""

import argparse
import datetime
import json
import re
import sys
from pathlib import Path

RELEASE_PREFIX = "https://github.com/BtbN/FFmpeg-Builds/releases/download/"
TAG = re.compile(r"autobuild-(\d{4})-(\d{2})-(\d{2})-(\d{2})-(\d{2})")
ASSET = re.compile(r"ffmpeg-n(\d+\.\d+)[^/]*-linux64-gpl-\1\.tar\.xz")
MAX_AGE = datetime.timedelta(days=365)


def pinned(script: str) -> tuple[str, str, str]:
    """The release tag, asset name and sha256 the script installs."""
    release = re.search(rf"^release={re.escape(RELEASE_PREFIX)}(\S+)$", script, re.MULTILINE)
    asset = re.search(r"^asset=(\S+)$", script, re.MULTILINE)
    sha256 = re.search(r"^sha256=([0-9a-f]{64})$", script, re.MULTILINE)
    if not (release and asset and sha256):
        raise ValueError("install_ffmpeg.sh no longer has its release, asset and sha256 lines")
    return release.group(1), asset.group(1), sha256.group(1)


def tag_date(tag: str) -> datetime.date:
    match = TAG.fullmatch(tag)
    if not match:
        raise ValueError(f"{tag} is not a dated autobuild")
    return datetime.date(int(match.group(1)), int(match.group(2)), int(match.group(3)))


def branch_of(asset: str) -> str:
    match = ASSET.fullmatch(asset + ".tar.xz") or ASSET.fullmatch(asset)
    if not match:
        raise ValueError(f"{asset} is not a linux64 gpl release-branch build")
    return match.group(1)


def month_end_builds(releases: list[dict], today: datetime.date) -> list[dict]:
    """The last autobuild of each complete month, newest first: the ones BtbN keeps."""
    newest_by_month: dict[tuple[int, int], dict] = {}
    for release in releases:
        if not TAG.fullmatch(release["tag_name"]):
            continue
        built = tag_date(release["tag_name"])
        if (built.year, built.month) >= (today.year, today.month):
            continue
        month = (built.year, built.month)
        if month not in newest_by_month or release["tag_name"] > newest_by_month[month]["tag_name"]:
            newest_by_month[month] = release
    return [newest_by_month[month] for month in sorted(newest_by_month, reverse=True)]


def choose(release: dict, branch: str) -> tuple[str, str] | None:
    """The asset to install from this release and its sha256: the same branch, else the newest."""
    builds = {}
    for asset in release["assets"]:
        match = ASSET.fullmatch(asset["name"])
        digest = asset.get("digest") or ""
        if match and digest.startswith("sha256:"):
            builds[match.group(1)] = (asset["name"].removesuffix(".tar.xz"), digest.removeprefix("sha256:"))
    if branch in builds:
        return builds[branch]
    if not builds:
        return None
    newest = max(builds, key=lambda name: tuple(int(part) for part in name.split(".")))
    return builds[newest]


def refresh(script: str, releases: list[dict], today: datetime.date) -> tuple[str, str] | None:
    """The rewritten script and its new tag, or None when the pin is young enough to keep."""
    tag, asset, _ = pinned(script)
    if today - tag_date(tag) < MAX_AGE:
        return None
    branch = branch_of(asset)
    for release in month_end_builds(releases, today):
        if tag_date(release["tag_name"]) <= tag_date(tag):
            break
        chosen = choose(release, branch)
        if chosen is None:
            continue
        new_asset, sha256 = chosen
        script = re.sub(r"^release=\S+$", f"release={RELEASE_PREFIX}{release['tag_name']}", script,
                        count=1, flags=re.MULTILINE)
        script = re.sub(r"^asset=\S+$", f"asset={new_asset}", script, count=1, flags=re.MULTILINE)
        script = re.sub(r"^sha256=\S+$", f"sha256={sha256}", script, count=1, flags=re.MULTILINE)
        return script, release["tag_name"]
    raise ValueError(f"the pin {tag} is a year old and BtbN has no newer month-end linux64 gpl build")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--script", type=Path, required=True)
    parser.add_argument("--releases", type=Path, required=True, help="BtbN's releases as the GitHub API lists them")
    parser.add_argument("--today", type=datetime.date.fromisoformat, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    result = refresh(
        args.script.read_text(encoding="utf-8"),
        json.loads(args.releases.read_text(encoding="utf-8")),
        args.today,
    )
    if result is not None:
        script, tag = result
        args.script.write_text(script, encoding="utf-8")
        print(tag)
    return 0


if __name__ == "__main__":
    sys.exit(main())
