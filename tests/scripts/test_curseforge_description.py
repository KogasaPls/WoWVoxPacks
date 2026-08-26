import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPOSITORY_ROOT / "scripts" / "curseforge_description.py"
PAGES = REPOSITORY_ROOT / "docs" / "curseforge"
sys.path.insert(0, str(REPOSITORY_ROOT / "scripts"))

import curseforge_packages

CATALOG = curseforge_packages.load()
# Wide enough to catch {Url:BigWigs_Voice}, the form a missing substitution leaves behind.
PLACEHOLDER = r"\{[A-Za-z][A-Za-z0-9_:.-]*\}"


def published_matrix() -> dict[str, dict[str, int]]:
    return CATALOG.projects


def run(*arguments: str) -> str:
    result = subprocess.run(
        [sys.executable, str(SCRIPT), *arguments],
        capture_output=True, text=True, check=False)
    if result.returncode != 0:
        raise AssertionError(f"{' '.join(arguments)} failed: {result.stderr.strip()}")
    return result.stdout


class CurseForgeDescriptionTests(unittest.TestCase):
    def test_every_published_addon_has_a_page(self):
        addons = {addon for projects in published_matrix().values() for addon in projects}
        self.assertEqual(addons, {page.stem for page in PAGES.glob("*.md")
                                 if not page.stem.startswith("_")})

    def test_rendered_pages_carry_no_placeholder(self):
        matrix = published_matrix()
        for voice, projects in matrix.items():
            for addon in projects:
                with self.subTest(voice=voice, addon=addon):
                    page = run("--addon", addon, "--voice", voice)
                    summary = run("--addon", addon, "--voice", voice, "--summary")
                    self.assertEqual([], re.findall(PLACEHOLDER, page))
                    self.assertEqual([], re.findall(PLACEHOLDER, summary))
                    self.assertIn(voice, page)
                    self.assertTrue(summary.strip())

    def test_every_published_voice_has_a_description(self):
        for voice in CATALOG.voice_names:
            self.assertTrue(CATALOG.voice_description(voice))

    def test_every_published_addon_has_a_slug(self):
        for addon in CATALOG.addon_names:
            self.assertTrue(CATALOG.addon_slug(addon))

    def test_pages_link_every_pack_to_its_own_voice(self):
        for voice in published_matrix():
            page = run("--addon", "Callouts", "--voice", voice)
            for slug in re.findall(r"curseforge\.com/wow/addons/([\w-]+)", page):
                self.assertTrue(slug.startswith(f"wowvoxpacks-{voice.lower().replace('_', '-')}-"),
                                f"{slug} does not belong to {voice}")

    def test_all_writes_one_page_and_summary_per_voice(self):
        matrix = published_matrix()
        with tempfile.TemporaryDirectory() as temporary_directory:
            destination = Path(temporary_directory)
            run("--all", str(destination))
            for voice, projects in matrix.items():
                for addon in projects:
                    self.assertTrue((destination / voice / f"{addon}.md").is_file())
                    self.assertTrue((destination / voice / f"{addon}.summary.txt").is_file())


if __name__ == "__main__":
    unittest.main()
