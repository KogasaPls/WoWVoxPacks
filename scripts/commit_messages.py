"""Parse Conventional Commit subjects and check only the requested new commit range."""

import argparse
import re
import subprocess
import sys
from typing import NamedTuple

SUBJECT = re.compile(
    r"(feat|fix|perf|build|ci|docs|style|refactor|test|chore|revert)"
    r"(?:\(([a-z][a-z0-9_-]*)\))?(!)?: (\S(?:.*\S)?)",
    re.IGNORECASE,
)


class CommitSubject(NamedTuple):
    kind: str
    scope: str | None
    description: str
    breaking: bool


def parse_subject(subject: str) -> CommitSubject | None:
    match = SUBJECT.fullmatch(subject)
    if match is None:
        return None
    kind, scope, breaking, description = match.groups()
    return CommitSubject(
        kind.lower(), scope.lower() if scope else None, description, bool(breaking)
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", required=True)
    parser.add_argument("--head", default="HEAD")
    args = parser.parse_args()
    result = subprocess.run(
        ["git", "log", "--no-merges", "--format=%h%x00%s", f"{args.base}..{args.head}"],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode:
        print(result.stderr.strip(), file=sys.stderr)
        return 2

    invalid = []
    for line in result.stdout.splitlines():
        sha, subject = line.split("\0", 1)
        if parse_subject(subject) is None:
            invalid.append(f"{sha}: {subject}")
    if invalid:
        print(
            "Use type(scope): description; see CONTRIBUTING.md.\n" + "\n".join(invalid),
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
