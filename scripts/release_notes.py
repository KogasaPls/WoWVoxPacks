#!/usr/bin/env python3
"""Render a release's notes: the commits it carries and the callouts they changed."""

import argparse
import subprocess
from pathlib import Path

VOCABULARY_FILES = (
    "nsrt-vocabulary.txt",
    "nsrt-alert-vocabulary.txt",
    "lorrgs-vocabulary.txt",
)

# A season's first sync moves hundreds of names at once, which is a changelog nobody reads.
LIST_LIMIT = 40


def git(*arguments: str, repository: Path | None = None) -> str:
    command = ["git"]
    if repository is not None:
        command += ["-C", str(repository)]
    command += list(arguments)
    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(f"{' '.join(command)} failed: {result.stderr.strip()}")
    return result.stdout


def previous_tag(ref: str = "HEAD", repository: Path | None = None,
                 tag: str | None = None) -> str | None:
    """The newest release tag before ref, or None when no release precedes it."""
    # create-release generates the notes before the release creates its tag, so ref is
    # normally untagged. A rerun on `push: tags` has the tag already, and describing from
    # ref itself would return the tag being released and span nothing. Only that tag is
    # stepped over: another release sitting on this commit is a real previous release, and
    # skipping it would report its commits a second time.
    described = ref
    if tag and tag in git("tag", "--points-at", ref, repository=repository).split():
        described = f"{ref}^"
    try:
        return git(
            "describe", "--tags", "--abbrev=0", "--match", "v*", described,
            repository=repository,
        ).strip()
    except RuntimeError:
        return None


def commit_subjects(previous: str | None, ref: str = "HEAD",
                    repository: Path | None = None) -> list[str]:
    span = f"{previous}..{ref}" if previous else ref
    log = git("log", "--no-merges", "--pretty=format:%s", span, repository=repository)
    return [line.strip() for line in log.splitlines() if line.strip()]


def commit_date(ref: str = "HEAD", repository: Path | None = None) -> str:
    return git("log", "-1", "--format=%cs", ref, repository=repository).strip()


def parse_vocabulary_diff(diff: str) -> tuple[list[str], list[str]]:
    added: list[str] = []
    removed: list[str] = []

    for line in diff.splitlines():
        if line.startswith(("+++", "---")):
            continue
        if line.startswith("+"):
            entry, target = line[1:].strip(), added
        elif line.startswith("-"):
            entry, target = line[1:].strip(), removed
        else:
            continue
        if entry and not entry.startswith("#"):
            target.append(entry)

    # A name that only moved between vocabulary files is in both lists and is not news.
    moved = set(added) & set(removed)
    return (
        sorted({entry for entry in added if entry not in moved}),
        sorted({entry for entry in removed if entry not in moved}),
    )


def vocabulary_changes(previous: str | None, ref: str = "HEAD",
                       repository: Path | None = None,
                       files: tuple[str, ...] = VOCABULARY_FILES) -> tuple[list[str], list[str]]:
    if not previous:
        return [], []
    diff = git(
        "diff", "--unified=0", f"{previous}..{ref}", "--", *files, repository=repository
    )
    return parse_vocabulary_diff(diff)


def compare_url(repository_url: str | None, previous: str | None, tag: str) -> str | None:
    if not repository_url:
        return None
    if previous:
        return f"{repository_url}/compare/{previous}...{tag}"
    return f"{repository_url}/commits/{tag}"


def render(tag: str, date: str, commits: list[str], added: list[str], removed: list[str],
           url: str | None = None, limit: int = LIST_LIMIT) -> str:
    lines = [f"## {tag} ({date})", ""]
    if url:
        lines += [f"[Full Changelog]({url})", ""]

    lines += [f"- {subject}" for subject in commits] or ["- No changes since the previous release."]

    for title, names in (("New callouts", added), ("Removed callouts", removed)):
        if names:
            lines += ["", f"### {title}", ""] + [f"- {name}" for name in names[:limit]]
            if len(names) > limit:
                lines.append(f"- ... and {len(names) - limit} more")

    return "\n".join(lines).strip() + "\n"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--ref", default="HEAD")
    parser.add_argument("--previous", default="")
    parser.add_argument("--repository-url", default="")
    parser.add_argument("--repository", type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    previous = args.previous or previous_tag(args.ref, args.repository, args.tag)
    added, removed = vocabulary_changes(previous, args.ref, args.repository)

    print(
        render(
            args.tag,
            commit_date(args.ref, args.repository),
            commit_subjects(previous, args.ref, args.repository),
            added,
            removed,
            compare_url(args.repository_url, previous, args.tag),
        ),
        end="",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
