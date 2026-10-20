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


def pull(number, branch, fork=False):
    return {"number": number, "headRefName": branch, "isCrossRepository": fork}


class DecideTests(unittest.TestCase):
    def test_nothing_pending_lets_the_build_run(self):
        self.assertIsNone(update_gate.decide([pull(52, "interface-12.1.5")], [], "schedule"))

    def test_an_open_update_pr_holds_every_event(self):
        for event in ("schedule", "workflow_dispatch"):
            with self.subTest(event=event):
                reason = update_gate.decide([pull(54, "ci/update-v12.1.5-123")], [], event)
                self.assertIn("#54", reason)

    def test_a_fork_cannot_hold_the_build(self):
        """The repository is public, so anyone can open a PR from a branch named ci/update-."""
        self.assertIsNone(
            update_gate.decide([pull(60, "ci/update-x", fork=True)], [], "schedule")
        )

    def test_a_failed_build_holds_scheduled_runs(self):
        reason = update_gate.decide([], [{"number": 7}], "schedule")

        self.assertIn("#7", reason)

    def test_a_manual_run_overrides_a_failed_build(self):
        self.assertIsNone(update_gate.decide([], [{"number": 7}], "workflow_dispatch"))

    def test_a_manual_run_does_not_override_an_open_update_pr(self):
        reason = update_gate.decide(
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
