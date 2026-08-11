import os
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
TEXT_SUFFIXES = {".cs", ".json", ".lua", ".md", ".sh", ".toc", ".txt", ".yml", ".yaml"}
SKIP_PARTS = {".git", ".superpowers", "bin", "obj"}


class AddonIdentityTests(unittest.TestCase):
    def test_old_addon_identity_is_absent_from_source_contracts(self):
        forbidden = (
            "WoWVoxPacks_" + "Speech",
            "WoWVoxPacks" + "SpeechDB",
            "WOWVOXPACKS_" + "SPEECH_VOICE",
            "CF_" + "SPEECH_PROJECT_ID",
        )
        roots = ["README.md", "appsettings.json", "src", "scripts", "tests", ".github"]
        matches = []
        for relative in roots:
            root = REPOSITORY_ROOT / relative
            candidates = [root] if root.is_file() else root.rglob("*")
            for path in candidates:
                parts = path.relative_to(REPOSITORY_ROOT).parts
                if (path.is_file() and path.suffix in TEXT_SUFFIXES
                        and not SKIP_PARTS.intersection(parts)):
                    content = path.read_text(encoding="utf-8")
                    if any(name in content for name in forbidden):
                        matches.append(str(path.relative_to(REPOSITORY_ROOT)))
        self.assertEqual([], sorted(set(matches)))

    def test_package_script_creates_per_voice_northern_sky_raid_tools_archives(self):
        voices = ("Wavenet_E", "Neural2_C", "Standard_D", "Studio_Q", "Studio_O")
        with tempfile.TemporaryDirectory() as temporary_directory:
            working_directory = Path(temporary_directory)
            for voice in voices:
                addon = (
                    working_directory
                    / "output"
                    / voice
                    / f"WoWVoxPacks_NorthernSkyRaidTools_{voice}"
                )
                (addon / "Sounds").mkdir(parents=True)
                (addon / f"WoWVoxPacks_NorthernSkyRaidTools_{voice}.toc").write_text(
                    "## Interface: 110000\nCore.lua\n",
                    encoding="utf-8",
                )
                (addon / "Core.lua").write_text("-- addon content\n", encoding="utf-8")
                (addon / "SoundFiles.json").write_text("[]\n", encoding="utf-8")
                (addon / "Sounds" / "voice.ogg").write_bytes(b"OggS\x00fixture")

            completed = subprocess.run(
                [REPOSITORY_ROOT / "scripts/package.sh"],
                cwd=working_directory,
                env={**os.environ, "RELEASE_TAG": "test-tag"},
                capture_output=True,
                text=True,
            )
            self.assertEqual(0, completed.returncode, completed.stderr)

            archives = working_directory / "dist"
            expected_names = {
                f"WoWVoxPacks_{voice}_NorthernSkyRaidTools_test-tag.zip"
                for voice in voices
            }
            self.assertEqual(expected_names, {archive.name for archive in archives.iterdir()})
            for voice in voices:
                archive = archives / f"WoWVoxPacks_{voice}_NorthernSkyRaidTools_test-tag.zip"
                with zipfile.ZipFile(archive) as package:
                    names = set(package.namelist())
                    addon_prefix = f"WoWVoxPacks_NorthernSkyRaidTools_{voice}/"
                    self.assertIn(f"{addon_prefix}{addon_prefix[:-1]}.toc", names)
                    self.assertIn(f"{addon_prefix}Core.lua", names)
                    self.assertIn(f"{addon_prefix}SoundFiles.json", names)
                    ogg = package.read(f"{addon_prefix}Sounds/voice.ogg")
                    self.assertTrue(ogg)

    def test_new_addon_identity_is_wired_through_source_contracts(self):
        settings = (REPOSITORY_ROOT / "appsettings.json").read_text(encoding="utf-8")
        package = (REPOSITORY_ROOT / "scripts/package.sh").read_text(encoding="utf-8")
        publish = (REPOSITORY_ROOT / ".github/workflows/publish-to-curseforge.yml").read_text(
            encoding="utf-8"
        )
        self.assertIn('"NorthernSkyRaidTools"', settings)
        self.assertIn('"Title": "WoWVoxPacks_NorthernSkyRaidTools"', settings)
        self.assertNotIn("Shared/WoWVoxPacks_NorthernSkyRaidTools", package)
        self.assertNotIn("WoWVoxPacks_NorthernSkyRaidTools_${RELEASE_TAG}.zip", package)
        self.assertNotIn("CF_NORTHERN_SKY_" + "RAID_TOOLS_PROJECT_ID", publish)
        self.assertNotIn(
            "WoWVoxPacks_NorthernSkyRaidTools_${{ env.RELEASE_TAG }}.zip", publish
        )


if __name__ == "__main__":
    unittest.main()
