import json
import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]


def committed_paths(workflow: str) -> list[str]:
    block = re.search(r"\n( +)add-paths: \|\n((?:\1 +\S+\n)+)", workflow)
    return block.group(2).split() if block else []


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

    def test_the_version_in_every_toc_is_the_version_the_tag_is_built_from(self):
        """Two files decide one number, and only one of them names the release.

        create-release builds the tag from bigwigs-voice-version.txt, while the generated TOCs
        take theirs from appsettings.json. Bump one without the other and the addons players
        install advertise a version that no release has.
        """
        settings = json.loads(
            (REPOSITORY_ROOT / "appsettings.json").read_text(encoding="utf-8")
        )
        tracked = (
            REPOSITORY_ROOT / "bigwigs-voice-version.txt"
        ).read_text(encoding="utf-8").strip()

        self.assertEqual(tracked, "v" + settings["AddOn"]["Version"])

    def test_the_updater_moves_both_files_that_decide_the_version(self):
        """Upstream moves on a schedule, with no human to bump the second file.

        The check above only reports the mismatch, and it reports it as a red update PR.
        The updater has to write appsettings.json and commit it for that PR to be green.
        """
        updater = (
            REPOSITORY_ROOT / ".github/workflows/update.yml"
        ).read_text(encoding="utf-8")

        self.assertIn(
            "python scripts/set_addon_version.py", updater
        )
        self.assertIn("--settings appsettings.json --version", updater)
        self.assertIn("appsettings.json", committed_paths(updater))

    def test_the_builder_and_the_updater_agree_on_how_far_a_pack_may_shrink(self):
        """Both refuse a collapsed vocabulary, and the builder runs second.

        If the builder draws its line ahead of the workflow's, an update the workflow accepts
        fails the sync job instead, taking every other pack's update down with it.
        """
        updater = (
            REPOSITORY_ROOT / ".github/workflows/update.yml"
        ).read_text(encoding="utf-8")
        manifest = (
            REPOSITORY_ROOT / "src/WoWVoxPack.Core/TTS/SoundFileManifest.cs"
        ).read_text(encoding="utf-8")

        self.assertIn('"${new_count}" -lt $(( old_count / 2 ))', updater)
        self.assertIn("(_recordingsByFileName.Count + 1) / 2", manifest)

    def test_release_creator_cannot_release_the_same_tag_twice(self):
        """The PAT that fixes the event also makes tag pushes re-enter this workflow.

        `create-release` still triggers on `push: tags: ['v*']`, and creating a release
        creates its tag. Without the guard the second run repackages, updates the release
        in place and uploads every archive to CurseForge again.
        """
        release_creator = (
            REPOSITORY_ROOT / ".github/workflows/create-release.yml"
        ).read_text(encoding="utf-8")

        self.assertIn("releases/tags/${RELEASE_TAG}", release_creator)
        # 200 skips, 404 releases, and anything else stops rather than guessing "not released".
        self.assertIn("Could not tell whether ${RELEASE_TAG} is already released", release_creator)
        self.assertIn(
            "released: ${{ steps.existing-release.outputs.skip != 'true' }}", release_creator
        )
        self.assertIn(
            "if: needs.create-release.outputs.released == 'true'", release_creator
        )
        for guarded in ("- name: Package artifacts", "- name: Create GitHub Release"):
            step = release_creator.index(guarded)
            # Only this step's own lines: a window that runs on reaches the next step's `if:`
            # and passes for a step that lost its guard.
            end = release_creator.find("\n      - name: ", step + len(guarded))
            if end == -1:
                end = len(release_creator)
            self.assertIn(
                "if: steps.existing-release.outputs.skip != 'true'",
                release_creator[step:end],
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
