import json
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "scripts"))

import curseforge_packages
import plan_curseforge_release


class PlanCurseForgeReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.current = self.root / "current"
        self.previous = self.root / "previous"
        self.current.mkdir()
        self.previous.mkdir()
        self.catalog = write_catalog(self.root / "catalog.json")

    def tearDown(self):
        self.temporary_directory.cleanup()

    def test_forced_plan_contains_every_selected_package_in_catalog_order(self):
        for voice in self.catalog.voice_names:
            for addon in self.catalog.addon_names:
                write_package(self.current / archive(voice, addon, "v2"), {"Addon/Core.lua": b"same"})

        plan = plan_curseforge_release.plan_release(
            self.catalog, "v2", self.current, force=True
        )

        self.assertEqual(
            [
                {
                    "voice": "Voice_A",
                    "addon": "Addon_One",
                    "project_id": 11,
                    "relations": "dependency:requiredDependency",
                    "archive": "WoWVoxPacks_Voice_A_Addon_One_v2.zip",
                },
                {
                    "voice": "Voice_A",
                    "addon": "Addon_Two",
                    "project_id": 12,
                    "relations": "",
                    "archive": "WoWVoxPacks_Voice_A_Addon_Two_v2.zip",
                },
                {
                    "voice": "Voice_B",
                    "addon": "Addon_One",
                    "project_id": 21,
                    "relations": "dependency:requiredDependency",
                    "archive": "WoWVoxPacks_Voice_B_Addon_One_v2.zip",
                },
                {
                    "voice": "Voice_B",
                    "addon": "Addon_Two",
                    "project_id": 22,
                    "relations": "",
                    "archive": "WoWVoxPacks_Voice_B_Addon_Two_v2.zip",
                },
            ],
            plan,
        )

    def test_unchanged_package_is_omitted_when_only_toc_version_changed(self):
        old = b"## Interface: 120100\n## Version: v1\nCore.lua\n"
        new = b"## Interface: 120100\n## Version: v2\nCore.lua\n"
        write_package(self.previous / archive("Voice_A", "Addon_One", "v1"), {"Addon/Addon.toc": old})
        write_package(self.current / archive("Voice_A", "Addon_One", "v2"), {"Addon/Addon.toc": new})

        plan = plan_curseforge_release.plan_release(
            self.catalog, "v2", self.current, "v1", self.previous,
            voices=["Voice_A"], addons=["Addon_One"]
        )

        self.assertEqual([], plan)

    def test_changed_package_is_included(self):
        write_package(self.previous / archive("Voice_A", "Addon_One", "v1"), {"Addon/Core.lua": b"old"})
        write_package(self.current / archive("Voice_A", "Addon_One", "v2"), {"Addon/Core.lua": b"new"})

        plan = plan_curseforge_release.plan_release(
            self.catalog, "v2", self.current, "v1", self.previous,
            voices=["Voice_A"], addons=["Addon_One"]
        )

        self.assertEqual(["WoWVoxPacks_Voice_A_Addon_One_v2.zip"],
                         [package["archive"] for package in plan])

    def test_missing_previous_package_is_included(self):
        write_package(self.current / archive("Voice_A", "Addon_One", "v2"), {"Addon/Core.lua": b"new"})

        plan = plan_curseforge_release.plan_release(
            self.catalog, "v2", self.current, "v1", self.previous,
            voices=["Voice_A"], addons=["Addon_One"]
        )

        self.assertEqual(1, len(plan))

    def test_corrupt_package_is_a_planning_error(self):
        write_package(self.previous / archive("Voice_A", "Addon_One", "v1"), {"Addon/Core.lua": b"old"})
        (self.current / archive("Voice_A", "Addon_One", "v2")).write_bytes(b"not a zip")

        with self.assertRaisesRegex(
            plan_curseforge_release.compare_addon_packages.PackageComparisonError,
            "File is not a zip file",
        ):
            plan_curseforge_release.plan_release(
                self.catalog, "v2", self.current, "v1", self.previous,
                voices=["Voice_A"], addons=["Addon_One"]
            )

    def test_missing_current_package_is_fatal(self):
        with self.assertRaisesRegex(FileNotFoundError, "missing or empty current release asset"):
            plan_curseforge_release.plan_release(
                self.catalog, "v2", self.current, force=True,
                voices=["Voice_A"], addons=["Addon_One"]
            )

    def test_unknown_selection_is_fatal(self):
        with self.assertRaisesRegex(ValueError, "unknown voice 'Unknown'"):
            plan_curseforge_release.plan_release(
                self.catalog, "v2", self.current, force=True,
                voices=["Unknown"], addons=["Addon_One"]
            )

    def test_selected_archives_are_derived_without_downloading_packages(self):
        self.assertEqual(
            [
                "WoWVoxPacks_Voice_B_Addon_One_v2.zip",
                "WoWVoxPacks_Voice_B_Addon_Two_v2.zip",
            ],
            plan_curseforge_release.selected_archives(
                self.catalog, "v2", voices=["Voice_B"]
            ),
        )


def archive(voice: str, addon: str, tag: str) -> str:
    return f"WoWVoxPacks_{voice}_{addon}_{tag}.zip"


def write_package(path: Path, members: dict[str, bytes]) -> None:
    with zipfile.ZipFile(path, "w") as package:
        for name, content in members.items():
            package.writestr(name, content)


def write_catalog(path: Path):
    path.write_text(json.dumps({
        "voices": [
            {"name": "Voice_A", "description": "A",
             "projects": {"Addon_One": 11, "Addon_Two": 12}},
            {"name": "Voice_B", "description": "B",
             "projects": {"Addon_One": 21, "Addon_Two": 22}},
        ],
        "addons": [
            {"name": "Addon_One", "slug": "one", "relations": [
                {"slug": "dependency", "type": "requiredDependency"}
            ]},
            {"name": "Addon_Two", "slug": "two", "relations": []},
        ],
    }), encoding="utf-8")
    return curseforge_packages.load(path)


if __name__ == "__main__":
    unittest.main()
