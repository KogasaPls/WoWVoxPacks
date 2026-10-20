import importlib.util
import json
import subprocess
import sys
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


class ToInterfaceTests(unittest.TestCase):
    def test_a_client_build_drops_its_build_number(self):
        self.assertEqual(game_versions.to_interface("12.1.5.70448"), "120105")

    def test_a_single_digit_major_keeps_five_digits(self):
        self.assertEqual(game_versions.to_interface("9.2.5.12345"), "90205")

    def test_a_patch_without_a_build_is_not_a_client_build(self):
        with self.assertRaises(ValueError):
            game_versions.to_interface("12.1.5")

    def test_a_component_that_overflows_two_digits_is_an_error(self):
        for build in ("12.100.0.70448", "12.1.100.70448"):
            with self.subTest(build=build), self.assertRaises(ValueError):
                game_versions.to_interface(build)

    def test_a_build_with_extra_components_is_an_error(self):
        with self.assertRaises(ValueError):
            game_versions.to_interface("12.1.5.70448.1")

    def test_surrounding_whitespace_is_ignored(self):
        self.assertEqual(game_versions.to_interface(" 12.1.5.70448\r\n"), "120105")


VERSIONS = (
    "Region!STRING:0|BuildConfig!HEX:16|CDNConfig!HEX:16|KeyRing!HEX:16|BuildId!DEC:4"
    "|VersionsName!String:0|ProductConfig!HEX:16\n"
    "## seqn = 4086275\n"
    "us|88d5|86e1|3ca5|70448|12.1.5.70448|5302\n"
    "eu|88d5|86e1|3ca5|70448|12.1.5.70448|5302\n"
    "kr|aaaa|bbbb|cccc|70500|12.1.7.70500|dddd\n"
)


class LiveBuildTests(unittest.TestCase):
    def test_the_us_row_names_the_build(self):
        self.assertEqual(game_versions.live_build(VERSIONS), "12.1.5.70448")

    def test_another_region_reads_its_own_row(self):
        self.assertEqual(game_versions.live_build(VERSIONS, "kr"), "12.1.7.70500")

    def test_a_table_without_the_region_is_an_error(self):
        with self.assertRaises(ValueError):
            game_versions.live_build(VERSIONS, "tw")

    def test_a_table_without_a_versions_column_is_an_error(self):
        with self.assertRaises(ValueError):
            game_versions.live_build(VERSIONS.replace("VersionsName", "Name"))

    def test_an_error_page_is_not_a_build(self):
        with self.assertRaises(ValueError):
            game_versions.live_build("<html><body>503</body></html>\n")

    def test_a_malformed_build_is_an_error(self):
        with self.assertRaises(ValueError):
            game_versions.live_build(VERSIONS.replace("12.1.5.70448", "12.1.5"))


class FollowTests(unittest.TestCase):
    SETTINGS = (
        '{\n  "AddOn": {\n    "Version": "12.1.0",\n    "Author": "KogasaPls",\n'
        '    "Interfaces": [ "120007", "120100" ]\n  }\n}\n'
    )

    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.settings = Path(self.temporary_directory.name) / "appsettings.json"
        self.settings.write_text(self.SETTINGS, encoding="utf-8")

    def tearDown(self):
        self.temporary_directory.cleanup()

    def test_a_newer_live_build_replaces_every_interface(self):
        self.assertEqual(game_versions.follow(self.settings, "12.1.5.70448"), "12.1.5")

        self.assertEqual(
            self.settings.read_text(encoding="utf-8"),
            '{\n  "AddOn": {\n    "Version": "12.1.5",\n    "Author": "KogasaPls",\n'
            '    "Interfaces": [ "120105" ]\n  }\n}\n',
        )

    def test_the_declared_live_build_changes_nothing(self):
        self.assertIsNone(game_versions.follow(self.settings, "12.1.0.68000"))
        self.assertEqual(self.settings.read_text(encoding="utf-8"), self.SETTINGS)

    def test_an_older_live_build_changes_nothing(self):
        """A mirror that lags behind a hand bump must not walk the addons back."""
        self.assertIsNone(game_versions.follow(self.settings, "12.0.7.66000"))
        self.assertEqual(self.settings.read_text(encoding="utf-8"), self.SETTINGS)

    def test_the_highest_declared_interface_counts_wherever_it_is_listed(self):
        unordered = self.SETTINGS.replace('"120007", "120100"', '"120100", "120007"')
        self.settings.write_text(unordered, encoding="utf-8")

        self.assertIsNone(game_versions.follow(self.settings, "12.1.0.68000"))

    def test_interfaces_compare_as_numbers(self):
        """As strings, "90205" sorts after "120105" and would hold the settings back forever."""
        self.settings.write_text(
            self.SETTINGS.replace('"120007", "120100"', '"90205"'), encoding="utf-8"
        )

        self.assertEqual(game_versions.follow(self.settings, "12.1.5.70448"), "12.1.5")

    def test_a_second_version_key_is_an_error(self):
        """A per-addon Version would otherwise be rewritten along with the root's."""
        ambiguous = self.SETTINGS.replace('"Author"', '"BigWigs_Voice": { "Version": "1" },\n    "Author"')
        self.settings.write_text(ambiguous, encoding="utf-8")

        with self.assertRaises(ValueError):
            game_versions.follow(self.settings, "12.1.5.70448")
        self.assertEqual(self.settings.read_text(encoding="utf-8"), ambiguous)

    def test_the_shipped_settings_change_only_the_two_keys(self):
        shipped = (REPOSITORY_ROOT / "appsettings.json").read_text(encoding="utf-8")
        self.settings.write_text(shipped, encoding="utf-8")

        self.assertEqual(game_versions.follow(self.settings, "99.9.9.99999"), "99.9.9")

        before = json.loads(shipped)
        after = json.loads(self.settings.read_text(encoding="utf-8"))
        self.assertEqual(after["AddOn"]["Interfaces"], ["990909"])
        self.assertEqual(after["AddOn"]["Version"], "99.9.9")
        for key in ("Interfaces", "Version"):
            del before["AddOn"][key], after["AddOn"][key]
        self.assertEqual(after, before)


class CommandLineTests(unittest.TestCase):
    """The update workflow rebuilds whenever --follow prints anything."""

    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.directory = Path(self.temporary_directory.name)
        self.settings = self.directory / "appsettings.json"
        self.settings.write_text(FollowTests.SETTINGS, encoding="utf-8")

    def tearDown(self):
        self.temporary_directory.cleanup()

    def run_script(self, *arguments):
        return subprocess.run(
            [sys.executable, str(SCRIPT_PATH), *arguments],
            capture_output=True,
            text=True,
            check=True,
        ).stdout

    def test_follow_prints_the_new_game_version(self):
        output = self.run_script("--settings", str(self.settings), "--follow", "12.1.5.70448")

        self.assertEqual(output, "12.1.5\n")

    def test_follow_prints_nothing_when_nothing_changed(self):
        output = self.run_script("--settings", str(self.settings), "--follow", "12.1.0.68000")

        self.assertEqual(output, "")

    def test_live_build_prints_the_us_build(self):
        versions = self.directory / "versions.txt"
        versions.write_text(VERSIONS, encoding="utf-8")

        self.assertEqual(self.run_script("--live-build", str(versions)), "12.1.5.70448\n")


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
