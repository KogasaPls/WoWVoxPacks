import importlib.util
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]

_spec = importlib.util.spec_from_file_location(
    "check_nsrt_tts_coverage", REPOSITORY_ROOT / "scripts/check_nsrt_tts_coverage.py")
checker = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(checker)


ALERTS = '''
local _, NSI = ...
NSI.InitializeAlerts[1] = function(self)
    local data = {internalID = "Taunts", text = "Taunt", TTS = true, dur = 3,
        isConditional = {text = "This Alert only shows if you have threat on boss1."},
    }
    self:AddEncounterAlert(data)
    data.internalID, data.text = "Breath", "Breath"
    self:AddEncounterAlert(data)

    local data = {internalID = "Displayed", text = "Drop-Pool", TTS = false, dur = 6}
    self:AddEncounterAlert(data)

    local data = {internalID = "Overridden", text = "Stack Up", TTS = "Stack", dur = 6}
    self:AddEncounterAlert(data)

    local data = {internalID = "Unset", text = "Displayed Only", TTS = nil, dur = 4}
    self:AddEncounterAlert(data)

    local markers = {"Star", "Cross"}
    for i = 1, 2 do
        local data = {internalID = "Mark", text = "Soak {rt1}", TTS = "Soak "..markers[i]}
        self:AddEncounterAlert(data)
    end

    Soak.TTS = subgroup <= 2 and NSI:EncounterAlertLoc("Soak") or NSI:EncounterAlertLoc("Don't soak")

    local TTS = NSI:EncounterAlertLoc("Go to ")..NSI:EncounterAlertLoc(pos)
    Alert.TTS, Alert.text = TTS, text
end
'''

DIRECT_CALLS = '''
function NSAPI:TTS(sound, voice) -- NSAPI:TTS("Bait Frontal")
end
local function ready()
    NSAPI:TTS("Soulstone")
    NSAPI:TTS("Rebuff "..name)
    NSAPI:TTS(info.text)
end
'''


def write_source(directory, alerts=ALERTS, direct=DIRECT_CALLS):
    root = Path(directory)
    alerts_dir = root / "NorthernSkyRaidTools" / "EncounterAlerts" / "Season"
    alerts_dir.mkdir(parents=True)
    (alerts_dir / "Boss.lua").write_text(alerts, encoding="utf-8")
    (root / "NorthernSkyRaidTools" / "Functions.lua").write_text(direct, encoding="utf-8")
    return root


class CollectSpokenTests(unittest.TestCase):
    def collect(self, **kwargs):
        with tempfile.TemporaryDirectory() as directory:
            return checker.collect_spoken(write_source(directory, **kwargs))

    def test_an_alert_speaks_its_text(self):
        spoken, _ = self.collect()
        self.assertEqual(1, spoken["Taunt"])

    def test_a_reused_table_speaks_the_reassigned_text(self):
        spoken, _ = self.collect()
        self.assertEqual(1, spoken["Breath"])

    def test_tts_false_speaks_nothing(self):
        """The alert still displays its text, so a plain grep overstates the gap."""
        spoken, _ = self.collect()
        self.assertNotIn("Drop-Pool", spoken)

    def test_a_string_tts_wins_over_the_text(self):
        spoken, _ = self.collect()
        self.assertIn("Stack", spoken)
        self.assertNotIn("Stack Up", spoken)

    def test_a_nested_conditional_description_is_not_a_callout(self):
        spoken, _ = self.collect()
        self.assertNotIn("This Alert only shows if you have threat on boss1.", spoken)

    def test_a_literal_call_outside_the_alerts_counts(self):
        spoken, _ = self.collect()
        self.assertEqual(1, spoken["Soulstone"])

    def test_a_call_in_a_comment_is_not_a_callout(self):
        spoken, _ = self.collect()
        self.assertNotIn("Bait Frontal", spoken)

    def test_a_concatenated_call_is_reported_as_unmatchable(self):
        _, composed = self.collect()
        self.assertIn("Rebuff ", [f for site in composed for f in site.fragments])

    def test_a_late_tts_assignment_on_any_receiver_is_read(self):
        """Five encounters swap speech through a local that is not named data."""
        spoken, _ = self.collect()
        self.assertEqual(1, spoken["Don't soak"])
        self.assertEqual(1, spoken["Soak"])

    def test_a_concatenated_tts_field_is_reported_not_counted_as_its_prefix(self):
        """Recording the prefix would report a string as covered that never plays."""
        spoken, composed = self.collect()
        self.assertNotIn("Soak Star", spoken)
        self.assertTrue(any("Soak " in site.fragments for site in composed))

    def test_a_concatenated_local_is_reported_rather_than_read_as_its_prefix(self):
        spoken, composed = self.collect()
        self.assertNotIn("Go to ", spoken)
        self.assertTrue(any("Go to " in site.fragments for site in composed))

    def test_tts_nil_speaks_nothing(self):
        """PlayReminderSound gates on `if info.TTS`, so nil is silent like false."""
        spoken, _ = self.collect()
        self.assertNotIn("Displayed Only", spoken)

    def test_an_unreadable_tts_expression_refuses_to_guess(self):
        with self.assertRaises(checker.UpstreamShapeError):
            self.collect(alerts=ALERTS + "\n    Alert.TTS = SomeTable[index]\n")

    def test_an_unparsed_source_is_an_error_rather_than_full_coverage(self):
        with self.assertRaises(checker.UpstreamShapeError):
            self.collect(alerts="-- nothing here\n")



