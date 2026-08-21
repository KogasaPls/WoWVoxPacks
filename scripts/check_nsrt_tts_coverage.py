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
TTS_FIELD_CONTINUES = re.compile(r"^\s*\.\.")
TEXT_REASSIGNMENT = re.compile(r"\bdata\.text\s*=\s*\"([^\"]+)\"")
LATE_TTS_ASSIGNMENT = re.compile(r"^\s*(?:local\s+)?([\w.]*\bTTS)\s*=\s*(.+)$")
LOCAL_BINDING = re.compile(r"^\s*local\s+(\w+)\s*=")
CONCATENATED_TTS_FIELD = re.compile(r"\bTTS\s*=\s*\"([^\"]*)\"\s*\.\.")
LITERAL_CALL = re.compile(r"NSAPI:TTS\(\s*\"([^\"]+)\"\s*[,)]")
CONCATENATED_CALL = re.compile(r"NSAPI:TTS\(\s*\"([^\"]+)\"\s*\.\.")
STRING_LITERAL = re.compile(r"\"([^\"]*)\"")
CONCATENATION = re.compile(r"\.\.")

# EncounterAlertLoc returns its key verbatim on enUS, so the key is what is spoken.
LOCALISED = re.compile(r"EncounterAlertLoc\(\s*\"([^\"]*)\"\s*\)")

BUFF_TABLE = re.compile(r"\blocal\s+buffs\s*=\s*\{")
BUFF_TABLE_ENTRY = re.compile(r"\[\s*\d+\s*\]\s*=\s*(\{[^}]*\}|\d+)")


class UpstreamShapeError(RuntimeError):
    """The source no longer looks like what the parser was written against."""


class Composed:
    """A spoken string built at runtime from a literal and something only the game knows."""

    def __init__(self, location: str, fragments: list[str]) -> None:
        self.location = location
        self.fragments = fragments

    def __str__(self) -> str:
        return f"{self.location}  " + " .. ".join(
            f'"{fragment}"' for fragment in self.fragments) + " .. <runtime>"


def strip_lua_comments(text: str) -> str:
    """Drop line comments, so the NSAPI:TTS("Bait Frontal") in a comment is not a callout."""
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


def composed_sites(text: str, pattern: re.Pattern[str], location: str) -> list[Composed]:
    return [
        Composed(f"{location}:{number}", [match.group(1)])
        for number, line in enumerate(text.splitlines(), start=1)
        if (match := pattern.search(line))
    ]


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


def literals_in(expression: str) -> list[str]:
    """The strings an expression can evaluate to.

    EncounterAlertLoc returns its key verbatim on enUS, so where an expression localises, its
    keys are the whole answer and the bare literals around them are arguments to it.
    """
    localised = LOCALISED.findall(expression)
    if localised:
        return localised

    return STRING_LITERAL.findall(expression)


def spoken_in_alerts(text: str, path: str) -> tuple[Counter[str], list[Composed]]:
    """Count the strings each alert table in one file can speak.

    An alert speaks its `TTS` field when that is a string, nothing at all when it is false or
    nil, and its `text` otherwise. A table reused for a second alert with `data.text`
    reassigned speaks the new value under the same TTS setting. A `TTS` field concatenated
    with something the game supplies is a site to enumerate, not a string.
    """
    spoken: Counter[str] = Counter()
    for chunk in ALERT_TABLE.split(text)[1:]:
        table = chunk.split("AddEncounterAlert")[0]
        match = TTS_FIELD.search(table)
        if not match:
            raise UpstreamShapeError(
                "an alert declares no TTS field, so whether it speaks is the user's setting "
                "and this script cannot tell; teach it that shape before trusting the result")

        literal, keyword = match.group(1), match.group(2)
        if keyword in ("false", "nil"):
            continue

        if literal is not None:
            if TTS_FIELD_CONTINUES.match(table[match.end():]):
                continue
            spoken[literal.strip()] += 1
        else:
            declared = TEXT_FIELD.search(table)
            if declared and declared.group(1).strip():
                spoken[declared.group(1).strip()] += 1

        for reassigned in TEXT_REASSIGNMENT.finditer(chunk):
            spoken[reassigned.group(1).strip()] += 1

    return spoken, composed_sites(text, CONCATENATED_TTS_FIELD, path)


def spoken_in_late_assignments(text: str, path: str) -> tuple[Counter[str], list[Composed]]:
    """Read every `TTS = <expression>` written outside an alert's table literal.

    Encounters swap an alert's speech at runtime through locals of their own naming, not only
    through `data`.
    """
    spoken: Counter[str] = Counter()
    composed: list[Composed] = []
    readable: set[str] = set()
    for line_number, line in enumerate(text.splitlines(), start=1):
        match = LATE_TTS_ASSIGNMENT.match(line)
        if not match:
            continue

        target, expression = match.group(1), match.group(2)
        fragments = literals_in(expression)

        if not fragments:
            # A pass-through of a local this file already spelled out is not a second value.
            if any(re.search(rf"\b{re.escape(name)}\b", expression) for name in readable):
                continue
            if expression.strip().rstrip(",").split()[0] in ("false", "nil"):
                continue
            raise UpstreamShapeError(
                f"{path}:{line_number} sets {target} from an expression holding no string "
                "this script can read; teach it that shape before trusting the result")

        if CONCATENATION.search(expression):
            composed.append(Composed(f"{path}:{line_number}", fragments))
        else:
            for fragment in fragments:
                spoken[fragment.strip()] += 1

        binding = LOCAL_BINDING.match(line)
        if binding:
            readable.add(binding.group(1))

    return spoken, composed


