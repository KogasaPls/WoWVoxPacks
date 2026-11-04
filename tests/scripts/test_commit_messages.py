import os
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/commit_messages.py"


class CommitRangeTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)
        self.git("init", "-q", "-b", "main")
        self.git("config", "user.name", "Test")
        self.git("config", "user.email", "test@example.invalid")
        self.git("config", "commit.gpgsign", "false")
        self.commit("Add initial voice-pack recordings")
        self.base = self.git("rev-parse", "HEAD").strip()

    def git(self, *arguments):
        return subprocess.check_output(
            ["git", *arguments], cwd=self.directory, text=True
        )

    def commit(self, subject):
        self.git("commit", "--allow-empty", "-q", "-m", subject)

    def check(self):
        return subprocess.run(
            [sys.executable, str(SCRIPT), "--base", self.base, "--head", "HEAD"],
            cwd=self.directory,
            capture_output=True,
            text=True,
            check=False,
        )

    def install_hook(self):
        scripts = self.directory / "scripts"
        scripts.mkdir()
        (scripts / SCRIPT.name).write_text(SCRIPT.read_text())
        self.git("config", "core.hooksPath", str(SCRIPT.parent.parent / ".githooks"))

    def test_local_hook_rejects_a_bad_subject_before_creating_a_commit(self):
        self.install_hook()

        result = subprocess.run(
            ["git", "commit", "--allow-empty", "-m",
             "Fix Debuff and Debuffs pronunciation across voice packs"],
            cwd=self.directory, capture_output=True, text=True, check=False,
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("type(scope): description", result.stderr)
        self.assertEqual(self.base, self.git("rev-parse", "HEAD").strip())

    def test_local_hook_accepts_a_scoped_fix_with_a_commit_body(self):
        self.install_hook()
        self.git("commit", "--allow-empty", "-q", "-m",
                 "fix(packs): pronounce Debuffs with a stressed DEE syllable",
                 "-m", "Regenerate the affected recordings across all five voices.")

        self.assertEqual(0, self.check().returncode)

    def test_local_hook_checks_the_subject_after_comment_and_whitespace_cleanup(self):
        self.install_hook()
        message = self.directory / "message.txt"
        message.write_text(
            "# Editor comment\n\nfix(packs): pronounce Debuffs with a stressed DEE syllable\n"
        )
        self.git("commit", "--allow-empty", "-q", "--cleanup=strip", "-F", str(message))

        self.assertEqual(0, self.check().returncode)

    def test_only_new_commits_are_checked_and_internal_types_are_valid(self):
        for subject in [
            "feat(nsrt): add Ula'tek callouts",
            "fix(bigwigs_voice)!: replace old keys",
            "ci: check workflows",
            "docs: explain installation",
        ]:
            self.commit(subject)

        result = self.check()

        self.assertEqual(0, result.returncode, result.stderr)

    def test_a_bad_subject_fails_with_the_offending_commit_and_message(self):
        self.commit("fixed some stuff")
        sha = self.git("rev-parse", "--short", "HEAD").strip()

        result = self.check()

        self.assertEqual(1, result.returncode)
        self.assertIn(sha, result.stderr)
        self.assertIn("fixed some stuff", result.stderr)

    def test_git_generated_merge_subjects_are_exempt(self):
        self.install_hook()
        self.git("checkout", "-q", "-b", "feature")
        self.commit("fix(callouts): correct spell pronunciation")
        self.git("checkout", "-q", "main")
        self.git("merge", "-q", "--no-ff", "-m", "Merge pull request #1", "feature")

        self.assertEqual(0, self.check().returncode)

    def test_an_invalid_range_is_an_error_not_an_empty_success(self):
        self.base = "not-a-ref"

        result = self.check()

        self.assertNotEqual(0, result.returncode)

    def test_ci_fetches_the_before_commit_after_an_amended_branch_push(self):
        self.commit("feat(nsrt): add Ula'tek wave directions")
        before = self.git("rev-parse", "HEAD").strip()
        self.git(
            "commit",
            "--amend",
            "--allow-empty",
            "-q",
            "-m",
            "feat(nsrt): add Ula'tek wave direction callouts",
        )
        head = self.git("rev-parse", "HEAD").strip()
        with tempfile.TemporaryDirectory() as fresh:
            subprocess.run(
                ["git", "clone", "--no-local", "-q", str(self.directory), fresh],
                check=True,
            )
            missing = subprocess.run(
                ["git", "cat-file", "-e", before],
                cwd=fresh,
                capture_output=True,
                check=False,
            )
            self.assertNotEqual(0, missing.returncode)
            scripts = Path(fresh) / "scripts"
            scripts.mkdir()
            (scripts / SCRIPT.name).write_text(SCRIPT.read_text())
            workflow = (SCRIPT.parent.parent / ".github/workflows/ci.yml").read_text()
            step = workflow.split("- name: Check new commit messages\n", 1)[1].split(
                "\n      - name:", 1
            )[0]
            shell = textwrap.dedent(step.split("run: |\n", 1)[1])

            result = subprocess.run(
                ["bash", "-e", "-c", shell],
                cwd=fresh,
                capture_output=True,
                text=True,
                env=dict(
                    os.environ, BASE_SHA=before, HEAD_SHA=head, DEFAULT_BRANCH="main"
                ),
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            subprocess.run(["git", "cat-file", "-e", before], cwd=fresh, check=True)


if __name__ == "__main__":
    unittest.main()