class MainTests(unittest.TestCase):
    def run_main(self, vocabulary, *extra):
        with tempfile.TemporaryDirectory() as directory:
            root = write_source(directory)
            vocabulary_path = root / "vocabulary.txt"
            vocabulary_path.write_text(vocabulary, encoding="utf-8")
            return checker.main(
                ["--source", str(root), "--vocabulary", str(vocabulary_path), *extra])

    def test_uncovered_strings_fail(self):
        self.assertEqual(1, self.run_main("# comment\nStack\n"))

    def test_full_coverage_passes(self):
        self.assertEqual(0, self.run_main(
            "Taunt\nBreath\nStack\nSoulstone\nSoak\nDon't soak\n"))

    def test_exit_zero_reports_without_failing(self):
        self.assertEqual(0, self.run_main("Stack\n", "--exit-zero"))

    def test_an_empty_vocabulary_is_an_error(self):
        self.assertEqual(2, self.run_main("# only a comment\n"))


DIRECT_CALLS_WITH_BUFFS = DIRECT_CALLS + '''
local buffs = {
    [1] = 6673, -- Battle Shout
    [13] = {381741, 381757},
}
'''


class ComposedSnapshotTests(unittest.TestCase):
    def run_main(self, direct, *extra):
        with tempfile.TemporaryDirectory() as directory:
            root = write_source(directory, direct=direct)
            vocabulary_path = root / "vocabulary.txt"
            vocabulary_path.write_text(
                "Taunt\nBreath\nStack\nSoulstone\nSoak\nDon't soak\n", encoding="utf-8")
            snapshot_path = root / "composed.txt"
            return checker.main(
                ["--source", str(root), "--vocabulary", str(vocabulary_path),
                 *(argument.replace("SNAPSHOT", str(snapshot_path)) for argument in extra)])

    def test_a_written_snapshot_round_trips(self):
        self.assertEqual(0, self.run_main(
            DIRECT_CALLS_WITH_BUFFS,
            "--write-composed", "SNAPSHOT", "--composed", "SNAPSHOT"))

    def test_a_new_composed_site_fails_the_check(self):
        with tempfile.TemporaryDirectory() as directory:
            snapshot = Path(directory) / "composed.txt"
            self.assertEqual(0, self.run_main(
                DIRECT_CALLS_WITH_BUFFS, "--write-composed", str(snapshot)))
            self.assertEqual(1, self.run_main(
                DIRECT_CALLS_WITH_BUFFS + '\nNSAPI:TTS("Stack "..count)\n',
                "--composed", str(snapshot)))

    def test_a_changed_buff_table_fails_the_check(self):
        """The Rebuff site speaks buff names, so a new class buff is a new string."""
        with tempfile.TemporaryDirectory() as directory:
            snapshot = Path(directory) / "composed.txt"
            self.assertEqual(0, self.run_main(
                DIRECT_CALLS_WITH_BUFFS, "--write-composed", str(snapshot)))
            self.assertEqual(1, self.run_main(
                DIRECT_CALLS_WITH_BUFFS.replace("[1] = 6673,", "[1] = 6673,\n    [7] = 462854,"),
                "--composed", str(snapshot)))

    def test_every_buff_table_in_a_file_is_read(self):
        """Only reading the first table would pass the snapshot while a second one drifts."""
        with tempfile.TemporaryDirectory() as directory:
            snapshot = Path(directory) / "composed.txt"
            doubled = DIRECT_CALLS_WITH_BUFFS + "\nlocal buffs = {\n    [2] = 17,\n}\n"
            self.assertEqual(0, self.run_main(doubled, "--write-composed", str(snapshot)))
            self.assertIn("17", snapshot.read_text(encoding="utf-8"))

    def test_a_rebuff_site_with_no_buff_table_refuses_to_snapshot(self):
        with tempfile.TemporaryDirectory() as directory:
            self.assertEqual(2, self.run_main(
                DIRECT_CALLS, "--write-composed", str(Path(directory) / "composed.txt")))

    def test_the_exit_zero_report_still_fails_on_snapshot_drift(self):
        """--exit-zero forgives uncovered strings, not an unenumerated composed site."""
        with tempfile.TemporaryDirectory() as directory:
            snapshot = Path(directory) / "composed.txt"
            self.assertEqual(0, self.run_main(
                DIRECT_CALLS_WITH_BUFFS, "--write-composed", str(snapshot)))
            self.assertEqual(1, self.run_main(
                DIRECT_CALLS_WITH_BUFFS + '\nNSAPI:TTS("Stack "..count)\n',
                "--composed", str(snapshot), "--exit-zero"))


class TrackedVocabularyTests(unittest.TestCase):
    """Each string lives in exactly one vocabulary, so overlap means one went stale."""

    def read(self, name):
        return checker.load_vocabulary([REPOSITORY_ROOT / name])

    def test_no_string_lives_in_two_vocabularies(self):
        names = ["nsrt-vocabulary.txt", "nsrt-alert-vocabulary.txt",
                 "nsrt-extra-vocabulary.txt"]
        for first in names:
            for second in names:
                if first < second:
                    self.assertEqual(set(), self.read(first) & self.read(second),
                                     f"{first} overlaps {second}")

    def test_the_supplement_is_not_empty(self):
        self.assertTrue(self.read("nsrt-extra-vocabulary.txt"))


if __name__ == "__main__":
    unittest.main()
