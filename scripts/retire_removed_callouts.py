#!/usr/bin/env python3
"""Preserve shipped callout keys removed from tracked upstream vocabularies."""

import argparse
import json
from pathlib import Path
from typing import Iterable


def load_vocabulary(path: Path) -> set[str]:
    return {
        line
        for raw_line in path.read_text(encoding="utf-8").splitlines()
        if (line := raw_line.strip()) and not line.startswith("#")
    }


def load_retired(path: Path) -> set[str]:
    if not path.exists():
        return set()

    parsed = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(parsed, list) or not all(isinstance(name, str) for name in parsed):
        raise ValueError(f"{path} must contain a JSON array of strings")
    return set(parsed)


def retire_removed(
    vocabulary_pairs: Iterable[tuple[Path, Path]],
    retired_path: Path,
    max_removed_per_source: int = 5,
) -> list[str]:
    removals = []
    for old_path, new_path in vocabulary_pairs:
        removed = sorted(load_vocabulary(old_path) - load_vocabulary(new_path))
        if len(removed) > max_removed_per_source:
            raise ValueError(
                f"{old_path} removed {len(removed)} callouts; refusing to retire more than "
                f"{max_removed_per_source} automatically"
            )
        removals.extend(removed)

    retired = sorted(load_retired(retired_path).union(removals))
    retired_path.write_text(
        json.dumps(retired, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return retired


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--vocabulary",
        action="append",
        nargs=2,
        metavar=("OLD", "NEW"),
        required=True,
        help="tracked and replacement vocabulary paths; may be repeated",
    )
    parser.add_argument("--retired", type=Path, required=True)
    parser.add_argument("--max-removed-per-source", type=int, default=5)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    pairs = [(Path(old), Path(new)) for old, new in args.vocabulary]
    before = load_retired(args.retired)
    retired = retire_removed(pairs, args.retired, args.max_removed_per_source)

    for name in retired:
        if name not in before:
            print(f"Retiring removed callout: {name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