def spoken_in_direct_calls(source: Path) -> tuple[Counter[str], list[Composed]]:
    """Collect literal NSAPI:TTS arguments, and the prefixes a runtime value is appended to.

    A call passing a bare variable is NSRT's own plumbing handing along a string this script
    has already read off an alert. A call concatenating onto a literal is different: the
    string it speaks never exists in full in the source.
    """
    spoken: Counter[str] = Counter()
    composed: list[Composed] = []
    for path in sorted(source.glob("**/*.lua")):
        if "Libs" in path.parts:
            continue
        text = strip_lua_comments(path.read_text(encoding="utf-8", errors="replace"))
        for call in LITERAL_CALL.finditer(text):
            spoken[call.group(1).strip()] += 1
        composed.extend(
            composed_sites(text, CONCATENATED_CALL, str(path.relative_to(source))))

    return spoken, composed


def collect_spoken(source: Path) -> tuple[Counter[str], list[Composed]]:
    spoken: Counter[str] = Counter()
    composed: list[Composed] = []
    for path in iter_alert_files(source):
        text = strip_lua_comments(path.read_text(encoding="utf-8", errors="replace"))
        declared, declared_composed = spoken_in_alerts(text, path.name)
        spoken.update(declared)
        composed.extend(declared_composed)

        late, late_composed = spoken_in_late_assignments(text, path.name)
        spoken.update(late)
        composed.extend(late_composed)

    if not spoken:
        raise UpstreamShapeError(
            "no alert speaks anything, which is a parse failure rather than full coverage")

    direct, direct_composed = spoken_in_direct_calls(source)
    spoken.update(direct)
    composed.extend(direct_composed)
    return spoken, composed


def buff_table_ids(source: Path) -> list[int]:
    ids: set[int] = set()
    for path in sorted(source.glob("**/*.lua")):
        if "Libs" in path.parts:
            continue
        text = strip_lua_comments(path.read_text(encoding="utf-8", errors="replace"))
        for match in BUFF_TABLE.finditer(text):
            depth = 1
            end = match.end()
            while end < len(text) and depth > 0:
                if text[end] == "{":
                    depth += 1
                elif text[end] == "}":
                    depth -= 1
                end += 1
            for entry in BUFF_TABLE_ENTRY.finditer(text[match.end():end]):
                ids.update(int(number) for number in re.findall(r"\d+", entry.group(1)))

    return sorted(ids)


def composed_signatures(source: Path, composed: list[Composed]) -> list[str]:
    """Stable identities for the composed sites: file and fragments, no line numbers.

    The Rebuff site speaks GetSpellInfo(buffs[class]).name, so the strings it can produce
    change exactly when that table does; its signature pins the table's spell IDs.
    """
    signatures = {
        site.location.rsplit(":", 1)[0] + "  "
        + " .. ".join(f'"{fragment}"' for fragment in site.fragments)
        for site in composed
    }
    if any("Rebuff " in site.fragments for site in composed):
        ids = buff_table_ids(source)
        if not ids:
            raise UpstreamShapeError(
                "a Rebuff site composes from a buffs table this script cannot find; teach it "
                "the new shape before trusting the snapshot")
        signatures.add("buffs  " + " ".join(str(identifier) for identifier in ids))

    return sorted(signatures)


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
    parser.add_argument("--composed", type=Path,
                        help="fail unless the composed-site snapshot in this file still holds")
    parser.add_argument("--write-composed", type=Path,
                        help="write the current composed-site snapshot here")
    parser.add_argument("--exit-zero", action="store_true",
                        help="report uncovered strings without failing")
    args = parser.parse_args(argv)

    try:
        spoken, composed = collect_spoken(args.source)
        covered = load_vocabulary(args.vocabulary)
        signatures = (
            composed_signatures(args.source, composed)
            if args.composed or args.write_composed else [])
    except UpstreamShapeError as error:
        print(f"error: {error}", file=sys.stderr)
        return 2

    if args.write_composed:
        args.write_composed.write_text(
            "# Runtime-composed TTS sites in Northern Sky Raid Tools, and the buff table the\n"
            "# Rebuff site reads. On a mismatch: enumerate what the new or changed site can\n"
            "# speak into nsrt-extra-vocabulary.txt, then regenerate this file by rerunning\n"
            "# scripts/check_nsrt_tts_coverage.py with --write-composed nsrt-composed-sites.txt.\n"
            + "".join(f"{signature}\n" for signature in signatures),
            encoding="utf-8")

    if args.composed:
        expected = {
            line.strip()
            for line in args.composed.read_text(encoding="utf-8").splitlines()
            if line.strip() and not line.startswith("#")
        }
        if expected != set(signatures):
            print("error: the composed-site snapshot no longer holds; enumerate what each "
                  "new or changed site can speak into nsrt-extra-vocabulary.txt, then "
                  "regenerate it with --write-composed", file=sys.stderr)
            for signature in sorted(set(signatures) - expected):
                print(f"  new:  {signature}", file=sys.stderr)
            for signature in sorted(expected - set(signatures)):
                print(f"  gone: {signature}", file=sys.stderr)
            return 1

    uncovered = sorted(
        ((count, string) for string, count in spoken.items()
         if string.casefold() not in covered),
        key=lambda pair: (-pair[0], pair[1].casefold()))

    print(f"{len(spoken)} strings can be spoken, {len(spoken) - len(uncovered)} recorded")
    for count, string in uncovered:
        print(f"  {count:>3} alert(s)  {string}")

    if composed:
        print(f"{len(composed)} built at runtime, so no recording matches them whole. "
              "Enumerate what the game can append and add the results by hand:")
        for site in composed:
            print(f"  {site}")

    if args.output:
        args.output.write_text(
            "".join(f"{string}\n" for _, string in uncovered), encoding="utf-8")

    return 0 if args.exit_zero or not uncovered else 1


if __name__ == "__main__":
    sys.exit(main())
