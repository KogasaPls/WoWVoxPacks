import os
import shutil
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
ADDON = "WoWVoxPacks_Callouts_V"


@unittest.skipUnless(shutil.which("zip"), "zip is not installed")
class PackageTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        (self.root / "scripts").mkdir()
        shutil.copy(REPOSITORY_ROOT / "scripts/package.sh", self.root / "scripts/package.sh")

    def write(self, path, content):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")

    def package(self):
        subprocess.run(
            ["bash", "scripts/package.sh"], cwd=self.root, check=True, capture_output=True,
            env={**os.environ, "RELEASE_TAG": "vT"},
        )
        return sorted(path.name for path in (self.root / "dist").iterdir())

    def entries(self, archive):
        with zipfile.ZipFile(self.root / "dist" / archive) as opened:
            return opened.namelist()

    def test_forever_tree_gets_forever_archives(self):
        self.write(f"output/V/{ADDON}/retail.toc", "retail")
        self.write(f"output-forever/V/{ADDON}/forever.toc", "forever")

        archives = self.package()

        self.assertEqual(
            ["WoWVoxPacks_V_Callouts_Forever_vT.zip", "WoWVoxPacks_V_Callouts_vT.zip"], archives
        )
        self.assertEqual([f"{ADDON}/retail.toc"], [
            name for name in self.entries("WoWVoxPacks_V_Callouts_vT.zip") if name.endswith(".toc")
        ])
        self.assertEqual([f"{ADDON}/forever.toc"], [
            name for name in self.entries("WoWVoxPacks_V_Callouts_Forever_vT.zip")
            if name.endswith(".toc")
        ])

    def test_a_checkout_without_forever_output_packages_retail_only(self):
        self.write(f"output/V/{ADDON}/retail.toc", "retail")

        self.assertEqual(["WoWVoxPacks_V_Callouts_vT.zip"], self.package())


if __name__ == "__main__":
    unittest.main()
