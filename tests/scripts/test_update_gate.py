import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPOSITORY_ROOT / "scripts" / "update_gate.py"

spec = importlib.util.spec_from_file_location("update_gate", SCRIPT_PATH)
update_gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(update_gate)


HEAD = "a" * 40


def decide(pull_requests, failure_issues, event, run_sha=HEAD, main_sha=HEAD):
    return update_gate.decide(pull_requests, failure_issues, event, run_sha, main_sha)


def pull(number, branch, fork=False):
    return {"number": number, "headRefName": branch, "isCrossRepository": fork}


class DecideTests(unittest.TestCase):
    def test_nothing_pending_lets_the_build_run(self):
        self.assertIsNone(decide([pull(52, "interface-12.1.5")], [], "schedule"))

    def test_an_open_update_pr_holds_every_event(self):
        for event in ("schedule", "workflow_dispatch"):
            with self.subTest(event=event):
                reason = decide([pull(54, "ci/update-v12.1.5-123")], [], event)
                self.assertIn("#54", reason)

    def test_a_fork_cannot_hold_the_build(self):
        """The repository is public, so anyone can open a PR from a branch named ci/update-."""
        self.assertIsNone(
            decide([pull(60, "ci/update-x", fork=True)], [], "schedule")
        )

    def test_a_failed_build_holds_scheduled_runs(self):
        reason = decide([], [{"number": 7}], "schedule")

        self.assertIn("#7", reason)

    def test_a_manual_run_overrides_a_failed_build(self):
        self.assertIsNone(decide([], [{"number": 7}], "workflow_dispatch"))

    def test_a_run_whose_commit_main_has_left_waits_for_the_next(self):
        """Its build would start from before a merge that may have rendered the same files."""
        for event in ("schedule", "workflow_dispatch"):
            with self.subTest(event=event):
                reason = decide([], [], event, run_sha="a" * 40, main_sha="b" * 40)
                self.assertIn("is not main's head", reason)

    def test_a_lookup_that_found_no_commit_fails_instead_of_holding_the_build(self):
        """A scheduled run has no event payload, so a lookup keyed on one comes back empty."""
        for main_sha in ("", "main", "HEAD"):
            with self.subTest(main_sha=main_sha), self.assertRaises(ValueError):
                decide([], [], "schedule", main_sha=main_sha)

    def test_a_manual_run_does_not_override_an_open_update_pr(self):
        reason = decide(
            [pull(54, "ci/update-v12.1.5-123")], [{"number": 7}], "workflow_dispatch"
        )

        self.assertIn("#54", reason)


class CommandLineTests(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.directory = Path(self.temporary_directory.name)
        self.output = self.directory / "github_output"
        self.output.write_text("earlier=1\n", encoding="utf-8")

    def tearDown(self):
        self.temporary_directory.cleanup()

    def run_gate(self, pulls, issues, event="schedule"):
        (self.directory / "pulls.json").write_text(json.dumps(pulls), encoding="utf-8")
        (self.directory / "issues.json").write_text(json.dumps(issues), encoding="utf-8")
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPT_PATH),
                "--pulls",
                str(self.directory / "pulls.json"),
                "--issues",
                str(self.directory / "issues.json"),
                "--event",
                event,
                "--run-sha",
                HEAD,
                "--main-sha",
                HEAD,
                "--output",
                str(self.output),
            ],
            capture_output=True,
            text=True,
        )

    def test_a_held_build_warns_and_appends_blocked_true(self):
        result = self.run_gate([pull(54, "ci/update-v12.1.5-123")], [])

        self.assertEqual(result.returncode, 0)
        self.assertTrue(result.stdout.startswith("::warning::Update PR #54"))
        self.assertEqual(self.output.read_text(encoding="utf-8"), "earlier=1\nblocked=true\n")

    def test_a_free_build_appends_blocked_false_silently(self):
        result = self.run_gate([], [])

        self.assertEqual(result.returncode, 0)
        self.assertEqual(result.stdout, "")
        self.assertEqual(self.output.read_text(encoding="utf-8"), "earlier=1\nblocked=false\n")

    def test_unreadable_listings_fail_without_deciding(self):
        """The workflow skips the build when this job fails, so an error must not say false."""
        result = self.run_gate([{"number": 54}], [])

        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.output.read_text(encoding="utf-8"), "earlier=1\n")


if __name__ == "__main__":
    unittest.main()
