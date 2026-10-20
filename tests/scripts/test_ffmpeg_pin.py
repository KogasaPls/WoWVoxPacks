import datetime
import importlib.util
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPOSITORY_ROOT / "scripts" / "ffmpeg_pin.py"

spec = importlib.util.spec_from_file_location("ffmpeg_pin", SCRIPT_PATH)
ffmpeg_pin = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ffmpeg_pin)

SCRIPT = """set -euo pipefail

release=https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-09-30-13-08
asset=ffmpeg-n9.0.2-17-g2a571b6068-linux64-gpl-9.0
sha256=68ee646831adaae2495618346f3bba94ff207ff83bbd34d643e7004730d66269
"""


def release(tag, *branches, digest="a" * 64):
    return {
        "tag_name": tag,
        "assets": [
            {"name": f"ffmpeg-n{branch}.1-3-gabc-linux64-gpl-{branch}.tar.xz", "digest": f"sha256:{digest}"}
            for branch in branches
        ] + [{"name": "checksums.sha256", "digest": "sha256:" + "c" * 64}],
    }


class RefreshTests(unittest.TestCase):
    def test_a_pin_under_a_year_old_stays(self):
        self.assertIsNone(ffmpeg_pin.refresh(SCRIPT, [], datetime.date(2027, 9, 1)))

    def test_a_year_old_pin_moves_to_the_last_build_of_the_newest_complete_month(self):
        releases = [
            release("autobuild-2027-09-29-13-00", "9.0", digest="1" * 64),
            release("autobuild-2027-09-30-13-00", "9.0", digest="2" * 64),
            release("autobuild-2027-10-01-13-00", "9.0", digest="3" * 64),
            release("autobuild-2027-08-31-13-00", "9.0", digest="4" * 64),
        ]

        script, tag = ffmpeg_pin.refresh(SCRIPT, releases, datetime.date(2027, 10, 2))

        self.assertEqual(tag, "autobuild-2027-09-30-13-00")
        self.assertIn("release=https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2027-09-30-13-00\n", script)
        self.assertIn("asset=ffmpeg-n9.0.1-3-gabc-linux64-gpl-9.0\n", script)
        self.assertIn("sha256=" + "2" * 64 + "\n", script)
        self.assertTrue(script.startswith("set -euo pipefail\n"))

    def test_the_same_branch_wins_over_a_newer_one(self):
        releases = [release("autobuild-2027-09-30-13-00", "9.0", "10.0")]

        script, _ = ffmpeg_pin.refresh(SCRIPT, releases, datetime.date(2027, 10, 2))

        self.assertIn("asset=ffmpeg-n9.0.1-3-gabc-linux64-gpl-9.0\n", script)

    def test_a_dropped_branch_moves_to_the_newest_one(self):
        releases = [release("autobuild-2027-09-30-13-00", "8.1", "10.0")]

        script, _ = ffmpeg_pin.refresh(SCRIPT, releases, datetime.date(2027, 10, 2))

        self.assertIn("asset=ffmpeg-n10.0.1-3-gabc-linux64-gpl-10.0\n", script)

    def test_no_newer_month_end_build_is_an_error(self):
        """A pin that can neither stay nor move is about to break CI, so the job must say so."""
        releases = [release("autobuild-2026-08-31-13-00", "9.0")]

        with self.assertRaises(ValueError):
            ffmpeg_pin.refresh(SCRIPT, releases, datetime.date(2027, 10, 2))

    def test_an_asset_without_a_digest_is_not_chosen(self):
        undigested = release("autobuild-2027-09-30-13-00", "9.0")
        undigested["assets"][0].pop("digest")
        releases = [undigested, release("autobuild-2027-08-31-13-00", "9.0", digest="5" * 64)]

        script, tag = ffmpeg_pin.refresh(SCRIPT, releases, datetime.date(2027, 10, 2))

        self.assertEqual(tag, "autobuild-2027-08-31-13-00")
        self.assertIn("sha256=" + "5" * 64 + "\n", script)

    def test_the_shipped_installer_is_readable(self):
        tag, asset, sha256 = ffmpeg_pin.pinned((REPOSITORY_ROOT / "scripts/install_ffmpeg.sh").read_text())

        ffmpeg_pin.tag_date(tag)
        ffmpeg_pin.branch_of(asset)
        self.assertRegex(sha256, r"^[0-9a-f]{64}$")


if __name__ == "__main__":
    unittest.main()
