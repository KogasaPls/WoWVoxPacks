import subprocess
import sys
import tempfile
import unittest
import warnings
import zipfile
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPOSITORY_ROOT / "scripts" / "compare_addon_packages.py"


class CompareAddonPackagesTests(unittest.TestCase):
    def _write_zip(self, path: Path, members: list[tuple[str, bytes]], year: int) -> None:
        with zipfile.ZipFile(path, "w") as archive:
            for name, content in members:
                info = zipfile.ZipInfo(name, (year, 1, 1, 0, 0, 0))
                archive.writestr(info, content)

    def _compare(self, previous: Path, current: Path) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, SCRIPT, previous, current],
            capture_output=True,
            text=True,
        )

    def test_zip_metadata_does_not_mark_identical_packages_changed(self):
        """Comparing raw ZIP bytes would republish identical addon files."""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = Path(temporary_directory)
            previous = directory / "previous.zip"
            current = directory / "current.zip"
            members = [("Addon/Core.lua", b"-- same addon\n")]
            self._write_zip(previous, members, 2025)
            self._write_zip(current, members, 2026)

            completed = self._compare(previous, current)

            self.assertEqual(0, completed.returncode, completed.stderr)
            self.assertEqual("unchanged\n", completed.stdout)

    def test_changed_media_marks_the_package_changed(self):
        """A comparator that always reports unchanged would suppress real audio updates."""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = Path(temporary_directory)
            previous = directory / "previous.zip"
            current = directory / "current.zip"
            self._write_zip(previous, [("Addon/Sounds/call.ogg", b"old audio")], 2026)
            self._write_zip(current, [("Addon/Sounds/call.ogg", b"new audio")], 2026)

            completed = self._compare(previous, current)

            self.assertEqual(1, completed.returncode, completed.stderr)
            self.assertEqual("changed\nmodified: Addon/Sounds/call.ogg\n", completed.stdout)

    def test_added_or_removed_members_mark_the_package_changed(self):
        """Comparing only shared paths would suppress package layout changes."""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = Path(temporary_directory)
            previous = directory / "previous.zip"
            current = directory / "current.zip"
            self._write_zip(previous, [("Addon/Old.lua", b"old")], 2026)
            self._write_zip(current, [("Addon/New.lua", b"new")], 2026)

            completed = self._compare(previous, current)

            self.assertEqual(1, completed.returncode, completed.stderr)
            self.assertEqual(
                "changed\nadded: Addon/New.lua\nremoved: Addon/Old.lua\n",
                completed.stdout,
            )

    def test_toc_version_only_does_not_mark_the_package_changed(self):
        """Package revisions may drift without forcing users to redownload the addon."""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = Path(temporary_directory)
            previous = directory / "previous.zip"
            current = directory / "current.zip"
            self._write_zip(
                previous,
                [("Addon/Addon.toc", b"## Interface: 120100\n## Version: 12.1.0-r13\nCore.lua\n")],
                2026,
            )
            self._write_zip(
                current,
                [("Addon/Addon.toc", b"## Interface: 120100\n## Version: 12.1.0-r14\nCore.lua\n")],
                2026,
            )

            completed = self._compare(previous, current)

            self.assertEqual(0, completed.returncode, completed.stderr)
            self.assertEqual("unchanged\n", completed.stdout)

    def test_non_version_toc_change_marks_the_package_changed(self):
        """Ignoring the whole TOC would hide compatibility and dependency changes."""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = Path(temporary_directory)
            previous = directory / "previous.zip"
            current = directory / "current.zip"
            self._write_zip(
                previous,
                [("Addon/Addon.toc", b"## Interface: 120100\n## Version: old\nCore.lua\n")],
                2026,
            )
            self._write_zip(
                current,
                [("Addon/Addon.toc", b"## Interface: 120200\n## Version: new\nCore.lua\n")],
                2026,
            )

            completed = self._compare(previous, current)

            self.assertEqual(1, completed.returncode, completed.stderr)
            self.assertEqual("changed\nmodified: Addon/Addon.toc\n", completed.stdout)

    def test_invalid_zip_is_an_error_not_a_changed_package(self):
        """Invalid comparison data must stop publishing instead of authorizing an upload."""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = Path(temporary_directory)
            previous = directory / "previous.zip"
            current = directory / "current.zip"
            previous.write_bytes(b"not a zip")
            self._write_zip(current, [("Addon/Core.lua", b"-- addon\n")], 2026)

            completed = self._compare(previous, current)

            self.assertEqual(2, completed.returncode)
            self.assertEqual("", completed.stdout)
            self.assertIn("Could not compare addon packages:", completed.stderr)
            self.assertNotIn("Traceback", completed.stderr)

    def test_duplicate_member_name_is_an_error(self):
        """Choosing one duplicate member would make the package comparison ambiguous."""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = Path(temporary_directory)
            previous = directory / "previous.zip"
            current = directory / "current.zip"
            with warnings.catch_warnings():
                warnings.simplefilter("ignore", UserWarning)
                self._write_zip(
                    previous,
                    [("Addon/Core.lua", b"first"), ("Addon/Core.lua", b"second")],
                    2026,
                )
            self._write_zip(current, [("Addon/Core.lua", b"second")], 2026)

            completed = self._compare(previous, current)

            self.assertEqual(2, completed.returncode)
            self.assertIn("duplicate member Addon/Core.lua", completed.stderr)


if __name__ == "__main__":
    unittest.main()
