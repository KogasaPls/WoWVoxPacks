#!/usr/bin/env python3
"""Keep the output a failed update build already paid for, and put it back for the retry."""

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

OUTPUT = "output"
TEMPORARY_PREFIX = ".wvp-"


def git(*arguments: str, check: bool = True) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *arguments], check=check, capture_output=True, text=True)


def inside_output(path: Path) -> Path:
    if Path(OUTPUT).resolve() not in path.resolve().parents:
        raise ValueError(f"{path} is outside {OUTPUT}/")
    return path


def changed_paths() -> tuple[list[str], list[str]]:
    """The output files the build wrote or created, and the ones it deleted."""
    status = git("status", "--porcelain=v1", "-z", "--untracked-files=all", "--", OUTPUT).stdout
    written, deleted = [], []
    for entry in filter(None, status.split("\0")):
        code, path = entry[:2], entry[3:]
        if Path(path).name.startswith(TEMPORARY_PREFIX):
            continue
        (deleted if "D" in code else written).append(path)
    return sorted(written), sorted(deleted)


def collect(into: Path) -> None:
    written, deleted = changed_paths()
    into.mkdir(parents=True, exist_ok=True)
    for path in written:
        target = into / "files" / path
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
    (into / "deleted.txt").write_text("".join(f"{path}\n" for path in deleted), encoding="utf-8")
    (into / "base-sha").write_text(git("rev-parse", "HEAD").stdout.strip() + "\n", encoding="utf-8")
    print(f"Kept {len(written)} written and {len(deleted)} deleted output files")


def restore(source: Path) -> None:
    """Applies the kept output only onto the output it was built from."""
    base = (source / "base-sha").read_text(encoding="utf-8").strip()
    if git("cat-file", "-e", f"{base}^{{commit}}", check=False).returncode != 0:
        print(f"Not restoring: {base} is not in this checkout")
        return
    if git("diff", "--quiet", base, "HEAD", "--", OUTPUT, check=False).returncode != 0:
        print(f"Not restoring: output changed on main after {base}")
        return

    files = source / "files"
    written = [path for path in files.rglob("*") if path.is_file()] if files.is_dir() else []
    for path in written:
        target = inside_output(path.relative_to(files))
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)

    deleted = (source / "deleted.txt").read_text(encoding="utf-8").splitlines()
    for path in deleted:
        inside_output(Path(path)).unlink(missing_ok=True)
    print(f"Restored {len(written)} written and {len(deleted)} deleted output files from {base}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("collect").add_argument("--into", type=Path, required=True)
    commands.add_parser("restore").add_argument("--from", dest="source", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.command == "collect":
        collect(args.into)
    else:
        restore(args.source)
    return 0


if __name__ == "__main__":
    sys.exit(main())
