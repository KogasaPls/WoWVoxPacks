#!/usr/bin/env python3
"""Compare the user-visible contents of two packaged addon ZIP files."""

import argparse
from dataclasses import dataclass
import re
import sys
import zipfile
from pathlib import Path


TOC_VERSION_LINE = re.compile(br"(?m)^## Version:[^\r\n]*(?:\r?\n|$)")


class PackageComparisonError(RuntimeError):
    pass


@dataclass(frozen=True)
class PackageChanges:
    added: tuple[str, ...]
    removed: tuple[str, ...]
    modified: tuple[str, ...]

    @property
    def changed(self) -> bool:
        return bool(self.added or self.removed or self.modified)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("previous", type=Path)
    parser.add_argument("current", type=Path)
    return parser.parse_args()


def file_contents(path: Path) -> dict[str, bytes]:
    with zipfile.ZipFile(path) as archive:
        contents = {}
        for info in archive.infolist():
            if info.is_dir():
                continue
            if info.filename in contents:
                raise PackageComparisonError(f"duplicate member {info.filename}")
            content = archive.read(info)
            if info.filename.lower().endswith(".toc"):
                content = TOC_VERSION_LINE.sub(b"", content)
            contents[info.filename] = content
        return contents


def compare(previous_path: Path, current_path: Path) -> PackageChanges:
    try:
        previous = file_contents(previous_path)
        current = file_contents(current_path)
    except (OSError, zipfile.BadZipFile, PackageComparisonError) as error:
        raise PackageComparisonError(str(error)) from error
    return PackageChanges(
        tuple(sorted(current.keys() - previous.keys())),
        tuple(sorted(previous.keys() - current.keys())),
        tuple(sorted(
            name
            for name in previous.keys() & current.keys()
            if previous[name] != current[name]
        )),
    )


def main() -> int:
    args = parse_args()
    try:
        changes = compare(args.previous, args.current)
    except (OSError, zipfile.BadZipFile, PackageComparisonError) as error:
        print(f"Could not compare addon packages: {error}", file=sys.stderr)
        return 2
    if not changes.changed:
        print("unchanged")
        return 0

    print("changed")
    for label, names in (
        ("added", changes.added),
        ("removed", changes.removed),
        ("modified", changes.modified),
    ):
        for name in names:
            print(f"{label}: {name}")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
