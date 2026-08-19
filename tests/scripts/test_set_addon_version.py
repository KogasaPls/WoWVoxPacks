import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPOSITORY_ROOT / "scripts" / "set_addon_version.py"

spec = importlib.util.spec_from_file_location("set_addon_version", SCRIPT_PATH)
set_addon_version = importlib.util.module_from_spec(spec)
spec.loader.exec_module(set_addon_version)


SETTINGS = """{
  "AddOn": {
    "BigWigs_Voice": {
      "Title": "BigWigs_Voice_WoWVoxPacks"
    },
    "Version": "12.1.0",
    "Author": "KogasaPls",
    "Interfaces": [ "120007", "120100" ]
  }
}
"""


class SetAddonVersionTests(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.settings = Path(self.temporary_directory.name) / "appsettings.json"
        self.settings.write_text(SETTINGS, encoding="utf-8")

    def tearDown(self):
        self.temporary_directory.cleanup()

    def test_the_tag_prefix_is_not_part_of_the_version(self):
        """bigwigs-voice-version.txt holds a tag; the toc holds a version."""
        set_addon_version.set_version(self.settings, "v12.1.0.1")

        self.assertEqual(set_addon_version.read_version(self.settings), "12.1.0.1")

    def test_only_the_version_line_changes(self):
        """The settings are hand-formatted, so a rewrite would bury the bump in churn."""
        set_addon_version.set_version(self.settings, "12.1.0.1")

        self.assertEqual(
            self.settings.read_text(encoding="utf-8"),
            SETTINGS.replace('"Version": "12.1.0"', '"Version": "12.1.0.1"'),
        )

    def test_an_unchanged_version_writes_nothing(self):
        self.assertFalse(set_addon_version.set_version(self.settings, "v12.1.0"))
        self.assertEqual(self.settings.read_text(encoding="utf-8"), SETTINGS)

    def test_an_empty_version_is_refused(self):
        """The workflow feeds this an upstream lookup that can come back empty."""
        with self.assertRaises(ValueError):
            set_addon_version.set_version(self.settings, "v")

        self.assertEqual(self.settings.read_text(encoding="utf-8"), SETTINGS)

    def test_an_ambiguous_version_is_refused(self):
        """Another key of the same name and value gives the replacement two candidates."""
        parsed = json.loads(SETTINGS)
        parsed["AddOn"]["BigWigs_Voice"]["Version"] = parsed["AddOn"]["Version"]
        self.settings.write_text(json.dumps(parsed, indent=2), encoding="utf-8")

        with self.assertRaises(ValueError):
            set_addon_version.set_version(self.settings, "12.1.0.1")

    def test_settings_without_a_version_are_an_error(self):
        self.settings.write_text('{"AddOn": {"Author": "KogasaPls"}}', encoding="utf-8")

        with self.assertRaises(ValueError):
            set_addon_version.set_version(self.settings, "12.1.0.1")


if __name__ == "__main__":
    unittest.main()
