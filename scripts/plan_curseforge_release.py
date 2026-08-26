#!/usr/bin/env python3
"""Plan the CurseForge packages whose user-visible contents need publishing."""

import argparse
import json
from pathlib import Path
import sys
from typing import Sequence

import compare_addon_packages
import curseforge_packages


def archive_name(voice: str, addon: str, tag: str) -> str:
    return f"WoWVoxPacks_{voice}_{addon}_{tag}.zip"


def selected_archives(
    catalog: curseforge_packages.Catalog,
    tag: str,
    *,
    voices: Sequence[str] | None = None,
    addons: Sequence[str] | None = None,
) -> list[str]:
    selected_voices = _selection("voice", voices, catalog.voice_names)
    selected_addons = _selection("addon", addons, catalog.addon_names)
    return [
        archive_name(voice, addon, tag)
        for voice in selected_voices
        for addon in selected_addons
    ]


def plan_release(
    catalog: curseforge_packages.Catalog,
    current_tag: str,
    current_directory: Path,
    previous_tag: str | None = None,
    previous_directory: Path | None = None,
    *,
    voices: Sequence[str] | None = None,
    addons: Sequence[str] | None = None,
    force: bool = False,
) -> list[dict[str, str | int]]:
    selected_voices = _selection("voice", voices, catalog.voice_names)
    selected_addons = _selection("addon", addons, catalog.addon_names)
    packages = []

    for voice in selected_voices:
        for addon in selected_addons:
            current_archive = archive_name(voice, addon, current_tag)
            current_path = current_directory / current_archive
            if not current_path.is_file() or current_path.stat().st_size == 0:
                raise FileNotFoundError(
                    f"missing or empty current release asset: {current_archive}"
                )

            publish = force or previous_tag is None or previous_directory is None
            if not publish:
                previous_archive = archive_name(voice, addon, previous_tag)
                previous_path = previous_directory / previous_archive
                publish = not previous_path.is_file() or previous_path.stat().st_size == 0
                if not publish:
                    publish = compare_addon_packages.compare(
                        previous_path, current_path
                    ).changed

            if publish:
                packages.append({
                    "voice": voice,
                    "addon": addon,
                    "project_id": catalog.project_id(voice, addon),
                    "relations": catalog.relations(addon),
                    "archive": current_archive,
                })

    return packages


def _selection(
    kind: str, selected: Sequence[str] | None, available: tuple[str, ...]
) -> tuple[str, ...]:
    if selected is not None and (
        isinstance(selected, str) or not isinstance(selected, (list, tuple))
    ):
        raise ValueError(f"{kind} selection must be a JSON array")
    values = available if selected is None else tuple(selected)
    for value in values:
        if value not in available:
            raise ValueError(f"unknown {kind} '{value}'; choose from {', '.join(available)}")
    if len(values) != len(set(values)):
        raise ValueError(f"duplicate {kind} selection")
    return values


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=curseforge_packages.DEFAULT_CATALOG)
    parser.add_argument("--current-tag", required=True)
    parser.add_argument("--current-dir", type=Path)
    parser.add_argument("--previous-tag")
    parser.add_argument("--previous-dir", type=Path)
    parser.add_argument("--voices", type=json.loads, metavar="JSON")
    parser.add_argument("--addons", type=json.loads, metavar="JSON")
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--list-assets", action="store_true")
    return parser.parse_args()


def main() -> int:
    arguments = parse_args()
    try:
        catalog = curseforge_packages.load(arguments.catalog)
        if arguments.list_assets:
            for archive in selected_archives(
                catalog,
                arguments.current_tag,
                voices=arguments.voices,
                addons=arguments.addons,
            ):
                print(archive)
            return 0
        if arguments.current_dir is None:
            raise ValueError("--current-dir is required unless --list-assets is used")
        packages = plan_release(
            catalog,
            arguments.current_tag,
            arguments.current_dir,
            arguments.previous_tag,
            arguments.previous_dir,
            voices=arguments.voices,
            addons=arguments.addons,
            force=arguments.force,
        )
    except (OSError, ValueError, compare_addon_packages.PackageComparisonError) as error:
        print(f"Could not plan CurseForge release: {error}", file=sys.stderr)
        return 2
    print(json.dumps(packages, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
