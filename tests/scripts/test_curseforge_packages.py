import json
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "scripts"))

import curseforge_packages


class CurseForgePackageTests(unittest.TestCase):
    def test_production_catalog_preserves_published_order_and_metadata(self):
        catalog = curseforge_packages.load()

        self.assertEqual(("Wavenet_E", "Neural2_C", "Studio_Q"), catalog.voice_names)
        self.assertEqual(
            ("BigWigs_Voice", "BigWigs_Countdown", "Callouts", "ExBoss",
             "NorthernSkyRaidTools"),
            catalog.addon_names,
        )
        self.assertEqual(1648938, catalog.project_id("Neural2_C", "NorthernSkyRaidTools"))
        self.assertEqual("en_US male", catalog.voice_description("Studio_Q"))
        self.assertEqual("northernskyraidtools", catalog.addon_slug("NorthernSkyRaidTools"))
        self.assertEqual(
            "exboss:requiredDependency,libsharedmedia-3-0:optionalDependency",
            catalog.relations("ExBoss"),
        )

    def test_duplicate_voice_is_rejected(self):
        data = valid_catalog()
        data["voices"].append(data["voices"][0])

        with self.assertRaisesRegex(ValueError, "duplicate voice 'Voice'"):
            load_temporary(data)

    def test_missing_project_is_rejected(self):
        data = valid_catalog()
        del data["voices"][0]["projects"]["Addon"]

        with self.assertRaisesRegex(ValueError, "Voice.*missing project.*Addon"):
            load_temporary(data)

    def test_unknown_project_addon_is_rejected(self):
        data = valid_catalog()
        data["voices"][0]["projects"]["Unknown"] = 22

        with self.assertRaisesRegex(ValueError, "Voice.*unknown project addon.*Unknown"):
            load_temporary(data)

    def test_non_positive_project_id_is_rejected(self):
        data = valid_catalog()
        data["voices"][0]["projects"]["Addon"] = 0

        with self.assertRaisesRegex(ValueError, "Voice/Addon.*positive integer"):
            load_temporary(data)

    def test_unsupported_relation_type_is_rejected(self):
        data = valid_catalog()
        data["addons"][0]["relations"][0]["type"] = "bundled"

        with self.assertRaisesRegex(ValueError, "Addon.*unsupported relation type.*bundled"):
            load_temporary(data)


def valid_catalog():
    return {
        "voices": [
            {
                "name": "Voice",
                "description": "en_US test",
                "projects": {"Addon": 11},
            }
        ],
        "addons": [
            {
                "name": "Addon",
                "slug": "addon",
                "relations": [
                    {"slug": "dependency", "type": "requiredDependency"}
                ],
            }
        ],
    }


def load_temporary(data):
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / "catalog.json"
        path.write_text(json.dumps(data), encoding="utf-8")
        return curseforge_packages.load(path)


if __name__ == "__main__":
    unittest.main()
