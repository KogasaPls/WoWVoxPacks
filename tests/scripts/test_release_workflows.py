import json
import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]


class ReleaseWorkflowTests(unittest.TestCase):
    def test_curseforge_publish_has_one_automatic_release_path(self):
        """create-release calls the publisher; no event may reach it a second way.

        A release the workflow's own GITHUB_TOKEN creates emits no `release` event, so both
        v12.1.0-r1 and -r2 published to GitHub and never reached CurseForge while every job
        reported success. The call is the path; an event trigger alongside it would publish
        the same tag twice.
        """
        publisher = (
            REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml"
        ).read_text(encoding="utf-8")
        release_creator = (
            REPOSITORY_ROOT / ".github/workflows/create-release.yml"
        ).read_text(encoding="utf-8")

        self.assertIn("\n  workflow_call:", publisher)
        self.assertNotIn("\n  release:", publisher)
        self.assertNotIn("\n  workflow_run:", publisher)
        self.assertIn(
            "uses: ./.github/workflows/publish-to-curseforge.yml", release_creator
        )
        self.assertIn("needs: create-release", release_creator)
        self.assertIn("secrets: inherit", release_creator)
        self.assertIn(
            "release_tag: ${{ needs.create-release.outputs.release_tag }}",
            release_creator,
        )
        # The action takes a token input; env.GITHUB_TOKEN leaves it on the default one.
        self.assertIn("token: ${{ secrets.WORKFLOW_RELEASE_PAT }}", release_creator)
        self.assertNotIn(
            "GITHUB_TOKEN: ${{ secrets.WORKFLOW_RELEASE_PAT }}", release_creator
        )

    def test_release_creator_cannot_release_the_same_tag_twice(self):
        """The PAT that fixes the event also makes tag pushes re-enter this workflow.

        `create-release` still triggers on `push: tags: ['v*']`, and creating a release
        creates its tag. Without the guard the second run repackages, updates the release
        in place and uploads every archive to CurseForge again.
        """
        release_creator = (
            REPOSITORY_ROOT / ".github/workflows/create-release.yml"
        ).read_text(encoding="utf-8")

        self.assertIn("gh release view \"$RELEASE_TAG\"", release_creator)
        self.assertIn(
            "released: ${{ steps.existing-release.outputs.skip != 'true' }}", release_creator
        )
        self.assertIn(
            "if: needs.create-release.outputs.released == 'true'", release_creator
        )
        for guarded in ("- name: Package artifacts", "- name: Create GitHub Release"):
            step = release_creator.index(guarded)
            self.assertIn(
                "if: steps.existing-release.outputs.skip != 'true'",
                release_creator[step:step + 200],
            )

    def test_publisher_asks_for_no_permission_its_caller_lacks(self):
        """A called workflow can only be granted less than the job that calls it.

        The repository defaults its token to read, so a job in the publisher asking for
        anything more fails the whole release run rather than that one job.
        """
        publisher = (
            REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml"
        ).read_text(encoding="utf-8")
        release_creator = (
            REPOSITORY_ROOT / ".github/workflows/create-release.yml"
        ).read_text(encoding="utf-8")

        self.assertNotIn("actions: write", publisher)
        self.assertNotIn("rerun-failed-jobs", publisher)
        call = release_creator.index("uses: ./.github/workflows/publish-to-curseforge.yml")
        self.assertIn("permissions:", release_creator[call - 300:call])
        self.assertIn("contents: read", release_creator[call - 300:call])

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
