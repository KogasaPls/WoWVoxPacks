import importlib.util
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "scripts"))

spec = importlib.util.spec_from_file_location(
    "release_notes", REPOSITORY_ROOT / "scripts" / "release_notes.py"
)
release_notes = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release_notes)


class RenderTests(unittest.TestCase):
    def test_notes_carry_the_commits_and_both_callout_lists(self):
        rendered = release_notes.render(
            "v12.1.0-r9",
            "2026-08-21",
            ["Callouts: pronounce Innervate correctly", "Northern Sky Raid Tools: voice spell assignments with the selected pack"],
            ["Adrenaline Rush"],
            ["Old Callout"],
            "https://example.invalid/compare/v12.1.0-r8...v12.1.0-r9",
        )

        self.assertIn("## v12.1.0-r9 (2026-08-21)", rendered)
        self.assertIn(
            "[Full Changelog](https://example.invalid/compare/v12.1.0-r8...v12.1.0-r9)",
            rendered,
        )
        self.assertIn("- Callouts: pronounce Innervate correctly", rendered)
        self.assertIn("### New callouts\n\n- Adrenaline Rush", rendered)
        self.assertIn("### Removed callouts\n\n- Old Callout", rendered)

    def test_an_empty_section_is_omitted_rather_than_left_bare(self):
        rendered = release_notes.render("v1", "2026-08-21", ["Northern Sky Raid Tools: correct Ula'tek Left and Right audio"], [], [])

        self.assertNotIn("New callouts", rendered)
        self.assertNotIn("Removed callouts", rendered)

    def test_a_long_list_states_what_it_left_out(self):
        """A season's first sync moves hundreds of names; a silent cut reads as the whole list."""
        names = [f"Callout {index:03d}" for index in range(100)]

        rendered = release_notes.render("v1", "2026-08-21", [], names, [], limit=40)

        self.assertIn("- Callout 039", rendered)
        self.assertNotIn("- Callout 040", rendered)
        self.assertIn("- ... and 60 more", rendered)


