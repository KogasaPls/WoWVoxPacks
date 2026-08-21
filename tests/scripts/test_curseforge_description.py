import json
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPOSITORY_ROOT / "scripts" / "curseforge_description.py"
PAGES = REPOSITORY_ROOT / "docs" / "curseforge"
WORKFLOW = REPOSITORY_ROOT / ".github" / "workflows" / "publish-to-curseforge.yml"


def published_matrix() -> dict[str, dict[str, int]]:
    """The voice and addon names publish-to-curseforge.yml holds project IDs for."""
    workflow = WORKFLOW.read_text(encoding="utf-8")
    block = re.search(
        r"voice-to-addon-to-project-id-json:\s*\n\s*- '(?P<json>.*?)'\s*\n", workflow, re.S)
    if block is None:
        raise AssertionError("publish-to-curseforge.yml has no project ID mapping")
    return json.loads(block.group("json"))


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
        self.assertEqual(addons, {page.stem for page in PAGES.glob("*.md")})

    def test_published_voices_match_the_publish_workflow(self):
        module = SCRIPT.read_text(encoding="utf-8")
        declared = re.search(r"PUBLISHED_VOICES = \((?P<voices>[^)]*)\)", module)
        self.assertIsNotNone(declared)
        voices = set(re.findall(r'"([^"]+)"', declared.group("voices")))
        self.assertEqual(set(published_matrix()), voices)

    def test_rendered_pages_carry_no_placeholder(self):
        matrix = published_matrix()
        for voice, projects in matrix.items():
            for addon in projects:
                with self.subTest(voice=voice, addon=addon):
                    page = run("--addon", addon, "--voice", voice)
                    summary = run("--addon", addon, "--voice", voice, "--summary")
                    self.assertEqual([], re.findall(r"\{[A-Za-z]+\}", page))
                    self.assertEqual([], re.findall(r"\{[A-Za-z]+\}", summary))
                    self.assertIn(voice, page)
                    self.assertTrue(summary.strip())

    def test_every_published_voice_has_a_description(self):
        module = SCRIPT.read_text(encoding="utf-8")
        declared = re.search(r"VOICE_DESCRIPTIONS = \{(?P<body>[^}]*)\}", module)
        self.assertIsNotNone(declared)
        described = set(re.findall(r'"([^"]+)":', declared.group("body")))
        self.assertEqual(set(published_matrix()), described)

    def test_every_published_addon_has_a_slug(self):
        module = SCRIPT.read_text(encoding="utf-8")
        declared = re.search(r"ADDON_SLUGS = \{(?P<body>[^}]*)\}", module)
        self.assertIsNotNone(declared)
        slugged = set(re.findall(r'"([^"]+)":', declared.group("body")))
        addons = {addon for projects in published_matrix().values() for addon in projects}
        self.assertEqual(addons, slugged)

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
