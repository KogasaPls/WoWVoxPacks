import importlib.util
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]


def load(name):
    spec = importlib.util.spec_from_file_location(
        name, REPOSITORY_ROOT / "scripts" / f"{name}.py"
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


pages = load("curseforge_description")
drift = load("curseforge_drift")


class NormalizationTests(unittest.TestCase):
    def test_html_blocks_become_one_line_each(self):
        html = "<p>First line.</p>\n<ul>\n<li>Second</li>\n<li>Third</li>\n</ul>"
        self.assertEqual(["First line.", "Second", "Third"], drift.html_to_lines(html))

    def test_a_table_row_joins_its_cells(self):
        html = ("<table><thead><tr><th>Pack</th><th>Use it when</th></tr></thead>"
                "<tbody><tr><td>Callouts</td><td>by hand</td></tr></tbody></table>")
        self.assertEqual(["Pack | Use it when", "Callouts | by hand"], drift.html_to_lines(html))

    def test_curseforge_linkout_unwraps_to_the_address_it_shows(self):
        html = ('<p>See <a href="/linkout?remoteUrl=https%253a%252f%252florrgs.io%252f">'
                'lorrgs</a></p>')
        self.assertEqual(["See lorrgs https://lorrgs.io/"], drift.html_to_lines(html))

    def test_a_plain_href_is_left_alone(self):
        html = '<p><a href="https://example.com/x">text</a></p>'
        self.assertEqual(["text https://example.com/x"], drift.html_to_lines(html))

    def test_a_link_showing_its_own_address_is_not_doubled(self):
        html = ('<p><a href="/linkout?remoteUrl=https%253a%252f%252florrgs.io%252f">'
                'https://lorrgs.io/</a></p>')
        self.assertEqual(["https://lorrgs.io/"], drift.html_to_lines(html))

    def test_markdown_drops_its_syntax_and_the_table_rule(self):
        markdown = ("# Title\n\nSome **bold** and `code`.\n\n"
                    "| Pack | Use it when |\n| --- | --- |\n| Callouts | by hand |\n\n"
                    "- A bullet\n")
        self.assertEqual(
            ["Title", "Some bold and code.", "Pack | Use it when", "Callouts | by hand",
             "A bullet"],
            drift.markdown_to_lines(markdown))

    def test_a_markdown_link_keeps_its_text_and_target(self):
        self.assertEqual(["lorrgs https://lorrgs.io/"],
                         drift.markdown_to_lines("[lorrgs](https://lorrgs.io/)"))

    def test_punctuation_around_an_inline_element_does_not_count_as_drift(self):
        html = ('<p>set it to <code>WoWVoxPacks: Neural2_C</code>. See '
                '<a href="https://example.com/x">the page</a>, then stop.</p>')
        self.assertEqual(
            drift.markdown_to_lines(
                "set it to `WoWVoxPacks: Neural2_C`. See [the page](https://example.com/x), "
                "then stop."),
            drift.html_to_lines(html))

    def test_an_inline_element_opening_a_bracket_does_not_count_as_drift(self):
        html = "<p>names (<code>Tranquility</code>, <code>Rally</code>) matter</p>"
        self.assertEqual(drift.markdown_to_lines("names (`Tranquility`, `Rally`) matter"),
                         drift.html_to_lines(html))

    def test_non_breaking_space_does_not_count_as_drift(self):
        self.assertEqual(drift.html_to_lines("<p>a\xa0b</p>"), drift.markdown_to_lines("a b"))


class DriftTests(unittest.TestCase):
    def test_a_page_republished_unchanged_reports_no_drift(self):
        _, body = pages.render("Callouts", "Wavenet_E")
        republished = as_curseforge_html(body)
        self.assertEqual([], drift.diff(republished, body, "Callouts (Wavenet_E)"))

    def test_an_edited_sentence_is_reported(self):
        _, body = pages.render("ExBoss", "Studio_Q")
        edited = as_curseforge_html(body.replace("A TTS voice pack", "A voice pack"))
        findings = drift.diff(edited, body, "ExBoss (Studio_Q)")
        self.assertTrue(any(line.startswith("+A TTS voice pack") for line in findings), findings)

    def test_every_published_project_resolves_to_a_page(self):
        for voice, addons in drift.published_projects().items():
            for addon, project_id in addons.items():
                with self.subTest(voice=voice, addon=addon):
                    self.assertIsInstance(project_id, int)
                    summary, body = pages.render(addon, voice)
                    self.assertTrue(summary.strip())
                    self.assertTrue(body.strip())


def as_curseforge_html(markdown: str) -> str:
    """The HTML CurseForge stores for a pasted page, block for block."""
    html = []
    rows = []
    for line in drift.markdown_to_lines(markdown):
        if " | " in line:
            rows.append("<tr>" + "".join(f"<td>{cell}</td>" for cell in line.split(" | ")) + "</tr>")
            continue
        if rows:
            html.append("<table><tbody>" + "".join(rows) + "</tbody></table>")
            rows = []
        html.append(f"<p>{line}</p>")
    if rows:
        html.append("<table><tbody>" + "".join(rows) + "</tbody></table>")
    return "\n".join(html)


if __name__ == "__main__":
    unittest.main()
