#!/usr/bin/env python3
"""Render player-facing changes to the addons distributed in a release."""

import argparse
import json
import re
import subprocess
from pathlib import Path

from commit_messages import parse_subject

ADDONS = {
    "bigwigs_voice": ("BigWigs Voice", "BigWigs_Voice_WoWVoxPacks"),
    "bigwigs_countdown": ("BigWigs Countdown", "BigWigs_Countdown_WoWVoxPacks"),
    "callouts": ("Callouts", "WoWVoxPacks_Callouts"),
    "nsrt": ("Northern Sky Raid Tools", "WoWVoxPacks_NorthernSkyRaidTools"),
    "exboss": ("ExBoss", "ExBoss_WoWVoxPacks"),
}

# A season's first sync moves hundreds of names at once, which is a changelog nobody reads.
LIST_LIMIT = 40


def git(*arguments: str, repository: Path | None = None) -> str:
    command = ["git"]
    if repository is not None:
        command += ["-C", str(repository)]
    command += list(arguments)
    result = subprocess.run(command, capture_output=True, text=True, check=False)
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


def packaged_addons(paths: list[str]) -> set[str]:
    """Match package directories, excluding files omitted by package.sh."""
    addons = set()
    for path in paths:
        parts = Path(path).parts
        if len(parts) < 4 or parts[0] != "output":
            continue
        if parts[-1].lower().endswith(".wav") or parts[-1].startswith(".wvp-"):
            continue
        for scope, (_, directory) in ADDONS.items():
            if parts[2] != f"{directory}_{parts[1]}":
                continue
            if parts[-1] == "SoundFiles.json" and scope != "nsrt":
                continue
            addons.add(scope)
    return addons


def changed_addons(previous: str | None, ref: str, repository: Path | None) -> set[str]:
    paths = (git("diff", "--name-only", f"{previous}..{ref}", "--", "output", repository=repository)
             if previous else git("ls-tree", "-r", "--name-only", ref, "--", "output", repository=repository))
    return packaged_addons(paths.splitlines())


def release_changes(previous: str | None, ref: str, repository: Path | None,
                    shipped: set[str]) -> list[str]:
    span = f"{previous}..{ref}" if previous else ref
    log = git("log", "--no-merges", "--format=%H%x00%s", span, repository=repository)
    changes = []
    for line in log.splitlines():
        sha, subject = line.split("\0", 1)
        commit = parse_subject(subject)
        if commit is None or commit.kind not in {"feat", "fix", "perf"}:
            continue
        paths = git("diff-tree", "--root", "--no-commit-id", "--name-only", "-r", sha,
                    "--", "output", repository=repository)
        affected = packaged_addons(paths.splitlines()) & shipped
        if commit.scope in ADDONS:
            affected &= {commit.scope}
        elif commit.scope not in (None, "packs"):
            continue
        if not affected:
            continue
        label = ADDONS[next(iter(affected))][0] if len(affected) == 1 else "Voice packs"
        breaking = " (breaking change)" if commit.breaking else ""
        changes.append(f"{label}{breaking}: {commit.description}")
    return list(dict.fromkeys(changes))


def registered_callouts(ref: str | None, repository: Path | None, scope: str) -> dict[str, str]:
    """Distributed names, keyed without case; None reads the builder's working tree."""
    keys = {}
    paths = (git("ls-tree", "-r", "--name-only", ref, "--", "output", repository=repository)
             if ref else git("ls-files", "--cached", "--others", "--exclude-standard",
                             "--", "output", repository=repository))
    for path in paths.splitlines():
        if not path.endswith("/Core.lua") or scope not in packaged_addons([path]):
            continue
        if ref:
            content = git("show", f"{ref}:{path}", repository=repository)
        else:
            source = (repository or Path.cwd()) / path
            if not source.exists():
                continue
            content = source.read_text(encoding="utf-8")
        for literal in re.findall(r'^LSM:Register\("sound", ("(?:[^"\\]|\\.)*"),', content, re.MULTILINE):
            key = re.sub(r"\|c[0-9a-fA-F]{8}|\|r", "", json.loads(literal))
            if scope == "callouts":
                key = re.sub(r"^WVP [^:]+: ", "", key)
            keys[key.casefold()] = key
    return keys


def shipped_callout_changes(previous: str | None, ref: str, repository: Path | None,
                               shipped: set[str]) -> tuple[list[str], list[str]]:
    if previous is None:
        return [], []
    added, removed = {}, {}
    for scope in shipped & {"nsrt", "callouts"}:
        before = registered_callouts(previous, repository, scope)
        after = registered_callouts(ref, repository, scope)
        added.update((key, after[key]) for key in after.keys() - before.keys())
        removed.update((key, before[key]) for key in before.keys() - after.keys())
    return sorted(added.values()), sorted(removed.values())


def commit_date(ref: str = "HEAD", repository: Path | None = None) -> str:
    return git("log", "-1", "--format=%cs", ref, repository=repository).strip()


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

    if commits:
        lines += [f"- {subject}" for subject in commits]
    elif not added and not removed:
        lines += ["- No user-facing addon changes."]

    for title, names in (("New callouts", added), ("Removed callouts", removed)):
        if names:
            if lines[-1]:
                lines.append("")
            lines += [f"### {title}", ""] + [f"- {name}" for name in names[:limit]]
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
    shipped = changed_addons(previous, args.ref, args.repository)
    added, removed = shipped_callout_changes(previous, args.ref, args.repository, shipped)

    print(
        render(
            args.tag,
            commit_date(args.ref, args.repository),
            release_changes(previous, args.ref, args.repository, shipped),
            added,
            removed,
            compare_url(args.repository_url, previous, args.tag),
        ),
        end="",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