class GitTests(unittest.TestCase):
    """The parts that read history, exercised against a real repository."""

    def setUp(self):
        self.directory = Path(tempfile.mkdtemp())
        self.addCleanup(subprocess.run, ["rm", "-rf", str(self.directory)])
        self.git("init", "-q", "-b", "main")
        self.git("config", "user.email", "test@example.invalid")
        self.git("config", "user.name", "Test")
        self.git("config", "commit.gpgsign", "false")

    def git(self, *arguments):
        return release_notes.git(*arguments, repository=self.directory)

    def commit(self, message, **files):
        for name, content in files.items():
            path = self.directory / name.replace("__", "-")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8")
        log = self.directory / "log.txt"
        log.write_text(f"{log.read_text() if log.exists() else ''}{message}\n", encoding="utf-8")
        self.git("add", "-A")
        self.git("commit", "-q", "-m", message)

    def notes(self):
        return subprocess.check_output(
            [sys.executable, str(REPOSITORY_ROOT / "scripts/release_notes.py"),
             "--repository", str(self.directory), "--previous", "v1.0.0-r1", "--tag", "v1.0.0-r2"],
            text=True,
        )

    def test_notes_exclude_internal_work_and_addons_that_have_not_shipped_changes(self):
        self.commit("Add initial NSRT raid callouts", **{
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": "old",
            "output/Test/BigWigs_Voice_WoWVoxPacks_Test/Core.lua": "old",
        })
        self.git("tag", "v1.0.0-r1")
        self.commit("feat(nsrt): add Ula'tek wave callouts", **{
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": "new",
        })
        self.commit("fix(ci): preserve renders", **{"scripts/build.py": "internal"})
        self.commit("feat(bigwigs_voice): add Kith'ix abilities", **{"src/spells.txt": "pending"})
        self.commit("perf(build): generate files faster", **{
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/SoundFiles.json": "metadata",
        })

        notes = self.notes()

        self.assertIn("- Northern Sky Raid Tools: add Ula'tek wave callouts", notes)
        self.assertNotIn("feat(", notes)
        self.assertNotIn("preserve renders", notes)
        self.assertNotIn("Kith'ix", notes)
        self.assertNotIn("generate files faster", notes)

    def test_the_render_commit_describes_a_change_whose_source_was_committed_earlier(self):
        self.commit("Add initial NSRT raid callouts", **{"output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": "old"})
        self.git("tag", "v1.0.0-r1")
        self.commit("fix(nsrt): use Left and Right audio for Ula'tek waves", **{"src/directions.txt": "fixed"})
        self.commit("fix(nsrt): use Left and Right audio for Ula'tek waves", **{"output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": "new"})

        self.assertIn("- Northern Sky Raid Tools: use Left and Right audio for Ula'tek waves", self.notes())
        self.assertEqual(1, self.notes().count("use Left and Right audio for Ula'tek waves"))

    def test_unscoped_features_need_distributed_changes_and_keep_the_addon_name(self):
        self.commit("Add initial NSRT raid callouts", **{"output/Test/ExBoss_WoWVoxPacks_Test/Core.lua": "old"})
        self.git("tag", "v1.0.0-r1")
        self.commit("fix: correct warning volume", **{"output/Test/ExBoss_WoWVoxPacks_Test/Core.lua": "new"})
        self.commit("feat: add build diagnostics", **{"scripts/debug.py": "internal"})

        notes = self.notes()

        self.assertIn("- ExBoss: correct warning volume", notes)
        self.assertNotIn("build diagnostics", notes)

    def test_build_metadata_and_unrendered_vocabulary_do_not_become_release_notes(self):
        self.commit("Add initial NSRT raid callouts", **{"nsrt__vocabulary.txt": "Soak\n"})
        self.git("tag", "v1.0.0-r1")
        self.commit("feat(nsrt): add Ula'tek egg deadline callout", **{"nsrt__vocabulary.txt": "Soak\nEgg Deadline\n"})
        self.commit("fix(callouts): manifest bookkeeping", **{
            "output/Test/WoWVoxPacks_Callouts_Test/SoundFiles.json": "metadata",
            "output/Test/WoWVoxPacks_Callouts_Test.recipe.json": "recipe",
        })

        notes = self.notes()

        self.assertIn("No user-facing addon changes", notes)
        self.assertNotIn("Egg Deadline", notes)
        self.assertNotIn("manifest bookkeeping", notes)

    def test_optional_nsrt_vocabulary_is_included_when_its_pack_changes(self):
        self.commit("Add initial NSRT raid callouts", **{"nsrt__extra-vocabulary.txt": "Soak\n"})
        self.git("tag", "v1.0.0-r1")
        self.commit("feat(nsrt): add optional callouts", **{
            "nsrt__extra-vocabulary.txt": "Soak\nSoak Star\n",
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": 'LSM:Register("sound", "Soak Star", path .. "soak_star.ogg")\n',
        })

        self.assertIn("### New callouts\n\n- Soak Star", self.notes())

    def test_pending_vocabulary_is_not_announced_with_an_unrelated_packaged_fix(self):
        self.commit("feat(nsrt): add Soak callout", **{
            "nsrt__extra-vocabulary.txt": "Soak\n",
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": 'LSM:Register("sound", "Soak", path .. "soak.ogg")\n',
        })
        self.git("tag", "v1.0.0-r1")
        self.commit("feat(nsrt): add Ula'tek egg deadline callout", **{"nsrt__extra-vocabulary.txt": "Soak\nEgg Deadline\n"})
        self.commit("fix(nsrt): correct Soak recording volume", **{"output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Sounds/soak.ogg": "corrected recording"})

        notes = self.notes()

        self.assertIn("correct Soak recording volume", notes)
        self.assertNotIn("egg deadline", notes)
        self.assertNotIn("Egg Deadline", notes)

    def test_removed_source_name_retained_as_a_distributed_alias_is_not_a_removed_callout(self):
        self.commit("feat(nsrt): add Soak Star callout", **{
            "nsrt__extra-vocabulary.txt": "Soak Star\n",
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": 'LSM:Register("sound", "Soak Star", path .. "soak_star.ogg")\n',
        })
        self.git("tag", "v1.0.0-r1")
        self.commit("chore(nsrt): retain Soak Star as a compatibility alias", **{"nsrt__extra-vocabulary.txt": ""})
        self.commit("fix(nsrt): correct Soak Star recording volume", **{"output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Sounds/soak_star.ogg": "corrected recording"})

        self.assertNotIn("Removed callouts", self.notes())

    def test_callouts_rendered_after_an_earlier_source_release_are_announced(self):
        self.commit("chore(nsrt): track Ula'tek egg deadline phrase", **{
            "nsrt__extra-vocabulary.txt": "Egg Deadline\n",
        })
        self.git("tag", "v1.0.0-r1")
        self.commit("feat(nsrt): add Ula'tek egg deadline recording", **{
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": 'LSM:Register("sound", "Egg Deadline", path .. "egg_deadline.ogg")\n',
        })

        self.assertIn("### New callouts\n\n- Egg Deadline", self.notes())

    def update_subject(self, *arguments):
        return subprocess.check_output(
            [sys.executable, str(REPOSITORY_ROOT / "scripts/update_commit.py"),
             "--repository", str(self.directory), *arguments], text=True,
        ).strip()

    def test_updater_names_new_callouts_in_the_rendered_working_tree(self):
        self.commit("chore(nsrt): track Ula'tek egg deadline phrase", **{
            "nsrt__extra-vocabulary.txt": "Egg Deadline\n",
        })
        core = self.directory / "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua"
        core.parent.mkdir(parents=True)
        core.write_text('LSM:Register("sound", "Egg Deadline", path .. "egg_deadline.ogg")\n')

        self.assertEqual("feat(nsrt): add Egg Deadline callout", self.update_subject())

    def test_updater_names_both_ability_and_game_compatibility_updates(self):
        self.commit("feat(bigwigs_voice): add Kith'ix callouts", **{
            "output/Test/BigWigs_Voice_WoWVoxPacks_Test/Sounds/12345.ogg": "old recording",
        })
        (self.directory / "output/Test/BigWigs_Voice_WoWVoxPacks_Test/Sounds/12345.ogg").write_text("new recording")

        self.assertEqual(
            "feat(bigwigs_voice): sync ability callouts for BigWigs Voice v12.1.5; support WoW 12.1.5",
            self.update_subject("--bigwigs-version", "v12.1.5", "--game-version", "12.1.5"),
        )

    def test_a_compatibility_only_update_does_not_claim_new_ability_callouts(self):
        self.commit("fix(bigwigs_voice): support WoW 12.1.0", **{
            "bigwigs__voice-version.txt": "v12.1.0.3\n",
            "output/Test/BigWigs_Voice_WoWVoxPacks_Test/pack.toc": "## Interface: 120100\n",
        })
        (self.directory / "output/Test/BigWigs_Voice_WoWVoxPacks_Test/pack.toc").write_text("## Interface: 120105\n")

        self.assertEqual(
            "fix(bigwigs_voice): support WoW 12.1.5",
            self.update_subject("--bigwigs-version", "v12.1.0.3", "--game-version", "12.1.5"),
        )

    def test_updater_does_not_label_a_source_only_update_as_a_shipped_feature(self):
        self.commit("chore(nsrt): track Ula'tek egg deadline phrase", **{
            "nsrt__extra-vocabulary.txt": "Egg Deadline\n",
        })
        (self.directory / "nsrt-extra-vocabulary.txt").write_text("Egg Deadline\nSoak Star\n")

        self.assertEqual("chore(packs): update voice pack build inputs", self.update_subject())

    def test_the_span_starts_at_the_previous_release_tag(self):
        self.commit("feat(nsrt): add Soak callout", **{
            "nsrt__vocabulary.txt": "Soak\n",
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": 'LSM:Register("sound", "Soak", path .. "soak.ogg")\n',
        })
        self.git("tag", "v1.0.0-r1")
        self.commit("feat(nsrt): add Bait callout", **{
            "nsrt__vocabulary.txt": "Soak\nBait\n",
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": 'LSM:Register("sound", "Soak", path .. "soak.ogg")\nLSM:Register("sound", "Bait", path .. "bait.ogg")\n',
        })
        self.commit("fix(nsrt): remove obsolete Soak callout", **{
            "nsrt__vocabulary.txt": "Bait\n",
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": 'LSM:Register("sound", "Bait", path .. "bait.ogg")\n',
        })

        previous = release_notes.previous_tag("HEAD", self.directory)

        self.assertEqual("v1.0.0-r1", previous)
        self.assertEqual(
            ["Northern Sky Raid Tools: remove obsolete Soak callout", "Northern Sky Raid Tools: add Bait callout"],
            release_notes.release_changes(previous, "HEAD", self.directory, {"nsrt"}),
        )
        self.assertEqual(
            (["Bait"], ["Soak"]),
            release_notes.shipped_callout_changes(previous, "HEAD", self.directory, {"nsrt"}),
        )

    def test_releasing_a_tag_that_already_exists_spans_back_to_the_release_before_it(self):
        """A rerun on `push: tags` has the tag; describing from the ref would span nothing."""
        self.commit("feat(nsrt): add Soak callout")
        self.git("tag", "v1.0.0-r1")
        self.commit("fix(nsrt): correct Ula'tek Left and Right audio", **{
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": "corrected Left and Right",
        })
        self.git("tag", "v1.0.0-r2")

        previous = release_notes.previous_tag("HEAD", self.directory, "v1.0.0-r2")

        self.assertEqual("v1.0.0-r1", previous)
        self.assertEqual(
            ["Northern Sky Raid Tools: correct Ula'tek Left and Right audio"],
            release_notes.release_changes(previous, "HEAD", self.directory, {"nsrt"})
        )

    def test_a_release_sitting_on_this_commit_is_the_previous_release(self):
        """Cutting r2 with r1 on HEAD reports nothing new, not r1's commits a second time."""
        self.commit("feat(nsrt): add Soak callout")
        self.commit("fix(nsrt): pronounce Dmg amp as Damage Amp")
        self.git("tag", "v1.0.0-r1")

        previous = release_notes.previous_tag("HEAD", self.directory, "v1.0.0-r2")

        self.assertEqual("v1.0.0-r1", previous)
        self.assertEqual([], release_notes.release_changes(previous, "HEAD", self.directory, {"nsrt"}))

    def test_the_first_release_reports_qualified_changes_and_no_callout_churn(self):
        self.commit("feat(nsrt): add Ula'tek wave direction callouts", **{
            "nsrt__vocabulary.txt": "Soak\n",
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": "Left and Right",
        })
        self.commit("fix(nsrt): correct Ula'tek Left and Right audio", **{
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": "corrected Left and Right",
        })

        previous = release_notes.previous_tag("HEAD", self.directory)

        self.assertIsNone(previous)
        self.assertEqual(
            ["Northern Sky Raid Tools: correct Ula'tek Left and Right audio", "Northern Sky Raid Tools: add Ula'tek wave direction callouts"],
            release_notes.release_changes(previous, "HEAD", self.directory, {"nsrt"}),
        )
        self.assertEqual(
            ([], []), release_notes.shipped_callout_changes(previous, "HEAD", self.directory, {"nsrt"})
        )

    def test_merge_commits_are_not_notes(self):
        self.commit("feat(nsrt): add Soak callout")
        self.git("tag", "v1.0.0-r1")
        self.git("checkout", "-q", "-b", "side")
        self.commit("feat(nsrt): add Ula'tek transition soak callouts", **{
            "output/Test/WoWVoxPacks_NorthernSkyRaidTools_Test/Core.lua": "Soak Star",
        })
        self.git("checkout", "-q", "main")
        self.git("merge", "-q", "--no-ff", "-m", "Merge pull request #28", "side")

        subjects = release_notes.release_changes("v1.0.0-r1", "HEAD", self.directory, {"nsrt"})

        self.assertEqual(["Northern Sky Raid Tools: add Ula'tek transition soak callouts"], subjects)


if __name__ == "__main__":
    unittest.main()
