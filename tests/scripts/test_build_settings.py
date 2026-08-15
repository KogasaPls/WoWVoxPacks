import json
import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SETTINGS = REPOSITORY_ROOT / "targets/Settings.props"


def _property(name: str) -> str:
    match = re.search(rf"<{name}>(.*?)</{name}>", SETTINGS.read_text(encoding="utf-8"))
    assert match is not None, f"{name} is not set in targets/Settings.props"
    return match.group(1)


class BuildSettingsTests(unittest.TestCase):
    def test_the_sdk_may_roll_forward_but_only_within_dotnet_10(self):
        sdk = json.loads(
            (REPOSITORY_ROOT / "global.json").read_text(encoding="utf-8")
        )["sdk"]

        self.assertTrue(sdk["version"].startswith("10."))
        self.assertNotIn(sdk["rollForward"], ("major", "latestMajor"))

    def test_the_diagnostic_surface_is_pinned_to_the_framework(self):
        """CI builds with -warnaserror against an SDK that updates itself.

        Left at the default of "latest", AnalysisLevel enables whatever rules the newest SDK
        ships, so a release with a new rule fails a pull request that changed nothing.
        """
        self.assertEqual("net10.0", _property("TargetFramework"))
        self.assertEqual("10.0", _property("AnalysisLevel"))
        self.assertEqual("10", _property("WarningLevel"))

    def test_a_new_advisory_cannot_fail_an_unrelated_build(self):
        """NuGet audits transitive packages against advisories published after the fact.

        Those stay warnings: a CVE disclosed overnight would otherwise fail every build until
        someone ships a fix, including the build that ships it.
        """
        not_as_errors = _property("WarningsNotAsErrors")

        for advisory in ("NU1901", "NU1902", "NU1903", "NU1904"):
            self.assertIn(advisory, not_as_errors)

    def test_ci_fails_on_a_new_warning(self):
        ci = (REPOSITORY_ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")

        self.assertIn("dotnet build -v q --nologo -warnaserror", ci)


if __name__ == "__main__":
    unittest.main()
