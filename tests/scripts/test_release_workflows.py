import importlib.util
import json
import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]

spec = importlib.util.spec_from_file_location(
    "game_versions", REPOSITORY_ROOT / "scripts" / "game_versions.py"
)
game_versions = importlib.util.module_from_spec(spec)
spec.loader.exec_module(game_versions)


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
        """The toc names a patch, and create-release names the tag after the same one.

        BigWigs_Voice numbers its own re-releases inside a patch (v12.1.0, then v12.1.0.1),
        so the version it tracks is not one of ours to advertise. Interfaces is what the
        addons say they support, and the tag is built from the highest of them.
        """
        settings = REPOSITORY_ROOT / "appsettings.json"
        declared = json.loads(settings.read_text(encoding="utf-8"))["AddOn"]["Version"]

        self.assertEqual(declared, game_versions.game_versions(settings)[-1])

    def test_neither_the_tag_nor_the_upload_carries_an_upstream_release_number(self):
        """BigWigs_Voice numbers its own re-releases inside a patch: v12.1.0, then v12.1.0.1.

        WoW never shipped a 12.1.0.1, so CurseForge cannot resolve it and players cannot
        match it. Both the tag and the upload have to name a patch the addons declare.
        """
        release_creator = (
            REPOSITORY_ROOT / ".github/workflows/create-release.yml"
        ).read_text(encoding="utf-8")
        publisher = (
            REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml"
        ).read_text(encoding="utf-8")

        self.assertIn(
            'base="v$(python scripts/game_versions.py --settings appsettings.json --highest)"',
            release_creator,
        )
        self.assertNotIn("base=$(cat bigwigs-voice-version.txt)", release_creator)
        # The revision comes from what has shipped. A counter file has nothing to reset it
        # when the game version moves, and it refills holes left by a deleted release.
        self.assertIn('for tag in $(git tag -l "${base}-r*")', release_creator)
        self.assertNotIn("release-revision.txt", release_creator)
        self.assertIn(
            "python scripts/game_versions.py --settings appsettings.json", publisher
        )
        self.assertIn("game_versions: ${{ env.RELEASE_GAME_VERSIONS }}", publisher)

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

    def test_every_nsrt_vocabulary_the_builder_reads_stays_current(self):
        """Each generated vocabulary flows through the update PR; only the last is hand-owned.

        The alert-text vocabulary once had no automated path into the repo: the coverage
        report was advisory only, and the "Waves" alert shipped unspoken for two weeks. A
        vocabulary the builder reads must be regenerated and committed by the updater unless
        it is the hand-enumerated composed-string file.
        """
        source = (
            REPOSITORY_ROOT
            / "src/WoWVoxPack.AddOns.Callouts/NorthernSkyRaidToolsVocabulary.cs"
        ).read_text(encoding="utf-8")
        names = re.findall(r'"(nsrt-[\w-]+\.txt)"', source)
        self.assertEqual(
            ["nsrt-vocabulary.txt", "nsrt-alert-vocabulary.txt", "nsrt-extra-vocabulary.txt"],
            names,
        )

        updater = (
            REPOSITORY_ROOT / ".github/workflows/update.yml"
        ).read_text(encoding="utf-8")
        self.assertIn("add-paths: |", updater)
        add_paths = updater.split("add-paths: |", 1)[1].split("sign-commits:", 1)[0]
        for name in names:
            self.assertTrue((REPOSITORY_ROOT / name).is_file(), name)
        for generated in names[:-1]:
            new_name = generated.replace(".txt", ".new")
            self.assertIn(f"mv {new_name} {generated}", updater)
            self.assertIn(generated, add_paths)
        self.assertNotIn(names[-1], add_paths)

    def test_a_changed_composed_site_fails_the_update_run(self):
        """Hand-enumerated strings cannot regenerate, so drift must fail, not print.

        The composed sites are the one class the alert-vocabulary generation cannot cover:
        a new site's strings exist only at runtime. The verify step diffs the sites against
        the tracked snapshot, and a mismatch fails the job, which files the issue.
        """
        updater = (
            REPOSITORY_ROOT / ".github/workflows/update.yml"
        ).read_text(encoding="utf-8")
        self.assertIn("--composed nsrt-composed-sites.txt", updater)
        self.assertTrue((REPOSITORY_ROOT / "nsrt-composed-sites.txt").is_file())

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
