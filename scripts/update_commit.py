"""Describe an automatic update using its rendered addon changes."""

import argparse
from pathlib import Path

from release_notes import ADDONS, git, packaged_addons, registered_callouts


def callout_list(names: list[str]) -> str:
    displayed = ", ".join(" ".join(name.split()) for name in names[:3])
    if len(names) > 3:
        displayed += f" and {len(names) - 3} more"
    return f"{displayed} callout{'s' if len(names) != 1 else ''}"


def subject(repository: Path, bigwigs_version: str, game_version: str) -> str:
    paths = git("diff", "--name-only", "HEAD", "--", "output", repository=repository)
    paths += git(
        "ls-files",
        "--others",
        "--exclude-standard",
        "--",
        "output",
        repository=repository,
    )
    changed_paths = paths.splitlines()
    affected = packaged_addons(changed_paths)
    ability_sounds_changed = any(
        path.endswith(".ogg") and "bigwigs_voice" in packaged_addons([path])
        for path in changed_paths
    )
    scope = next(iter(affected)) if len(affected) == 1 else "packs"
    descriptions = []
    kind = "fix"
    for addon in sorted(affected):
        details = []
        if addon in {"nsrt", "callouts"}:
            before = registered_callouts("HEAD", repository, addon)
            after = registered_callouts(None, repository, addon)
            added = sorted(after[key] for key in after.keys() - before.keys())
            removed = sorted(before[key] for key in before.keys() - after.keys())
            if added:
                kind = "feat"
                details.append(f"add {callout_list(added)}")
            if removed:
                details.append(f"remove {callout_list(removed)}")
        if addon == "bigwigs_voice" and bigwigs_version and ability_sounds_changed:
            kind = "feat"
            details.append(f"sync ability callouts for BigWigs Voice {bigwigs_version}")
        if details:
            prefix = f"{ADDONS[addon][0]}: " if len(affected) > 1 else ""
            descriptions.append(prefix + "; ".join(details))
    if affected and game_version:
        descriptions.append(f"support WoW {game_version}")
    if not descriptions:
        if not affected:
            return "chore(packs): update voice pack build inputs"
        label = ADDONS[scope][0] if scope != "packs" else "voice pack"
        return f"chore({scope}): rebuild {label} recordings from tracked settings"
    return f"{kind}({scope}): {'; '.join(descriptions)}"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", type=Path, default=Path.cwd())
    parser.add_argument("--bigwigs-version", default="")
    parser.add_argument("--game-version", default="")
    args = parser.parse_args()
    print(
        subject(
            args.repository,
            " ".join(args.bigwigs_version.split()),
            " ".join(args.game_version.split()),
        )
    )


if __name__ == "__main__":
    main()
