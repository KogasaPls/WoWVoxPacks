import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
RETIREMENT_PATH = REPOSITORY_ROOT / "scripts" / "retire_removed_callouts.py"

spec = importlib.util.spec_from_file_location("retire_removed_callouts", RETIREMENT_PATH)
retirement = importlib.util.module_from_spec(spec)
spec.loader.exec_module(retirement)


class RetireRemovedCalloutsTests(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.directory = Path(self.temporary_directory.name)
        self.retired_path = self.directory / "RetiredCallouts.json"

    def tearDown(self):
        self.temporary_directory.cleanup()

    def write_vocabulary(self, name, entries):
        path = self.directory / name
        path.write_text(
            "# generated vocabulary\n\n" + "\n".join(entries) + "\n",
            encoding="utf-8",
        )
        return path

    def test_retires_removed_callouts_vocabulary_and_is_idempotent(self):
        old_lorrgs = self.write_vocabulary("lorrgs.old", ["Healthstone", "Trinket"])
        new_lorrgs = self.write_vocabulary("lorrgs.new", ["Healthstone"])
        self.retired_path.write_text('["Already Retired"]\n', encoding="utf-8")

        retired = retirement.retire_removed(
            [(old_lorrgs, new_lorrgs)],
            self.retired_path,
        )

        self.assertEqual(["Already Retired", "Trinket"], retired)
        first_write = self.retired_path.read_text(encoding="utf-8")
        self.assertEqual(retired, json.loads(first_write))
        self.assertTrue(first_write.endswith("\n"))

        second = retirement.retire_removed(
            [(old_lorrgs, new_lorrgs)],
            self.retired_path,
        )

        self.assertEqual(retired, second)
        self.assertEqual(first_write, self.retired_path.read_text(encoding="utf-8"))

    def test_case_only_rename_retires_the_shipped_key(self):
        old = self.write_vocabulary("old", ["Soak"])
        new = self.write_vocabulary("new", ["soak"])

        retired = retirement.retire_removed([(old, new)], self.retired_path)

        self.assertEqual(["Soak"], retired)

    def test_missing_retired_file_starts_empty(self):
        old = self.write_vocabulary("old", ["OldCallout"])
        new = self.write_vocabulary("new", [])

        retired = retirement.retire_removed([(old, new)], self.retired_path)

        self.assertEqual(["OldCallout"], retired)
        self.assertEqual(["OldCallout"], json.loads(self.retired_path.read_text(encoding="utf-8")))

    def test_rejects_too_many_removals_from_one_source_without_writing(self):
        old = self.write_vocabulary("old", [f"Callout {number}" for number in range(6)])
        new = self.write_vocabulary("new", [])
        self.retired_path.write_text('["Existing"]\n', encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "removed 6 callouts"):
            retirement.retire_removed(
                [(old, new)],
                self.retired_path,
                max_removed_per_source=5,
            )

        self.assertEqual('["Existing"]\n', self.retired_path.read_text(encoding="utf-8"))

    def test_update_workflow_retires_only_callouts_snapshot_before_replacing_it(self):
        workflow = (REPOSITORY_ROOT / ".github" / "workflows" / "update.yml").read_text(
            encoding="utf-8"
        )

        helper = "python scripts/retire_removed_callouts.py"
        retirement_step = workflow[
            workflow.index("- name: Retire removed callouts") :
            workflow.index("- name: Apply the new vocabulary")
        ]
        self.assertIn(helper, workflow)
        self.assertIn("--vocabulary lorrgs-vocabulary.txt lorrgs-vocabulary.new", retirement_step)
        self.assertNotIn("nsrt-vocabulary.txt", retirement_step)
        self.assertLess(workflow.index(helper), workflow.index("mv lorrgs-vocabulary.new"))


if __name__ == "__main__":
    unittest.main()
