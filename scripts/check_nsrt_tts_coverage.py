#!/usr/bin/env python3
"""Report Northern Sky Raid Tools alert strings that no tracked callout records."""

from __future__ import annotations

import argparse
import re
import sys
from collections import Counter
from collections.abc import Iterable
from pathlib import Path

# NSAPI:TTS resolves a string through LibSharedMedia, then Media/Sounds/<string>.ogg, and
# speaks it with C_VoiceChat.SpeakText when neither yields a sound handle. A string absent
# from both is the one alert in a pull that comes out in the Blizzard voice.
ALERT_TABLE = re.compile(r"\blocal\s+data\s*=\s*\{")
TEXT_FIELD = re.compile(r"\btext\s*=\s*\"([^\"]*)\"")
TTS_FIELD = re.compile(r"\bTTS\s*=\s*(?:\"([^\"]*)\"|(true|false|nil))")
TEXT_REASSIGNMENT = re.compile(r"\bdata\.text\s*=\s*\"([^\"]+)\"")
LITERAL_CALL = re.compile(r"NSAPI:TTS\(\s*\"([^\"]+)\"\s*[,)]")
CONCATENATED_CALL = re.compile(r"NSAPI:TTS\(\s*\"([^\"]+)\"\s*\.\.")

# The parser models a TTS field written into the table literal. An alert that assigns one
# afterwards would be read with the wrong one, so refuse rather than report a wrong answer.
UNMODELLED = re.compile(r"\bdata\.TTS\s*=")


class UpstreamShapeError(RuntimeError):
    """The source no longer looks like what the parser was written against."""


def strip_lua_comments(text: str) -> str:
    """Drop line comments, so the NSAPI:TTS("Bait Frontal") in a docstring is not a callout."""
    stripped: list[str] = []
    for line in text.splitlines():
        in_string = False
        cut = len(line)
        index = 0
        while index < len(line):
            character = line[index]
            if character == "\\" and in_string:
                index += 2
                continue
            if character == '"':
                in_string = not in_string
            elif not in_string and line.startswith("--", index):
                cut = index
                break
            index += 1
        stripped.append(line[:cut])

    return "\n".join(stripped)


def iter_alert_files(source: Path) -> list[Path]:
    alerts = source / "NorthernSkyRaidTools" / "EncounterAlerts"
    if not alerts.is_dir():
        matches = sorted(source.glob("**/EncounterAlerts"))
        if not matches:
            raise UpstreamShapeError(f"no EncounterAlerts directory under {source}")
        alerts = matches[0]

    return sorted(
        path
        for path in alerts.glob("**/*.lua")
        if "Locales" not in path.parts and "Example" not in path.name
    )


def spoken_in_alerts(text: str) -> Counter[str]:
    """Count the strings each alert table in one file can speak.

    An alert speaks its `TTS` field when that is a string, nothing at all when it is false,
    and its `text` otherwise. A table reused for a second alert with `data.text` reassigned
    speaks the new value under the same TTS setting.
    """
    spoken: Counter[str] = Counter()
    for chunk in ALERT_TABLE.split(text)[1:]:
        table = chunk.split("AddEncounterAlert")[0]
        match = TTS_FIELD.search(table)
        literal, keyword = (match.group(1), match.group(2)) if match else (None, None)
        if keyword == "false":
            continue

        if literal is not None:
            spoken[literal.strip()] += 1
        else:
            declared = TEXT_FIELD.search(table)
            if declared and declared.group(1).strip():
                spoken[declared.group(1).strip()] += 1

        for reassigned in TEXT_REASSIGNMENT.finditer(chunk):
            spoken[reassigned.group(1).strip()] += 1

    return spoken


def spoken_in_direct_calls(source: Path) -> tuple[Counter[str], set[str]]:
    """Collect literal NSAPI:TTS arguments, and the prefixes a runtime value is appended to.

    A call passing a bare variable is NSRT's own plumbing handing along a string this script
    has already read off an alert. A call concatenating onto a literal is different: the
    string it speaks never exists in full in the source, so no recording can match it.
    """
    spoken: Counter[str] = Counter()
    concatenated: set[str] = set()
    for path in sorted(source.glob("**/*.lua")):
        if "Libs" in path.parts:
            continue
        text = strip_lua_comments(path.read_text(encoding="utf-8", errors="replace"))
        for call in LITERAL_CALL.finditer(text):
            spoken[call.group(1).strip()] += 1
        for call in CONCATENATED_CALL.finditer(text):
            concatenated.add(call.group(1))

    return spoken, concatenated


def collect_spoken(source: Path) -> tuple[Counter[str], set[str]]:
    spoken: Counter[str] = Counter()
    for path in iter_alert_files(source):
        text = strip_lua_comments(path.read_text(encoding="utf-8", errors="replace"))
        if UNMODELLED.search(text):
            raise UpstreamShapeError(
                f"{path.name} assigns data.TTS after the table literal, which this script "
                "does not model; teach it that shape before trusting the result")
        spoken.update(spoken_in_alerts(text))

    if not spoken:
        raise UpstreamShapeError(
            "no alert speaks anything, which is a parse failure rather than full coverage")

    direct, concatenated = spoken_in_direct_calls(source)
    spoken.update(direct)
    return spoken, concatenated


def load_vocabulary(paths: Iterable[Path]) -> set[str]:
    covered: set[str] = set()
    for path in paths:
        for raw_line in path.read_text(encoding="utf-8").splitlines():
            line = raw_line.strip()
            if line and not line.startswith("#"):
                covered.add(line.casefold())

    if not covered:
        raise UpstreamShapeError("the tracked vocabulary is empty")

    return covered


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description=__doc__,
        epilog="get a checkout with: mkdir nsrt-src && curl -sfL "
               "https://github.com/Reloe/NorthernSkyRaidTools/archive/refs/heads/main.tar.gz "
               "| tar -xz -C nsrt-src --strip-components=1")
    parser.add_argument("--source", type=Path, required=True,
                        help="a Northern Sky Raid Tools checkout")
    parser.add_argument("--vocabulary", type=Path, action="append", required=True,
                        help="a tracked callout vocabulary; repeat to merge several")
    parser.add_argument("--output", type=Path,
                        help="write the uncovered strings here, one per line")
    parser.add_argument("--exit-zero", action="store_true",
                        help="report uncovered strings without failing")
    args = parser.parse_args(argv)

    try:
        spoken, concatenated = collect_spoken(args.source)
        covered = load_vocabulary(args.vocabulary)
    except UpstreamShapeError as error:
        print(f"error: {error}", file=sys.stderr)
        return 2

    uncovered = sorted(
        ((count, string) for string, count in spoken.items()
         if string.casefold() not in covered),
        key=lambda pair: (-pair[0], pair[1].casefold()))

    print(f"{len(spoken)} strings can be spoken, {len(spoken) - len(uncovered)} recorded")
    for prefix in sorted(concatenated):
        print(f"  {prefix!r} takes a runtime value, so no recording can match it")

    for count, string in uncovered:
        print(f"  {count:>3} alert(s)  {string}")

    if args.output:
        args.output.write_text(
            "".join(f"{string}\n" for _, string in uncovered), encoding="utf-8")

    return 0 if args.exit_zero or not uncovered else 1


if __name__ == "__main__":
    sys.exit(main())
