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
SPECIAL_DISPLAY = re.compile(r"\bisSpecialDisplay\s*=\s*true\b")
NO_EDIT = re.compile(r"\bNoEdit\s*=\s*true\b")
VERSION_TABLE = re.compile(r"(?<![\w.])Version\s*=\s*\{")
TOP_LEVEL_TEXT = re.compile(r"(?<![\w.])text\s*=\s*\"([^\"]*)\"(\s*\.\.)?")
TEXT_REASSIGNMENT = re.compile(r"\bdata\.text\s*=\s*\"([^\"]+)\"")
LATE_TTS_ASSIGNMENT = re.compile(r"^\s*(?:local\s+)?([\w.]*\bTTS)\s*=\s*(.+)$")
LOCAL_BINDING = re.compile(r"^\s*local\s+(\w+)\s*=")
INTERNAL_ID = re.compile(r"\binternalID\s*=\s*\"([^\"]+)\"")
SETTING_COPY = re.compile(r"^\s*(\w+)\.TTS\s*,?\s*$")
BINDING = re.compile(r"^\s*(?:local\s+)?([A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)\s*=(.*)$")
ALERT_REFERENCE = re.compile(r"\bdiffData\.(\w+)")
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
    nil, and its `text` otherwise. A special display is driven by encounter-specific code
    instead of the alert scheduler, so it may omit TTS entirely. A table reused for a second
    alert with `data.text` reassigned speaks the new value under the same TTS setting. A `TTS`
    field concatenated with something the game supplies is a site to enumerate, not a string.
    """
    spoken: Counter[str] = Counter()
    for chunk in ALERT_TABLE.split(text)[1:]:
        table = chunk.split("AddEncounterAlert")[0]
        match = TTS_FIELD.search(table)
        if not match:
            if SPECIAL_DISPLAY.search(table):
                continue
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
            declared = top_level_text(table)
            if declared:
                spoken[declared] += 1

        for reassigned in TEXT_REASSIGNMENT.finditer(chunk):
            spoken[reassigned.group(1).strip()] += 1

    return spoken, composed_sites(text, CONCATENATED_TTS_FIELD, path)


def top_level_text(table: str) -> str | None:
    """The alert's own `text`, not a Version entry's or an isConditional description's.

    None when it is empty or concatenated with something the game supplies.
    """
    depth = 0
    in_string = False
    index = 0
    while index < len(table):
        character = table[index]
        if in_string:
            if character == "\\":
                index += 1
            elif character == '"':
                in_string = False
        elif character == '"':
            in_string = True
        elif character == "{":
            depth += 1
        elif character == "}":
            if depth == 0:
                return None
            depth -= 1
        elif depth == 0 and (match := TOP_LEVEL_TEXT.match(table, index)):
            if match.group(2) or not match.group(1).strip():
                return None
            return match.group(1).strip()
        index += 1
    return None


def balanced_table(text: str, start: int) -> str:
    """The table literal opening at text[start], braces inside strings ignored."""
    depth = 0
    in_string = False
    index = start
    while index < len(text):
        character = text[index]
        if in_string:
            if character == "\\":
                index += 1
            elif character == '"':
                in_string = False
        elif character == '"':
            in_string = True
        elif character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
            if depth == 0:
                return text[start:index + 1]
        index += 1
    raise UpstreamShapeError("an alert's Version table never closes")


def optional_texts(text: str) -> Counter[str]:
    """The texts of alerts a player can make speak by ticking TTS with no TTS text.

    NSRT's alert editor saves TTS = true in that case, and a true TTS speaks the alert's text,
    whatever the source default was. Special displays and NoEdit alerts have no such editor.
    A Version step's text is what NSRT writes into an older saved alert on upgrade, so a player
    may hold that text instead.
    """
    optional: Counter[str] = Counter()
    for chunk in ALERT_TABLE.split(text)[1:]:
        table = chunk.split("AddEncounterAlert")[0]
        if SPECIAL_DISPLAY.search(table) or NO_EDIT.search(table):
            continue
        declared = top_level_text(table)
        if declared:
            optional[declared] += 1
        version = VERSION_TABLE.search(table)
        if version:
            for upgraded in TEXT_FIELD.finditer(balanced_table(table, version.end() - 1)):
                if upgraded.group(1).strip():
                    optional[upgraded.group(1).strip()] += 1
        for reassigned in TEXT_REASSIGNMENT.finditer(chunk):
            optional[reassigned.group(1).strip()] += 1
    return optional


def collect_optional(source: Path) -> Counter[str]:
    optional: Counter[str] = Counter()
    for path in iter_alert_files(source):
        optional.update(optional_texts(
            strip_lua_comments(path.read_text(encoding="utf-8", errors="replace"))))
    return optional


def declared_tts_by_alert(text: str) -> dict[str, str]:
    """Each alert's declared TTS setting, keyed by internalID: its literal or its keyword."""
    declared: dict[str, str] = {}
    for chunk in ALERT_TABLE.split(text)[1:]:
        table = chunk.split("AddEncounterAlert")[0]
        identity = INTERNAL_ID.search(table)
        setting = TTS_FIELD.search(table)
        if identity and setting:
            declared[identity.group(1)] = (
                setting.group(1) if setting.group(1) is not None else setting.group(2))

    return declared


