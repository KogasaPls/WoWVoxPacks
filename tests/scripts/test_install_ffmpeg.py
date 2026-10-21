import hashlib
import io
import json
import os
import subprocess
import tarfile
import tempfile
import unittest
from pathlib import Path

INSTALLER = Path(__file__).resolve().parents[2] / "scripts/install_ffmpeg.sh"


class InstallTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.bin = self.root / "bin"
        self.bin.mkdir()
        gh = self.bin / "gh"
        gh.write_text(
            "#!/bin/sh\n"
            '[ "$*" = "api repos/BtbN/FFmpeg-Builds/releases/tags/latest" ] || exit 1\n'
            'cat "$FFMPEG_TEST_RELEASE"\n'
        )
        gh.chmod(0o755)
        self.runner = self.root / "runner"
        self.runner.mkdir()
        self.path_file = self.root / "github-path"

    def asset(self, branch):
        name = f"ffmpeg-n{branch}-latest-linux64-gpl-{branch}"
        archive = self.root / f"{name}.tar.xz"
        executable = f"#!/bin/sh\necho 'ffmpeg version {branch}'\n".encode()
        with tarfile.open(archive, "w:xz") as tar:
            entry = tarfile.TarInfo(f"{name}/bin/ffmpeg")
            entry.mode = 0o755
            entry.size = len(executable)
            tar.addfile(entry, io.BytesIO(executable))
        return {
            "name": archive.name,
            "digest": "sha256:" + hashlib.sha256(archive.read_bytes()).hexdigest(),
            "browser_download_url": archive.as_uri(),
        }

    def install(self, assets):
        release = self.root / "release.json"
        release.write_text(json.dumps({"assets": assets}))
        return subprocess.run(
            ["bash", str(INSTALLER)],
            check=False,
            env={
                **os.environ,
                "PATH": f"{self.bin}:{os.environ['PATH']}",
                "FFMPEG_TEST_RELEASE": str(release),
                "RUNNER_TEMP": str(self.runner),
                "GITHUB_PATH": str(self.path_file),
            },
            capture_output=True,
            text=True,
        )

    def test_installs_the_newest_minor_in_the_selected_major(self):
        result = self.install(
            [self.asset("9.1"), self.asset("10.0"), self.asset("9.2")]
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("ffmpeg version 9.2", result.stdout)
        installed = Path(self.path_file.read_text().strip()) / "ffmpeg"
        self.assertTrue(installed.is_file())

    def test_a_missing_major_stops_without_installing(self):
        result = self.install([self.asset("10.0")])
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse(self.path_file.exists())

    def test_a_missing_digest_stops_without_installing(self):
        asset = self.asset("9.2")
        asset["digest"] = None
        result = self.install([asset])
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse(self.path_file.exists())

    def test_a_wrong_digest_stops_before_extracting(self):
        asset = self.asset("9.2")
        asset["digest"] = "sha256:" + "0" * 64
        result = self.install([asset])
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse(list(self.runner.glob("*/bin/ffmpeg")))
        self.assertFalse(self.path_file.exists())


if __name__ == "__main__":
    unittest.main()
