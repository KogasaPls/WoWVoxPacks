#!/usr/bin/env python3
"""Report CurseForge project pages whose live text differs from docs/curseforge."""

import argparse
import difflib
import importlib.util
import json
import re
import urllib.parse
import urllib.request
from html.parser import HTMLParser
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
WORKFLOW = REPOSITORY_ROOT / ".github" / "workflows" / "publish-to-curseforge.yml"
API = "https://api.curseforge.com/v1/mods"

spec = importlib.util.spec_from_file_location(
    "curseforge_description", Path(__file__).resolve().parent / "curseforge_description.py"
)
pages = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pages)

BLOCK_TAGS = {"p", "div", "br", "li", "tr", "h1", "h2", "h3", "h4", "h5", "h6",
              "blockquote", "pre", "hr", "ul", "ol", "table", "thead", "tbody"}
CELL_TAGS = {"td", "th"}


class DescriptionText(HTMLParser):
    """Flattens CurseForge's stored HTML to one line per block, cells joined by a pipe."""

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.lines: list[str] = []
        self._parts: list[str] = []
        self._href: str | None = None

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag == "a":
            self._href = dict(attrs).get("href")
        elif tag in CELL_TAGS and self._parts:
            self._parts.append("|")
        elif tag in BLOCK_TAGS:
            self._break()

    def handle_startendtag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        self.handle_starttag(tag, attrs)

    def handle_endtag(self, tag: str) -> None:
        if tag == "a":
            target = unwrap_link(self._href) if self._href else ""
            if target and not normalize(" ".join(self._parts)).endswith(target):
                self._parts.append(target)
            self._href = None
        elif tag in BLOCK_TAGS:
            self._break()

    def handle_data(self, data: str) -> None:
        self._parts.append(data)

    def close(self) -> None:
        super().close()
        self._break()

    def _break(self) -> None:
        line = normalize(" ".join(self._parts))
        if line:
            self.lines.append(line)
        self._parts = []


def unwrap_link(href: str) -> str:
    """Turns CurseForge's /linkout?remoteUrl=<doubly encoded> back into the target address."""
    if not href.startswith("/linkout?"):
        return href
    remote = urllib.parse.parse_qs(urllib.parse.urlsplit(href).query).get("remoteUrl", [""])[0]
    return urllib.parse.unquote(urllib.parse.unquote(remote)) or href


def normalize(text: str) -> str:
    text = text.replace("\xa0", " ")
    text = re.sub(r"\s*\|\s*", " | ", text)
    return re.sub(r"\s+", " ", text).strip().strip("|").strip()


def html_to_lines(html: str) -> list[str]:
    parser = DescriptionText()
    parser.feed(html)
    parser.close()
    return parser.lines


def markdown_to_lines(markdown: str) -> list[str]:
    lines = []
    for raw in markdown.splitlines():
        line = raw.strip()
        if not line or re.fullmatch(r"\|?[\s|:-]*\|[\s|:-]*", line):
            continue
        line = re.sub(r"^#{1,6}\s*", "", line)
        line = re.sub(r"^[-*]\s+", "", line)
        line = re.sub(r"\[([^\]]*)\]\(([^)]*)\)", r"\1 \2", line)
        line = line.replace("**", "").replace("`", "")
        line = re.sub(r"(?<!\w)\*([^*]+)\*(?!\w)", r"\1", line)
        line = normalize(line)
        if line:
            lines.append(line)
    return lines


def published_projects() -> dict[str, dict[str, int]]:
    block = re.search(r"voice-to-addon-to-project-id-json:\s*\n\s*- '(?P<json>.*?)'\s*\n",
                      WORKFLOW.read_text(encoding="utf-8"), re.S)
    if block is None:
        raise ValueError(f"{WORKFLOW} holds no project ID mapping")
    return json.loads(block.group("json"))


def diff(live_html: str, rendered_markdown: str, label: str) -> list[str]:
    return list(difflib.unified_diff(
        html_to_lines(live_html), markdown_to_lines(rendered_markdown),
        fromfile=f"{label} live", tofile=f"{label} repo", lineterm="", n=1))


def fetch(path: str, key: str):
    request = urllib.request.Request(
        f"{API}/{path}", headers={"x-api-key": key, "Accept": "application/json"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)["data"]


def live_description(project_id: int, key: str) -> str:
    return str(fetch(f"{project_id}/description", key))


def live_summary(project_id: int, key: str) -> str:
    return str(fetch(str(project_id), key).get("summary", ""))


def check(voice: str, addon: str, project_id: int, key: str) -> list[str]:
    summary, body = pages.render(addon, voice)
    label = f"{addon} ({voice})"
    findings = diff(live_description(project_id, key), body, label)
    published = normalize(live_summary(project_id, key))
    if published != normalize(summary):
        findings = [f"--- {label} summary live", f"+++ {label} summary repo",
                    f"-{published}", f"+{normalize(summary)}", *findings]
    return findings


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--api-key-file", type=Path, required=True)
    parser.add_argument("--show-diff", action="store_true")
    parser.add_argument("--fail-on-drift", action="store_true")
    return parser.parse_args()


def main() -> int:
    arguments = parse_args()
    key = arguments.api_key_file.read_text(encoding="utf-8").strip()
    projects = published_projects()
    drifted = []
    total = 0
    for voice, addons in sorted(projects.items()):
        for addon, project_id in sorted(addons.items()):
            total += 1
            findings = check(voice, addon, project_id, key)
            print(f"{'ok   ' if not findings else 'DRIFT'} {addon} ({voice})")
            if findings:
                drifted.append(f"{addon} ({voice})")
                if arguments.show_diff:
                    print("\n".join(f"    {line}" for line in findings))
    print(f"\n{len(drifted)} of {total} pages differ from docs/curseforge"
          + (": " + ", ".join(drifted) if drifted else ""))
    return 1 if drifted and arguments.fail_on_drift else 0


if __name__ == "__main__":
    raise SystemExit(main())
