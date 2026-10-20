import importlib.util
import json
import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]


def job(workflow: str, name: str) -> str:
    """One job's YAML, from its key to the next job's key."""
    match = re.search(rf"(?ms)^  {re.escape(name)}:\n(.*?)(?=^  [\w-]+:\n|\Z)", workflow)
    assert match, f"no job {name}"
    return match.group(1)


def step(job_text: str, name: str) -> str:
    """One step's YAML, from its name to the next step."""
    return job_text.split(f"- name: {name}\n", 1)[1].split("\n      - ", 1)[0]


def checkout_steps(workflow: str) -> list[str]:
    """The complete YAML mapping for each checkout step."""
    return [
        step
        for step in re.split(r"(?m)(?=^      - )", workflow)
        if "uses: actions/checkout@v7" in step
    ]


spec = importlib.util.spec_from_file_location(
    "game_versions", REPOSITORY_ROOT / "scripts" / "game_versions.py"
)
game_versions = importlib.util.module_from_spec(spec)
spec.loader.exec_module(game_versions)

spec = importlib.util.spec_from_file_location(
    "update_gate", REPOSITORY_ROOT / "scripts" / "update_gate.py"
)
update_gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(update_gate)


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
            "python scripts/game_versions.py --settings release-appsettings.json", publisher
        )
        self.assertIn(
            "game_versions: ${{ needs.plan-release.outputs.game_versions }}", publisher
        )

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

    def test_a_new_live_build_reaches_the_update_pull_request(self):
        """The settings and the TOCs must land together, or every later run rebuilds for nothing.

        A PR carrying the regenerated TOCs without appsettings.json would release under the
        old interfaces' tag and game versions, and the next run would detect the same build
        again.
        """
        updater = (
            REPOSITORY_ROOT / ".github/workflows/update.yml"
        ).read_text(encoding="utf-8")
        pull_request_job = updater.split("\n  create-pull-request:", 1)[1]
        header = pull_request_job.split("\n    steps:", 1)[0]

        self.assertIn("check-live-build", header.split("needs:", 1)[1].split("\n", 1)[0])
        self.assertIn("needs.check-live-build.outputs.needs_update == 'true'", header)

        follow_step = pull_request_job.split("- name: Follow the live build", 1)[1].split(
            "\n      - ", 1
        )[0]
        self.assertIn("if: ${{ needs.check-live-build.outputs.needs_update == 'true' }}", follow_step)
        self.assertIn("LIVE_BUILD: ${{ needs.check-live-build.outputs.build }}", follow_step)
        self.assertIn('--settings appsettings.json --follow "$LIVE_BUILD"', follow_step)
        self.assertLess(
            pull_request_job.index("- name: Follow the live build"),
            pull_request_job.index("- name: Run addon builder"),
        )

        add_paths = pull_request_job.split("add-paths: |", 1)[1].split("sign-commits:", 1)[0]
        self.assertIn("appsettings.json", add_paths)

    def test_the_updater_never_pays_twice_for_the_same_audio(self):
        """Each run renders on a fresh checkout of main, so anything not merged is rendered again.

        An open update PR, a build that failed after rendering, or a run cancelled mid-build
        would each have the next scheduled run pay for the same files a second time.
        """
        updater = (
            REPOSITORY_ROOT / ".github/workflows/update.yml"
        ).read_text(encoding="utf-8")
        concurrency = updater.split("\nconcurrency:", 1)[1].split("\njobs:", 1)[0]
        self.assertIn("cancel-in-progress: false", concurrency)

        label = update_gate.FAILURE_LABEL
        gate = job(updater, "check-earlier-build")
        self.assertIn("continue-on-error: true", job(updater, "check-nsrt-vocabulary"))
        self.assertIn("continue-on-error: true", job(updater, "check-live-build"))
        self.assertNotIn("continue-on-error", gate)
        self.assertIn(
            "gh pr list --state open --limit 100 --json number,headRefName,isCrossRepository > pulls.json",
            gate,
        )
        self.assertIn(f"gh issue list --state open --label {label} --json number > issues.json", gate)
        self.assertIn("python3 scripts/update_gate.py --pulls pulls.json --issues issues.json", gate)
        self.assertIn('--event "$EVENT_NAME" --run-sha "$GITHUB_SHA" --main-sha "$main_sha"', gate)
        self.assertIn('--output "$GITHUB_OUTPUT"', gate)
        # A scheduled run has no event payload, so nothing may read the branch from github.event.
        self.assertIn('main_sha=$(gh api "repos/$GITHUB_REPOSITORY/commits/HEAD" --jq .sha)', gate)
        self.assertNotIn("github.event.repository", updater)
        self.assertIn("EVENT_NAME: ${{ github.event_name }}", gate)

        pull_request_job = updater.split("\n  create-pull-request:", 1)[1]
        header = pull_request_job.split("\n    steps:", 1)[0]
        self.assertIn("check-earlier-build", header.split("needs:", 1)[1].split("\n", 1)[0])
        self.assertIn("if: ${{ needs.check-earlier-build.outputs.blocked == 'false' && (", header)
        self.assertIn("timeout-minutes: 180", header)
        self.assertIn(f"branch: {update_gate.UPDATE_BRANCH_PREFIX}", pull_request_job)

        builder = step(pull_request_job, "Run addon builder")
        self.assertIn("id: builder", builder)
        # Only an interrupt the builder itself receives records its partial renders, and it has
        # to arrive with time left before the job's own limit.
        self.assertIn(
            "run: timeout -s INT -k 2m 155m dotnet run --project src/WoWVoxPack.Builder/WoWVoxPack.Builder.csproj",
            builder,
        )

        # Renders a failed build paid for come back before anything is planned or rendered, and
        # the free plan trips the builder's limits before the paid run spends on any pack.
        order = [
            "Restore the renders a failed build kept",
            "Plan the build",
            "Run addon builder",
            "Snapshot the rendered output",
            "Create a pull request",
        ]
        positions = [pull_request_job.index(f"- name: {name}\n") for name in order]
        self.assertEqual(positions, sorted(positions))
        restore = step(pull_request_job, "Restore the renders a failed build kept")
        self.assertIn('actions/artifacts?name=rendered-output&per_page=100', restore)
        self.assertIn(
            'select((.expired | not) and .workflow_run.head_branch == "main")] | sort_by(.created_at) | reverse',
            restore,
        )
        self.assertIn('gh run download "$run_id" --name rendered-output --dir "$RUNNER_TEMP/restore-$artifact_id"', restore)
        self.assertIn(
            'python3 scripts/rendered_output.py restore --from "$RUNNER_TEMP/restore-$artifact_id"', restore
        )
        self.assertNotIn("|| true", restore)
        self.assertIn(
            "run: dotnet run --project src/WoWVoxPack.Builder/WoWVoxPack.Builder.csproj -- --Matrix:DryRun true",
            step(pull_request_job, "Plan the build"),
        )
        snapshot = step(pull_request_job, "Snapshot the rendered output")
        self.assertIn(
            "if: always() && (steps.builder.outcome == 'success' || "
            "steps.builder.outcome == 'failure' || steps.builder.outcome == 'cancelled')",
            snapshot,
        )
        self.assertIn('python3 scripts/rendered_output.py collect --into "$RUNNER_TEMP/rendered-output"', snapshot)
        self.assertLess(snapshot.index("pkill -INT -f 'WoWVoxPack\\.Builder'"), snapshot.index("rendered_output.py collect"))
        keep = step(pull_request_job, "Keep the renders this failed build paid for")
        self.assertIn(
            "if: (failure() || cancelled()) && steps.snapshot.outcome == 'success' && "
            "steps.create-pull-request.outputs.pull-request-number == ''",
            keep,
        )
        self.assertIn("name: rendered-output", keep)
        self.assertIn("path: ${{ runner.temp }}/rendered-output", keep)
        # Once a PR holds the audio, no kept render may come back, even if auto-merge then
        # fails, and a failed delete must not stop the failure issue from closing.
        discard = step(pull_request_job, "Discard kept renders once a PR has them")
        self.assertIn("if: always() && steps.create-pull-request.outputs.pull-request-number != ''", discard)
        self.assertIn("continue-on-error: true", discard)
        self.assertIn('select((.expired | not) and .workflow_run.head_branch == "main") | .id', discard)
        self.assertIn('gh api -X DELETE "repos/$GITHUB_REPOSITORY/actions/artifacts/$artifact_id"', discard)

        close = pull_request_job.split("- name: Close the failed-build issue", 1)[1].split("\n      - ", 1)[0]
        self.assertIn("if: success()", close)
        self.assertIn(f"gh issue list --state open --label {label}", close)
        self.assertIn('gh issue close "$number"', close)

        report = pull_request_job.split("- name: File an issue when a build fails after rendering", 1)[1]
        self.assertIn(
            "if: (failure() || cancelled()) && (steps.builder.outcome == 'success' || "
            "steps.builder.outcome == 'failure' || steps.builder.outcome == 'cancelled') && "
            "steps.create-pull-request.outputs.pull-request-number == ''",
            report,
        )
        self.assertIn(f"gh label create {label} --force", report)
        self.assertIn(
            f"number=$(gh issue list --state open --label {label} --json number --jq '.[0].number')",
            report,
        )
        self.assertIn(f"--label {label}", report.split("gh issue create", 1)[1])
        self.assertIn('gh issue comment "$number"', report)

    def test_the_live_build_is_validated_before_the_log_prints_it(self):
        """The fetched table is untrusted; an unvalidated echo could inject workflow commands."""
        updater = (
            REPOSITORY_ROOT / ".github/workflows/update.yml"
        ).read_text(encoding="utf-8")
        check = updater.split("\n  check-live-build:", 1)[1].split("\n  create-pull-request:", 1)[0]

        self.assertIn("https://us.version.battle.net/v2/products/wow/versions", check)
        self.assertIn('build=$(python3 scripts/game_versions.py --live-build versions.txt)', check)
        self.assertLess(
            check.index("--live-build versions.txt"), check.index('echo "Live build: ${build}"')
        )
        self.assertIn("continue-on-error: true", check)

    def test_ffmpeg_comes_from_a_host_runners_can_reach(self):
        """setup-ffmpeg's johnvansickle.com source failed every CI run from runners."""
        installer = REPOSITORY_ROOT / "scripts/install_ffmpeg.sh"
        self.assertTrue(installer.stat().st_mode & 0o111, "install_ffmpeg.sh must be executable")
        commands = [
            line for line in installer.read_text().splitlines() if not line.startswith("#")
        ]
        script = "\n".join(commands)
        # A rolling "latest" release is deleted and re-created daily, so only a dated
        # autobuild with a checksum held here is reproducible.
        self.assertRegex(
            script,
            r"\nrelease=https://github\.com/BtbN/FFmpeg-Builds/releases/download/autobuild-\d{4}-\d{2}-\d{2}-\d{2}-\d{2}\n",
        )
        self.assertRegex(script, r"\nsha256=[0-9a-f]{64}\n")
        self.assertEqual(script.count("sha256="), 1, "the checksum must be the one held here")
        # The workflow runs the script through its shebang, so without -e a failed check
        # would print FAILED and install the download anyway.
        self.assertIn("set -euo pipefail", commands)
        self.assertNotIn("set +", script)
        self.assertIn(
            'curl -sSfL --retry 5 --retry-delay 10 --retry-all-errors -o "$asset.tar.xz" "$release/$asset.tar.xz"',
            script,
        )
        self.assertIn('echo "$sha256  $asset.tar.xz" | sha256sum -c -\n', script + "\n")
        self.assertIn('echo "$RUNNER_TEMP/$asset/bin" >> "$GITHUB_PATH"', script)
        self.assertNotIn("||", script)

        for workflow in ("ci.yml", "update.yml"):
            contents = (REPOSITORY_ROOT / ".github/workflows" / workflow).read_text(
                encoding="utf-8"
            )
            self.assertNotIn("setup-ffmpeg", contents, workflow)
            self.assertIn("run: scripts/install_ffmpeg.sh", contents, workflow)

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

    def test_every_upload_carries_the_notes_the_release_generated(self):
        """CurseForge shows whatever the upload sends, and it sent nothing until now.

        The action takes the changelog as an input; omitting it publishes a file with an
        empty changelog page, which is what every release through v12.1.0-r9 did.
        """
        release_creator = (
            REPOSITORY_ROOT / ".github/workflows/create-release.yml"
        ).read_text(encoding="utf-8")
        publisher = (
            REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml"
        ).read_text(encoding="utf-8")

        self.assertIn("python scripts/release_notes.py", release_creator)
        self.assertIn(
            "changelog: ${{ steps.release-notes.outputs.changelog }}", release_creator
        )
        self.assertIn(
            "changelog: ${{ needs.create-release.outputs.changelog }}", release_creator
        )
        # A multiline value only survives $GITHUB_OUTPUT behind a heredoc delimiter.
        self.assertIn('echo "changelog<<${delimiter}"', release_creator)
        self.assertIn("      changelog:\n        description:", publisher)
        self.assertIn("changelog: ${{ needs.plan-release.outputs.changelog }}", publisher)
        self.assertIn('echo "changelog<<${delimiter}"', publisher)
        self.assertIn("changelog_type: markdown", publisher)

    def test_generating_the_notes_cannot_release_a_tag_the_run_would_skip(self):
        """The notes step runs before packaging and must honour the same skip."""
        release_creator = (
            REPOSITORY_ROOT / ".github/workflows/create-release.yml"
        ).read_text(encoding="utf-8")

        step = release_creator.index("- name: Generate release notes")
        end = release_creator.find("\n      - name: ", step)
        self.assertIn(
            "if: steps.existing-release.outputs.skip != 'true'", release_creator[step:end]
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

    def test_automatic_publish_requires_changed_package_contents(self):
        """Automatic releases gate one plan; manual recovery still forces the selection."""
        publisher = (
            REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml"
        ).read_text(encoding="utf-8")

        called = publisher.split("workflow_call:", 1)[1].split("workflow_dispatch:", 1)[0]
        dispatched = publisher.split("workflow_dispatch:", 1)[1].split("concurrency:", 1)[0]
        self.assertRegex(called, r"force_publish:\n(?:        .*\n)*?        default: false")
        self.assertRegex(dispatched, r"force_publish:\n(?:        .*\n)*?        default: true")
        plan = publisher.index("  plan-release:")
        upload = publisher.index("  publish-addon:")
        self.assertIn("python scripts/plan_curseforge_release.py", publisher[plan:upload])
        self.assertIn("--force", publisher[plan:upload])
        self.assertNotIn("compare_addon_packages.py", publisher[upload:])
        self.assertIn("if: matrix.publish == true", publisher[upload:])

    def test_publisher_plans_once_before_expanding_the_upload_matrix(self):
        publisher = (
            REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml"
        ).read_text(encoding="utf-8")
        plan = publisher[publisher.index("  plan-release:"):publisher.index("  publish-addon:")]
        upload = publisher[publisher.index("  publish-addon:"):]

        self.assertEqual(
            1, publisher.count('packages=$(python scripts/plan_curseforge_release.py')
        )
        self.assertIn("uses: actions/checkout@v7", plan)
        self.assertIn("packages:", plan)
        self.assertIn("needs: plan-release", upload)
        self.assertIn("matrix: ${{ fromJSON(needs.plan-release.outputs.matrix) }}", upload)
        self.assertNotIn("uses: actions/checkout@v7", upload)
        self.assertNotIn("voice-to-addon-to-project-id-json", publisher)
        self.assertIn("project_id: ${{ matrix.project_id }}", upload)

    def test_non_build_checkouts_exclude_generated_output(self):
        """Jobs that never read output must not materialize half a gigabyte of audio."""
        for workflow in ("ci.yml", "publish-to-curseforge.yml", "update.yml"):
            contents = (REPOSITORY_ROOT / ".github/workflows" / workflow).read_text(
                encoding="utf-8"
            )
            steps = checkout_steps(contents)
            self.assertTrue(steps, workflow)
            for checkout in steps:
                if "fetch-depth: 0" in checkout:
                    continue
                self.assertIn("filter: blob:none", checkout, workflow)
                self.assertIn("sparse-checkout: |", checkout, workflow)
                self.assertIn("          /*", checkout, workflow)
                self.assertIn("          !/output/", checkout, workflow)
                self.assertIn("sparse-checkout-cone-mode: false", checkout, workflow)

    def test_update_checkout_avoids_historical_audio(self):
        """The updater needs current output, but never the audio from older commits."""
        updater = (REPOSITORY_ROOT / ".github/workflows/update.yml").read_text(
            encoding="utf-8"
        )
        checkout = next(
            step for step in checkout_steps(updater) if "fetch-depth: 0" in step
        )

        self.assertIn("filter: blob:none", checkout)
        self.assertIn("fetch-depth: 0", checkout)
        self.assertNotIn("!/output/", checkout)

    def test_curseforge_project_ids_come_from_the_planned_matrix(self):
        publisher = (
            REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml"
        ).read_text(encoding="utf-8")
        upload = publisher[publisher.index("  publish-addon:"):]

        self.assertIn("project_id: ${{ matrix.project_id }}", upload)
        self.assertIn("relations: ${{ matrix.relations }}", upload)
        self.assertNotRegex(publisher, r"\b1648(?:855|938|940)\b")
        self.assertNotIn("voice-to-addon-to-project-id-json", publisher)


if __name__ == "__main__":
    unittest.main()
