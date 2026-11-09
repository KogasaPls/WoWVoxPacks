#!/usr/bin/env python3
"""Plan the CurseForge packages whose user-visible contents need publishing."""

import argparse
import json
from pathlib import Path
import sys
from typing import Sequence

import compare_addon_packages
import curseforge_packages


def archive_name(voice: str, addon: str, tag: str, flavor: str = "retail") -> str:
    suffix = "_Forever" if flavor == "forever" else ""
    return f"WoWVoxPacks_{voice}_{addon}{suffix}_{tag}.zip"


def selected_flavors(
    catalog: curseforge_packages.Catalog,
    *,
    voices: Sequence[str] | None = None,
    addons: Sequence[str] | None = None,
    flavors: Sequence[str] | None = None,
) -> tuple[str, ...]:
    present = {
        flavor
        for _, _, flavor in _selected_files(
            catalog, voices=voices, addons=addons, flavors=flavors
        )
    }
    return tuple(flavor for flavor in curseforge_packages.FLAVORS if flavor in present)


def selected_archives(
    catalog: curseforge_packages.Catalog,
    tag: str,
    *,
    voices: Sequence[str] | None = None,
    addons: Sequence[str] | None = None,
    flavors: Sequence[str] | None = None,
) -> list[str]:
    return [
        archive_name(voice, addon, tag, flavor)
        for voice, addon, flavor in _selected_files(
            catalog, voices=voices, addons=addons, flavors=flavors
        )
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
    flavors: Sequence[str] | None = None,
    force: bool = False,
) -> list[dict[str, str | int]]:
    packages = []

    for voice, addon, flavor in _selected_files(
        catalog, voices=voices, addons=addons, flavors=flavors
    ):
        current_archive = archive_name(voice, addon, current_tag, flavor)
        current_path = current_directory / current_archive
        if not current_path.is_file() or current_path.stat().st_size == 0:
            raise FileNotFoundError(
                f"missing or empty current release asset: {current_archive}"
            )

        publish = force or previous_tag is None or previous_directory is None
        if not publish:
            previous_archive = archive_name(voice, addon, previous_tag, flavor)
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
                "flavor": flavor,
            })

    return packages


def _selected_files(
    catalog: curseforge_packages.Catalog,
    *,
    voices: Sequence[str] | None = None,
    addons: Sequence[str] | None = None,
    flavors: Sequence[str] | None = None,
) -> list[tuple[str, str, str]]:
    selected_voices = _selection("voice", voices, catalog.voice_names)
    selected_addons = _selection("addon", addons, catalog.addon_names)
    selected_flavors = _selection("flavor", flavors, curseforge_packages.FLAVORS)
    return [
        (voice, addon, flavor)
        for voice in selected_voices
        for addon in selected_addons
        for flavor in catalog.flavors(addon)
        if flavor in selected_flavors
    ]


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
    parser.add_argument("--flavors", type=json.loads, metavar="JSON")
    parser.add_argument("--force", action="store_true")
    listing = parser.add_mutually_exclusive_group()
    listing.add_argument("--list-assets", action="store_true")
    listing.add_argument("--list-flavors", action="store_true")
    return parser.parse_args()


def main() -> int:
    arguments = parse_args()
    try:
        catalog = curseforge_packages.load(arguments.catalog)
        if arguments.list_flavors:
            for flavor in selected_flavors(
                catalog,
                voices=arguments.voices,
                addons=arguments.addons,
                flavors=arguments.flavors,
            ):
                print(flavor)
            return 0
        if arguments.list_assets:
            for archive in selected_archives(
                catalog,
                arguments.current_tag,
                voices=arguments.voices,
                addons=arguments.addons,
                flavors=arguments.flavors,
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
            flavors=arguments.flavors,
            force=arguments.force,
        )
    except (OSError, ValueError, compare_addon_packages.PackageComparisonError) as error:
        print(f"Could not plan CurseForge release: {error}", file=sys.stderr)
        return 2
    print(json.dumps(packages, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
