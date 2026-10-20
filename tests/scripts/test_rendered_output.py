import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]


def manifest(*file_names):
    return json.dumps([{"FileName": name, "Text": name} for name in file_names])


BASE_MANIFEST = manifest("old.ogg", "retired.ogg")
PARTIAL_MANIFEST = manifest("get orb.ogg", "old.ogg", "retired.ogg")
SCRIPT_PATH = REPOSITORY_ROOT / "scripts" / "rendered_output.py"


class RenderedOutputTests(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        root = Path(self.temporary_directory.name)
        self.repo = root / "repo"
        self.kept = root / "kept"
        self.repo.mkdir()
        self.git("init", "-q")
        self.write("output/Voice/Pack/Sounds/old.ogg", "old")
        self.write("output/Voice/Pack/Sounds/retired.ogg", "retired")
        self.write("output/Voice/Pack/SoundFiles.json", BASE_MANIFEST)
        self.commit("base")
        self.base = self.git("rev-parse", "HEAD")

    def tearDown(self):
        self.temporary_directory.cleanup()

    def git(self, *arguments):
        return subprocess.run(
            ["git", "-c", "user.name=t", "-c", "user.email=t@t", *arguments],
            cwd=self.repo, check=True, capture_output=True, text=True,
        ).stdout.strip()

    def write(self, path, content):
        target = self.repo / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")

    def commit(self, message):
        self.git("add", "-A")
        self.git("commit", "-q", "-m", message)

    def run_script(self, *arguments):
        return subprocess.run(
            [sys.executable, str(SCRIPT_PATH), *arguments],
            cwd=self.repo, check=True, capture_output=True, text=True,
        ).stdout

    def build_partway(self):
        """What a failed build leaves: a new file, a changed manifest, a retired file, a temp file."""
        self.write("output/Voice/Pack/Sounds/get orb.ogg", "new")
        self.write("output/Voice/Pack/SoundFiles.json", PARTIAL_MANIFEST)
        (self.repo / "output/Voice/Pack/Sounds/retired.ogg").unlink()
        self.write("output/Voice/Pack/Sounds/.wvp-123.ogg", "half written")

    def test_collect_keeps_written_files_and_records_deletions(self):
        self.build_partway()

        self.run_script("collect", "--into", str(self.kept))

        self.assertEqual((self.kept / "files/output/Voice/Pack/Sounds/get orb.ogg").read_text(), "new")
        self.assertEqual((self.kept / "files/output/Voice/Pack/SoundFiles.json").read_text(), PARTIAL_MANIFEST)
        self.assertFalse((self.kept / "files/output/Voice/Pack/Sounds/.wvp-123.ogg").exists())
        self.assertEqual(
            (self.kept / "deleted.txt").read_text(), "output/Voice/Pack/Sounds/retired.ogg\n"
        )
        self.assertEqual((self.kept / "base-sha").read_text().strip(), self.base)

    def test_restore_puts_a_fresh_checkout_back_where_the_failed_build_stopped(self):
        self.build_partway()
        self.run_script("collect", "--into", str(self.kept))
        self.git("checkout", "-q", "--", ".")
        self.git("clean", "-qfd")

        self.run_script("restore", "--from", str(self.kept))

        sounds = self.repo / "output/Voice/Pack/Sounds"
        self.assertEqual((sounds / "get orb.ogg").read_text(), "new")
        self.assertEqual((self.repo / "output/Voice/Pack/SoundFiles.json").read_text(), PARTIAL_MANIFEST)
        self.assertFalse((sounds / "retired.ogg").exists())
        self.assertEqual((sounds / "old.ogg").read_text(), "old")

    def test_restore_drops_recordings_no_manifest_describes(self):
        """A build killed without warning wrote no partial manifest for what it rendered last.

        Kept anyway, such a file would count as current on the retry even after its spell was
        renamed upstream, so it is given up instead.
        """
        self.build_partway()
        self.write("output/Voice/Pack/Sounds/rendered after the kill.ogg", "unrecorded")
        self.write("output/Voice/Pack/Sounds/old.ogg", "re-rendered, then killed")
        self.write("output/Voice/Pack/SoundFiles.json", manifest("get orb.ogg", "retired.ogg"))
        self.run_script("collect", "--into", str(self.kept))
        self.git("checkout", "-q", "--", ".")
        self.git("clean", "-qfd")

        printed = self.run_script("restore", "--from", str(self.kept))

        sounds = self.repo / "output/Voice/Pack/Sounds"
        self.assertEqual((sounds / "get orb.ogg").read_text(), "new")
        self.assertFalse((sounds / "rendered after the kill.ogg").exists())
        self.assertEqual((sounds / "old.ogg").read_text(), "old")
        self.assertIn("dropped 2 recordings", printed)

    def test_restore_trusts_no_recording_in_a_pack_whose_manifest_never_came_back(self):
        """The shape a kill leaves: renders on disk, the pack manifest untouched since main.

        main's manifest still lists old.ogg under its old text, so the re-rendered take would
        pass as that text on the retry. Neither recording is kept.
        """
        self.write("output/Voice/Pack/Sounds/old.ogg", "re-rendered for new text")
        self.write("output/Voice/Pack/Sounds/get orb.ogg", "new")
        self.run_script("collect", "--into", str(self.kept))
        self.git("checkout", "-q", "--", ".")
        self.git("clean", "-qfd")

        self.run_script("restore", "--from", str(self.kept))

        sounds = self.repo / "output/Voice/Pack/Sounds"
        self.assertEqual((sounds / "old.ogg").read_text(), "old")
        self.assertFalse((sounds / "get orb.ogg").exists())

    def test_restore_skips_output_that_changed_after_the_failed_build(self):
        """Merged output is newer than the kept copy, and its manifests describe different files."""
        self.build_partway()
        self.run_script("collect", "--into", str(self.kept))
        self.git("checkout", "-q", "--", ".")
        self.git("clean", "-qfd")
        self.write("output/Voice/Pack/SoundFiles.json", manifest("old.ogg"))
        self.commit("merged update")

        printed = self.run_script("restore", "--from", str(self.kept))

        self.assertIn("Not restoring", printed)
        self.assertEqual((self.repo / "output/Voice/Pack/SoundFiles.json").read_text(), manifest("old.ogg"))
        self.assertFalse((self.repo / "output/Voice/Pack/Sounds/get orb.ogg").exists())

    def test_restore_writes_nothing_outside_output(self):
        (self.kept / "files/output/../evil").mkdir(parents=True)
        (self.kept / "files/evil/x").write_text("x")
        (self.kept / "deleted.txt").write_text("")
        (self.kept / "base-sha").write_text(self.base + "\n")

        result = subprocess.run(
            [sys.executable, str(SCRIPT_PATH), "restore", "--from", str(self.kept)],
            cwd=self.repo, capture_output=True, text=True,
        )

        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.repo / "evil").exists())

    def test_restore_deletes_nothing_outside_output(self):
        self.write("keep.txt", "keep")
        self.commit("a file outside output")
        (self.kept).mkdir()
        (self.kept / "deleted.txt").write_text("output/../keep.txt\n")
        (self.kept / "base-sha").write_text(self.git("rev-parse", "HEAD") + "\n")

        result = subprocess.run(
            [sys.executable, str(SCRIPT_PATH), "restore", "--from", str(self.kept)],
            cwd=self.repo, capture_output=True, text=True,
        )

        self.assertNotEqual(result.returncode, 0)
        self.assertTrue((self.repo / "keep.txt").exists())


if __name__ == "__main__":
    unittest.main()
