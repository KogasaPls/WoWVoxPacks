using System.Text.Json;

using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

/// <summary>
/// One convention for phonetics: a hand-authored entry says what is spoken in its text and how in
/// its Pronunciations. The "Word=IPA" escape belongs to the upstream BigWigs spell lists, which
/// this repo does not author, and is lifted on import.
/// </summary>
public class PronunciationConventionTests
{
    public static TheoryData<string> SoundFileManifests =>
    [
        "src/WoWVoxPack.AddOns.BigWigs_Voice/BigWigsVoice_Sounds.json",
        "src/WoWVoxPack.AddOns.Callouts/Callouts_Sounds.json",
        "src/WoWVoxPack.AddOns.Callouts/CalloutPronunciations.json",
        "src/WoWVoxPack.AddOns.ExBoss/Labels.json"
    ];

    [Theory]
    [MemberData(nameof(SoundFileManifests))]
    public void HandAuthoredData_SpellsPronunciationsOutOfTheSpokenText(string repoPath)
    {
        string content = File.ReadAllText(FindRepoFile(repoPath));

        Assert.DoesNotContain("<phoneme", content, StringComparison.OrdinalIgnoreCase);

        using JsonDocument document = JsonDocument.Parse(content);
        foreach ((_, JsonElement entry) in Entries(document.RootElement))
        {
            if (entry.TryGetProperty("Text", out JsonElement text))
            {
                Assert.DoesNotContain('=', text.GetString() ?? string.Empty);
            }
        }
    }

    [Theory]
    [MemberData(nameof(SoundFileManifests))]
    public void EveryPronunciationPhrase_AppearsInTheTextItCustomises(string repoPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(FindRepoFile(repoPath)));

        foreach ((string name, JsonElement entry) in Entries(document.RootElement))
        {
            if (!entry.TryGetProperty("Pronunciations", out JsonElement pronunciations))
            {
                continue;
            }

            // An override with no text of its own is spoken as its own key, the way the callout
            // vocabularies fall back to the media key for both the display name and the text.
            string text = entry.TryGetProperty("Text", out JsonElement value)
                ? value.GetString() ?? string.Empty
                : name;
            foreach (JsonElement pronunciation in pronunciations.EnumerateArray())
            {
                // Google applies a customisation by matching the phrase in the input, so a phrase
                // the text does not contain is a silent no-op rather than an error.
                string phrase = pronunciation.GetProperty("Phrase").GetString()!;
                Assert.Contains(phrase, text, StringComparison.Ordinal);
                Assert.NotEmpty(pronunciation.GetProperty("Ipa").GetString()!);
            }
        }
    }

    [Fact]
    public void UpstreamSpellNames_StillLiftTheirIpaEscape()
    {
        (string text, IReadOnlyList<Pronunciation> pronunciations) = SoundFile.ParseIpaHints("Tempest Winds=wɪndz");

        Assert.Equal("Tempest Winds", text);
        Assert.Equal([new Pronunciation("Winds", "wɪndz")], pronunciations);
    }

    private static IEnumerable<(string Name, JsonElement Entry)> Entries(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            ? root.EnumerateObject().Select(property => (property.Name, property.Value))
            : root.EnumerateArray().Select(entry => (
                entry.TryGetProperty("DisplayName", out JsonElement name) ? name.GetString() ?? string.Empty : string.Empty,
                entry));
    }

    private static string FindRepoFile(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, fileName)))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, fileName);
    }
}
