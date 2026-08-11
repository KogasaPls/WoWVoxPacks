using WoWVoxPack.AddOns.Callouts;

namespace WoWVoxPack.UnitTests;

public class CalloutVocabularyProviderTests
{
    [Fact]
    public void Callouts_UsesCuratedPlayerReminderAndRetiredSourcesOnly()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(directory, "Callouts_Sounds.json"),
            """[{"FileName":"tranquility.ogg","Text":"Tranquility","DisplayName":"Tranquility"}]""");
        File.WriteAllText(Path.Combine(directory, "CalloutPronunciations.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "nsrt-vocabulary.txt"), "DropPool\n");
        File.WriteAllText(Path.Combine(directory, "lorrgs-vocabulary.txt"), "Anti-Magic Shell\n");
        File.WriteAllText(Path.Combine(directory, "RetiredCallouts.json"), "[\"OldCallout\"]");

        CalloutsVocabularyProvider provider = new(
            Path.Combine(directory, "Callouts_Sounds.json"),
            Path.Combine(directory, "CalloutPronunciations.json"),
            Path.Combine(directory, "lorrgs-vocabulary.txt"),
            Path.Combine(directory, "RetiredCallouts.json"));

        IReadOnlyList<CalloutRegistration> first = provider.Registrations;

        Assert.Same(first, provider.Registrations);
        Assert.Contains(first, r => r.SoundFile.DisplayName == "Tranquility");
        Assert.Contains(first, r =>
            r.SoundFile.DisplayName == "Anti-Magic Shell"
            && r.MediaKeys.Contains("Anti-Magic Shell"));
        Assert.Contains(first, r => r.SoundFile.DisplayName == "Old Callout");
        Assert.DoesNotContain(first, r => r.MediaKeys.Contains("DropPool"));
    }

    [Fact]
    public void NorthernSkyRaidTools_UsesItsVocabularyOnlyAndKeepsEveryLiteralMediaKey()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(directory, "CalloutPronunciations.json"),
            "{\"DropPool\":{\"Text\":\"Drop Pool\"}}");
        File.WriteAllText(Path.Combine(directory, "nsrt-vocabulary.txt"),
            "# provenance\n\n  DropPool  \nDropPool\ndroppool\nDropPool\n");
        File.WriteAllText(Path.Combine(directory, "lorrgs-vocabulary.txt"), "Anti-Magic Shell\n");
        File.WriteAllText(Path.Combine(directory, "RetiredCallouts.json"), "[\"OldCallout\"]");

        NorthernSkyRaidToolsVocabularyProvider provider = new(
            Path.Combine(directory, "nsrt-vocabulary.txt"),
            Path.Combine(directory, "CalloutPronunciations.json"));

        IReadOnlyList<CalloutRegistration> registrations = provider.Registrations;

        Assert.Equal(
            ["  DropPool  ", "DropPool", "droppool", "DropPool"],
            registrations.SelectMany(registration => registration.MediaKeys));
        CalloutRegistration[] dropPoolRegistrations = registrations
            .Where(registration => registration.MediaKeys.SequenceEqual(["DropPool"]))
            .ToArray();
        Assert.Equal(2, dropPoolRegistrations.Length);
        Assert.All(dropPoolRegistrations,
            registration => Assert.Equal("Drop Pool", registration.SoundFile.DisplayName));
        Assert.All(dropPoolRegistrations,
            registration => Assert.Equal("Drop Pool", registration.SoundFile.Text));
        Assert.Same(dropPoolRegistrations[0].SoundFile,
            registrations.Single(registration => registration.MediaKeys.SequenceEqual(["droppool"])).SoundFile);
        Assert.DoesNotContain(registrations,
            r => r.SoundFile.DisplayName is "Anti-Magic Shell" or "Old Callout");
    }

    [Fact]
    public void Callouts_ToleratesAMissingRetiredCalloutsFile()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(directory, "Callouts_Sounds.json"), "[]");
        File.WriteAllText(Path.Combine(directory, "CalloutPronunciations.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "lorrgs-vocabulary.txt"), "");

        CalloutsVocabularyProvider provider = new(
            Path.Combine(directory, "Callouts_Sounds.json"),
            Path.Combine(directory, "CalloutPronunciations.json"),
            Path.Combine(directory, "lorrgs-vocabulary.txt"),
            Path.Combine(directory, "RetiredCallouts.json"));

        Assert.Empty(provider.Registrations);
    }
}
