#!/usr/bin/env python3
"""Render a CurseForge project page from docs/curseforge, substituted for one voice."""

import argparse
import sys
from pathlib import Path

# Mirrors the voices publish-to-curseforge.yml holds project IDs for.
PUBLISHED_VOICES = ("Wavenet_E", "Neural2_C", "Studio_Q")

# The CurseForge slug is the voice lowercased with hyphens, plus the addon's own fragment.
# NorthernSkyRaidTools keeps its words joined, so the fragments are listed rather than derived.
ADDON_SLUGS = {
    "BigWigs_Voice": "bigwigs-voice",
    "BigWigs_Countdown": "bigwigs-countdown",
    "Callouts": "callouts",
    "ExBoss": "exboss",
    "NorthernSkyRaidTools": "northernskyraidtools",
}

# TtsSettings.LanguageCode is en-US for every voice and appsettings.json never overrides it.
VOICE_DESCRIPTIONS = {
    "Wavenet_E": "en_US female",
    "Neural2_C": "en_US female",
    "Studio_Q": "en_US male",
}

DESCRIPTIONS = Path(__file__).resolve().parent.parent / "docs" / "curseforge"


def split_front_matter(source: str) -> tuple[str, str]:
    if not source.startswith("---\n"):
        raise ValueError("the page has no front matter")
    front, _, body = source[4:].partition("\n---\n")
    summary = next(
        (line[len("summary:"):].strip() for line in front.splitlines()
         if line.startswith("summary:")),
        None)
    if summary is None:
        raise ValueError("the front matter declares no summary")
    return summary, body.lstrip("\n")


def render(addon: str, voice: str) -> tuple[str, str]:
    page = DESCRIPTIONS / f"{addon}.md"
    if not page.is_file():
        raise ValueError(f"no page for {addon!r}; have {', '.join(addons())}")
    summary, body = split_front_matter(page.read_text(encoding="utf-8"))
    return substitute(summary, voice), substitute(body, voice)


def project_url(addon: str, voice: str) -> str:
    slug = f"wowvoxpacks-{voice.lower().replace('_', '-')}-{ADDON_SLUGS[addon]}"
    return f"https://www.curseforge.com/wow/addons/{slug}"


def substitute(text: str, voice: str) -> str:
    for addon in ADDON_SLUGS:
        text = text.replace(f"{{Url:{addon}}}", project_url(addon, voice))
    return (text
            .replace("{VoiceDescription}", VOICE_DESCRIPTIONS[voice])
            .replace("{Voice}", voice))


def addons() -> list[str]:
    return sorted(page.stem for page in DESCRIPTIONS.glob("*.md"))


def write_all(destination: Path) -> None:
    for voice in PUBLISHED_VOICES:
        for addon in addons():
            summary, body = render(addon, voice)
            directory = destination / voice
            directory.mkdir(parents=True, exist_ok=True)
            (directory / f"{addon}.summary.txt").write_text(summary + "\n", encoding="utf-8")
            (directory / f"{addon}.md").write_text(body, encoding="utf-8")
    print(f"Wrote {len(addons()) * len(PUBLISHED_VOICES)} pages to {destination}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--addon", choices=addons())
    parser.add_argument("--voice", choices=PUBLISHED_VOICES)
    parser.add_argument("--summary", action="store_true",
                        help="print the summary field instead of the description")
    parser.add_argument("--all", type=Path, metavar="DIR",
                        help="write every addon and voice under DIR")
    return parser.parse_args()


def main() -> int:
    arguments = parse_args()
    if arguments.all:
        write_all(arguments.all)
        return 0
    if not arguments.addon or not arguments.voice:
        print("--addon and --voice are required unless --all is given", file=sys.stderr)
        return 2
    summary, body = render(arguments.addon, arguments.voice)
    print(summary if arguments.summary else body, end="" if not arguments.summary else "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
