import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPOSITORY_ROOT / "scripts" / "game_versions.py"

spec = importlib.util.spec_from_file_location("game_versions", SCRIPT_PATH)
game_versions = importlib.util.module_from_spec(spec)
spec.loader.exec_module(game_versions)


class ToGameVersionTests(unittest.TestCase):
    def test_a_six_digit_interface_splits_two_by_two(self):
        self.assertEqual(game_versions.to_game_version("120100"), "12.1.0")
        self.assertEqual(game_versions.to_game_version("120007"), "12.0.7")

    def test_a_five_digit_interface_keeps_its_single_digit_major(self):
        self.assertEqual(game_versions.to_game_version("90205"), "9.2.5")

    def test_a_version_is_not_an_interface_number(self):
        """The two spellings look alike enough to hand the wrong one over."""
        with self.assertRaises(ValueError):
            game_versions.to_game_version("12.1.0")


class GameVersionsTests(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.settings = Path(self.temporary_directory.name) / "appsettings.json"

    def tearDown(self):
        self.temporary_directory.cleanup()

    def write_interfaces(self, interfaces):
        self.settings.write_text(
            json.dumps({"AddOn": {"Interfaces": interfaces}}), encoding="utf-8"
        )

    def test_versions_come_back_lowest_first(self):
        """The release tag takes the last one, so the order is not cosmetic."""
        self.write_interfaces(["120100", "120007"])

        self.assertEqual(game_versions.game_versions(self.settings), ["12.0.7", "12.1.0"])

    def test_the_same_patch_twice_is_named_once(self):
        self.write_interfaces(["120100", "120100"])

        self.assertEqual(game_versions.game_versions(self.settings), ["12.1.0"])

    def test_settings_without_interfaces_are_an_error(self):
        """Uploading with no game version silently hides the file from every player."""
        self.settings.write_text(json.dumps({"AddOn": {}}), encoding="utf-8")

        with self.assertRaises(ValueError):
            game_versions.game_versions(self.settings)

    def test_the_shipped_settings_name_real_patches(self):
        versions = game_versions.game_versions(REPOSITORY_ROOT / "appsettings.json")

        self.assertTrue(versions)
        for version in versions:
            self.assertRegex(version, r"^\d+\.\d+\.\d+$")


if __name__ == "__main__":
    unittest.main()
