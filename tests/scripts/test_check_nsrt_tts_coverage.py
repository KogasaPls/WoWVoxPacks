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
        _, concatenated = self.collect()
        self.assertEqual({"Rebuff "}, concatenated)

    def test_an_unparsed_source_is_an_error_rather_than_full_coverage(self):
        with self.assertRaises(checker.UpstreamShapeError):
            self.collect(alerts="-- nothing here\n")

    def test_a_tts_assigned_after_the_table_refuses_to_guess(self):
        with self.assertRaises(checker.UpstreamShapeError):
            self.collect(alerts=ALERTS + '\ndata.TTS = false\n')


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
        self.assertEqual(0, self.run_main("Taunt\nBreath\nStack\nSoulstone\n"))

    def test_exit_zero_reports_without_failing(self):
        self.assertEqual(0, self.run_main("Stack\n", "--exit-zero"))

    def test_an_empty_vocabulary_is_an_error(self):
        self.assertEqual(2, self.run_main("# only a comment\n"))


if __name__ == "__main__":
    unittest.main()
