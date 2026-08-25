using System.Text.Json;

using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

/// <summary>
/// One convention for phonetics: locally authored IPA lives in the root pronunciation catalog.
/// The "Word=IPA" escape belongs only to imported BigWigs spell lists.
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
    public void AddOnData_ContainsNoLocallyAuthoredIpa(string repoPath)
    {
        string content = File.ReadAllText(FindRepoFile(repoPath));

        Assert.DoesNotContain("<phoneme", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Pronunciations\"", content, StringComparison.Ordinal);

        using JsonDocument document = JsonDocument.Parse(content);
        foreach ((_, JsonElement entry) in Entries(document.RootElement))
        {
            if (entry.TryGetProperty("Text", out JsonElement text))
            {
                Assert.DoesNotContain('=', text.GetString() ?? string.Empty);
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