def copied_setting(text: str, local: str, declared: dict[str, str]) -> str | None:
    """The declared TTS of the alerts a name is bound to from the difficulty's alert table.

    NSRT keys that table by internalID, so `local x = diffData and diffData.Waves` names the
    alert declared with internalID "Waves". Files reuse names like `alert` across functions and
    scopes, which no line scan can follow, so every binding of the name in the file has to be
    readable: one this script cannot read refuses the copy, and one to an alert declared true
    reports true whatever the others say.
    """
    if hidden_binding(text, local):
        return None

    settings: list[str] = []
    for line in text.splitlines():
        match = BINDING.match(line)
        if not match:
            continue
        names = [name.strip() for name in match.group(1).split(",")]
        if local not in names:
            continue
        values = match.group(2).split(",")
        if len(values) != len(names):
            return None
        referenced = [declared.get(key) for key in ALERT_REFERENCE.findall(values[names.index(local)])]
        if not referenced or None in referenced:
            return None
        settings.extend(setting for setting in referenced if setting is not None)

    if not settings:
        return None

    return "true" if "true" in settings else settings[0]


def function_parameter(text: str, name: str) -> bool:
    """Whether the name appears as a function parameter anywhere in the file."""
    return bool(re.search(rf"\bfunction\b[^(\n]*\([^)]*\b{re.escape(name)}\b", text))


def hidden_binding(text: str, name: str) -> bool:
    """Whether the name is also bound somewhere the line scan cannot read.

    A parameter, a loop variable, or an assignment after `then`, `do` or `;` would let a
    readable binding elsewhere in the file answer for a value it never held.
    """
    word = re.escape(name)
    if function_parameter(text, name):
        return True

    patterns = [
        re.compile(rf"\bfor\b.*\b{word}\b.*\bin\b"),
        re.compile(rf"\bfor\s+{word}\s*="),
        re.compile(rf"(?:\bthen\b|\belse\b|\bdo\b|\brepeat\b|;|\))[^=]*\b{word}\b[^=]*=(?!=)"),
    ]
    return any(
        pattern.search(line)
        for line in text.splitlines()
        if name in line
        for pattern in patterns)


def spoken_in_late_assignments(text: str, path: str) -> tuple[Counter[str], list[Composed]]:
    """Read every `TTS = <expression>` written outside an alert's table literal.

    Encounters swap an alert's speech at runtime through locals of their own naming, not only
    through `data`. A reminder built at runtime may copy another alert's TTS setting; it then
    speaks that alert's string (already read where it was declared), nothing when that is
    false or nil, and its own text when that is true.
    """
    spoken: Counter[str] = Counter()
    composed: list[Composed] = []
    readable: set[str] = set()
    declared = declared_tts_by_alert(text)
    for line_number, line in enumerate(text.splitlines(), start=1):
        match = LATE_TTS_ASSIGNMENT.match(line)
        if not match:
            continue

        target, expression = match.group(1), match.group(2)
        fragments = literals_in(expression)

        if not fragments:
            # A pass-through of a local this file already spelled out is not a second value.
            if any(re.search(rf"(?<![\w.]){re.escape(name)}\b", expression) for name in readable):
                continue
            if expression.strip().rstrip(",").split()[0] in ("false", "nil"):
                continue
            copy = SETTING_COPY.match(expression)
            setting = copied_setting(text, copy.group(1), declared) if copy else None
            if setting == "true":
                raise UpstreamShapeError(
                    f"{path}:{line_number} copies the TTS setting of an alert declared true, "
                    "so it speaks its own text; teach it that shape before trusting the result")
            if setting is not None:
                continue
            if copy and function_parameter(text, copy.group(1)):
                name = copy.group(1)
                has_diffdata_binding = False
                for file_line in text.splitlines():
                    m = BINDING.match(file_line)
                    if not m:
                        continue
                    names = [n.strip() for n in m.group(1).split(",")]
                    if name not in names:
                        continue
                    values = m.group(2).split(",")
                    if len(values) != len(names):
                        continue
                    if ALERT_REFERENCE.search(values[names.index(name)]):
                        has_diffdata_binding = True
                        break
                if not has_diffdata_binding:
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
        optional = Counter({
            string: count for string, count in collect_optional(args.source).items()
            if string not in spoken})
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
            "# speak into nsrt-extra-vocabulary.txt (note phrases into nsrt-note-vocabulary.txt),\n"
            "# then regenerate this file by rerunning\n"
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
                  "new or changed site can speak into nsrt-extra-vocabulary.txt (note phrases "
                  "into nsrt-note-vocabulary.txt), then "
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

    uncovered_optional = sorted(
        ((count, string) for string, count in optional.items()
         if string.casefold() not in covered),
        key=lambda pair: (-pair[0], pair[1].casefold()))

    print(f"{len(spoken)} strings can be spoken, {len(spoken) - len(uncovered)} recorded")
    for count, string in uncovered:
        print(f"  {count:>3} alert(s)  {string}")

    print(f"{len(optional)} more alert texts speak once a player ticks TTS, "
          f"{len(optional) - len(uncovered_optional)} recorded")
    for count, string in uncovered_optional:
        print(f"  {count:>3} alert(s)  {string}")

    if composed:
        print(f"{len(composed)} built at runtime, so no recording matches them whole. "
              "Enumerate what the game can append and add the results by hand:")
        for site in composed:
            print(f"  {site}")

    if args.output:
        args.output.write_text(
            "".join(f"{string}\n" for _, string in uncovered + uncovered_optional),
            encoding="utf-8")

    return 0 if args.exit_zero or not (uncovered or uncovered_optional) else 1


if __name__ == "__main__":
    sys.exit(main())
