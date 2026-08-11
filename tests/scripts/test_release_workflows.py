import json
import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]


class ReleaseWorkflowTests(unittest.TestCase):
    def test_curseforge_publish_has_one_automatic_release_path(self):
        publisher = (
            REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml"
        ).read_text(encoding="utf-8")
        release_creator = (
            REPOSITORY_ROOT / ".github/workflows/create-release.yml"
        ).read_text(encoding="utf-8")

        self.assertIn("  release:\n    types: [ published ]", publisher)
        self.assertNotIn("\n  workflow_run:", publisher)
        self.assertIn(
            "GITHUB_TOKEN: ${{ secrets.WORKFLOW_RELEASE_PAT }}", release_creator
        )

    def test_northern_sky_raid_tools_publish_contract_uses_per_voice_matrix(self):
        publisher = (
            REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml"
        ).read_text(encoding="utf-8")
        expected_projects = {
            "Wavenet_E": "1648855",
            "Neural2_C": "1648938",
            "Studio_Q": "1648940",
        }
        match = re.search(
            r"voice-to-addon-to-project-id-json:\n\s+- '(\{.*?\})'",
            publisher,
            re.DOTALL,
        )
        self.assertIsNotNone(match)
        project_matrix = json.loads(match.group(1))
        self.assertEqual(
            expected_projects,
            {
                voice: str(addons["NorthernSkyRaidTools"])
                for voice, addons in project_matrix.items()
            },
        )
        self.assertIn('"NorthernSkyRaidTools"', publisher)
        self.assertNotIn("publish-northern-sky-raid-tools:", publisher)
        self.assertNotIn("Standard_D", publisher)
        self.assertNotIn("Studio_O", publisher)
        self.assertNotIn("CF_" + "SPEECH_PROJECT_ID", publisher)
        self.assertNotIn("CF_NORTHERN_SKY_" + "RAID_TOOLS_PROJECT_ID", publisher)
        self.assertNotIn("WoWVoxPacks_NorthernSkyRaidTools_${{ env.RELEASE_TAG }}.zip", publisher)
        self.assertIn("needs: [ publish-addon ]", publisher)

    def test_release_addon_fallback_includes_northern_sky_raid_tools(self):
        publisher = (
            REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml"
        ).read_text(encoding="utf-8")
        match = re.search(
            r"addon: .*?inputs\.addons \|\| '([^']+)'",
            publisher,
        )
        self.assertIsNotNone(match)
        self.assertEqual(
            [
                "BigWigs_Voice",
                "BigWigs_Countdown",
                "Callouts",
                "ExBoss",
                "NorthernSkyRaidTools",
            ],
            json.loads(f"[{match.group(1)}]"),
        )


if __name__ == "__main__":
    unittest.main()
