import contextlib
import io
import json
import sys
import tempfile
import unittest
import zipfile
from unittest import mock
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

    def run_main(self, *arguments):
        stdout, stderr = io.StringIO(), io.StringIO()
        argv = ["plan", "--current-tag", "v1", "--list-flavors", *arguments]
        with mock.patch.object(sys, "argv", argv), \
                contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            code = plan_curseforge_release.main()
        return code, stdout.getvalue().split(), stderr.getvalue()

    def test_a_tag_without_forever_settings_refuses_a_requested_forever_flavor(self):
        for flavors in ('["forever"]', '["retail","forever"]'):
            code, output, error = self.run_main("--no-forever", "--flavors", flavors)
            self.assertEqual(code, 1, flavors)
            self.assertEqual(output, [], flavors)
            self.assertIn("v1", error)

    def test_a_tag_without_forever_settings_defaults_to_retail_only(self):
        self.assertEqual(self.run_main("--no-forever")[:2], (0, ["retail"]))
        self.assertEqual(self.run_main("--no-forever", "--flavors", '["retail"]')[:2], (0, ["retail"]))
        self.assertEqual(self.run_main()[1], ["retail", "forever"])

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
                    "flavor": "retail",
                },
                {
                    "voice": "Voice_A",
                    "addon": "Addon_Two",
                    "project_id": 12,
                    "relations": "",
                    "archive": "WoWVoxPacks_Voice_A_Addon_Two_v2.zip",
                    "flavor": "retail",
                },
                {
                    "voice": "Voice_B",
                    "addon": "Addon_One",
                    "project_id": 21,
                    "relations": "dependency:requiredDependency",
                    "archive": "WoWVoxPacks_Voice_B_Addon_One_v2.zip",
                    "flavor": "retail",
                },
                {
                    "voice": "Voice_B",
                    "addon": "Addon_Two",
                    "project_id": 22,
                    "relations": "",
                    "archive": "WoWVoxPacks_Voice_B_Addon_Two_v2.zip",
                    "flavor": "retail",
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

    def test_forever_archive_name(self):
        self.assertEqual(
            "WoWVoxPacks_V_Callouts_Forever_vT.zip",
            plan_curseforge_release.archive_name("V", "Callouts", "vT", "forever"),
        )

    def test_selected_archives_include_forever_only_for_forever_addons(self):
        archives = plan_curseforge_release.selected_archives(curseforge_packages.load(), "vT")

        self.assertIn("WoWVoxPacks_Wavenet_E_Callouts_Forever_vT.zip", archives)
        self.assertIn("WoWVoxPacks_Studio_Q_BigWigs_Countdown_Forever_vT.zip", archives)
        self.assertIn("WoWVoxPacks_Neural2_C_NorthernSkyRaidTools_Forever_vT.zip", archives)
        self.assertIn("WoWVoxPacks_Wavenet_E_BigWigs_Voice_vT.zip", archives)
        self.assertFalse([name for name in archives if "BigWigs_Voice_Forever" in name])
        self.assertFalse([name for name in archives if "ExBoss_Forever" in name])

    def test_forever_publishes_when_previous_tag_lacks_it(self):
        catalog = write_catalog(self.root / "forever.json", forever=True)
        write_package(self.previous / archive("Voice_A", "Addon_One", "v1"), {"Addon/Core.lua": b"same"})
        write_package(self.current / archive("Voice_A", "Addon_One", "v2"), {"Addon/Core.lua": b"same"})
        write_package(self.current / forever_archive("Voice_A", "Addon_One", "v2"), {"Addon/Core.lua": b"same"})

        plan = plan_curseforge_release.plan_release(
            catalog, "v2", self.current, "v1", self.previous,
            voices=["Voice_A"], addons=["Addon_One"]
        )

        self.assertEqual(
            [{
                "voice": "Voice_A",
                "addon": "Addon_One",
                "project_id": 11,
                "relations": "dependency:requiredDependency",
                "archive": "WoWVoxPacks_Voice_A_Addon_One_Forever_v2.zip",
                "flavor": "forever",
            }],
            plan,
        )

    def test_forever_compares_against_previous_forever_archive(self):
        catalog = write_catalog(self.root / "forever.json", forever=True)
        for directory, tag in ((self.previous, "v1"), (self.current, "v2")):
            write_package(directory / archive("Voice_A", "Addon_One", tag), {"Addon/Core.lua": b"retail"})
            write_package(directory / forever_archive("Voice_A", "Addon_One", tag), {"Addon/Core.lua": b"forever"})

        plan = plan_curseforge_release.plan_release(
            catalog, "v2", self.current, "v1", self.previous,
            voices=["Voice_A"], addons=["Addon_One"]
        )

        self.assertEqual([], plan)

    def test_forever_change_is_not_hidden_by_the_retail_archive(self):
        catalog = write_catalog(self.root / "forever.json", forever=True)
        write_package(self.previous / archive("Voice_A", "Addon_One", "v1"), {"Addon/Core.lua": b"new"})
        write_package(self.previous / forever_archive("Voice_A", "Addon_One", "v1"), {"Addon/Core.lua": b"old"})
        write_package(self.current / archive("Voice_A", "Addon_One", "v2"), {"Addon/Core.lua": b"new"})
        write_package(self.current / forever_archive("Voice_A", "Addon_One", "v2"), {"Addon/Core.lua": b"new"})

        plan = plan_curseforge_release.plan_release(
            catalog, "v2", self.current, "v1", self.previous,
            voices=["Voice_A"], addons=["Addon_One"]
        )

        self.assertEqual(["forever"], [package["flavor"] for package in plan])

    def test_forced_plan_includes_forever_files_of_the_selection(self):
        catalog = write_catalog(self.root / "forever.json", forever=True)
        write_package(self.current / archive("Voice_B", "Addon_One", "v2"), {"Addon/Core.lua": b"same"})
        write_package(self.current / forever_archive("Voice_B", "Addon_One", "v2"), {"Addon/Core.lua": b"same"})

        plan = plan_curseforge_release.plan_release(
            catalog, "v2", self.current, "v1", self.previous,
            voices=["Voice_B"], addons=["Addon_One"], force=True
        )

        self.assertEqual(
            [("WoWVoxPacks_Voice_B_Addon_One_v2.zip", "retail", 21),
             ("WoWVoxPacks_Voice_B_Addon_One_Forever_v2.zip", "forever", 21)],
            [(package["archive"], package["flavor"], package["project_id"]) for package in plan],
        )

    def test_a_tag_without_forever_settings_plans_no_forever_files(self):
        """Tags cut before Forever shipped have no Forever archives or game version to upload."""
        catalog = write_catalog(self.root / "forever.json", forever=True)
        write_package(self.current / archive("Voice_A", "Addon_One", "v2"), {"Addon/Core.lua": b"same"})

        plan = plan_curseforge_release.plan_release(
            catalog, "v2", self.current, force=True,
            voices=["Voice_A"], addons=["Addon_One"], flavors=["retail"]
        )
        archives = plan_curseforge_release.selected_archives(
            catalog, "v2", addons=["Addon_One"], flavors=["retail"]
        )

        self.assertEqual(["retail"], [package["flavor"] for package in plan])
        self.assertEqual(
            ["WoWVoxPacks_Voice_A_Addon_One_v2.zip", "WoWVoxPacks_Voice_B_Addon_One_v2.zip"],
            archives,
        )

    def test_selected_flavors_follow_the_selected_addons(self):
        catalog = write_catalog(self.root / "forever.json", forever=True)

        self.assertEqual(("retail", "forever"), plan_curseforge_release.selected_flavors(catalog))
        self.assertEqual(
            ("retail",), plan_curseforge_release.selected_flavors(catalog, addons=["Addon_Two"])
        )

    def test_unknown_flavor_selection_is_fatal(self):
        with self.assertRaisesRegex(ValueError, "unknown flavor 'classic'"):
            plan_curseforge_release.selected_archives(self.catalog, "v2", flavors=["classic"])


def archive(voice: str, addon: str, tag: str) -> str:
    return f"WoWVoxPacks_{voice}_{addon}_{tag}.zip"


def forever_archive(voice: str, addon: str, tag: str) -> str:
    return f"WoWVoxPacks_{voice}_{addon}_Forever_{tag}.zip"


def write_package(path: Path, members: dict[str, bytes]) -> None:
    with zipfile.ZipFile(path, "w") as package:
        for name, content in members.items():
            package.writestr(name, content)


def write_catalog(path: Path, forever: bool = False):
    addon_one = {"name": "Addon_One", "slug": "one", "relations": [
        {"slug": "dependency", "type": "requiredDependency"}
    ]}
    if forever:
        addon_one["flavors"] = ["retail", "forever"]
    path.write_text(json.dumps({
        "voices": [
            {"name": "Voice_A", "description": "A",
             "projects": {"Addon_One": 11, "Addon_Two": 12}},
            {"name": "Voice_B", "description": "B",
             "projects": {"Addon_One": 21, "Addon_Two": 22}},
        ],
        "addons": [
            addon_one,
            {"name": "Addon_Two", "slug": "two", "relations": []},
        ],
    }), encoding="utf-8")
    return curseforge_packages.load(path)


if __name__ == "__main__":
    unittest.main()
