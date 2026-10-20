#!/usr/bin/env python3
"""Decide whether the update workflow may run its paid build."""

import argparse
import json
import re
from pathlib import Path

UPDATE_BRANCH_PREFIX = "ci/update-"
FAILURE_LABEL = "update-build-failed"


def decide(
    pull_requests: list[dict],
    failure_issues: list[dict],
    event_name: str,
    run_sha: str,
    main_sha: str,
) -> str | None:
    """The reason the build must wait, or None when it may run."""
    for name, sha in (("run", run_sha), ("main", main_sha)):
        if not re.fullmatch(r"[0-9a-f]{40}", sha):
            raise ValueError(f"the {name} commit {sha!r} is not a full commit hash")
    pending = [
        f"#{pull_request['number']}"
        for pull_request in pull_requests
        if pull_request["headRefName"].startswith(UPDATE_BRANCH_PREFIX)
        and not pull_request["isCrossRepository"]
    ]
    if pending:
        return f"Update PR {' '.join(pending)} is still open; merge or close it to build again."
    # A run builds the commit it was triggered on. Once main has moved, the merge it is waiting
    # behind may already have rendered what this run would render again.
    if run_sha != main_sha:
        return (
            f"This run's commit {run_sha[:9]} is not main's head {main_sha[:9]}; only a run of "
            "main's current head builds."
        )
    if failure_issues and event_name != "workflow_dispatch":
        return (
            f"Issue #{failure_issues[0]['number']} records a failed build; close it, or run this "
            "workflow by hand, which reuses the renders the failed build kept."
        )
    return None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pulls", type=Path, required=True, help="gh pr list --json output")
    parser.add_argument("--issues", type=Path, required=True, help="gh issue list --json output")
    parser.add_argument("--event", required=True)
    parser.add_argument("--run-sha", required=True, help="the commit this run checks out")
    parser.add_argument("--main-sha", required=True, help="the default branch's head now")
    parser.add_argument("--output", type=Path, required=True, help="the step's GITHUB_OUTPUT file")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    reason = decide(
        json.loads(args.pulls.read_text(encoding="utf-8")),
        json.loads(args.issues.read_text(encoding="utf-8")),
        args.event,
        args.run_sha,
        args.main_sha,
    )
    if reason is not None:
        print(f"::warning::{reason}")
    with args.output.open("a", encoding="utf-8") as output:
        output.write(f"blocked={'true' if reason is not None else 'false'}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
