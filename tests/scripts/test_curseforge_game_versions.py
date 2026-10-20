import importlib.util
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPOSITORY_ROOT / "scripts" / "curseforge_game_versions.py"

spec = importlib.util.spec_from_file_location("curseforge_game_versions", SCRIPT_PATH)
curseforge_game_versions = importlib.util.module_from_spec(spec)
spec.loader.exec_module(curseforge_game_versions)

LISTED = [
    {"id": 11, "gameVersionTypeID": 517, "name": "12.1.0", "slug": "12-1-0"},
    {"id": 12, "gameVersionTypeID": 517, "name": "12.1.5", "slug": "12-1-5"},
    {"id": 99, "gameVersionTypeID": 67408, "name": "1.15.8", "slug": "1-15-8"},
    {"id": 98, "gameVersionTypeID": 67408, "name": "12.1.7", "slug": "12-1-7"},
]


class VersionIdsTests(unittest.TestCase):
    def test_listed_retail_versions_resolve_in_order(self):
        self.assertEqual(curseforge_game_versions.version_ids(["12.1.5", "12.1.0"], LISTED), ([12, 11], []))

    def test_a_patch_curseforge_has_not_added_is_missing(self):
        self.assertEqual(curseforge_game_versions.version_ids(["12.1.5", "12.1.9"], LISTED), ([12], ["12.1.9"]))

    def test_another_game_types_version_of_the_same_name_does_not_count(self):
        """Uploading a retail pack under a Classic version would list it for the wrong game."""
        self.assertEqual(curseforge_game_versions.version_ids(["12.1.7"], LISTED), ([], ["12.1.7"]))

    def test_a_slug_is_not_a_name(self):
        self.assertEqual(curseforge_game_versions.version_ids(["12-1-5"], LISTED), ([], ["12-1-5"]))


if __name__ == "__main__":
    unittest.main()
