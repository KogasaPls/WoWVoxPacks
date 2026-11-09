using WoWVoxPack.AddOns.Callouts;

namespace WoWVoxPack.UnitTests;

public class CalloutVocabularyProviderTests
{
    private const string OverridesPath = "src/WoWVoxPack.AddOns.Callouts/CalloutPronunciations.json";

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
            [Path.Combine(directory, "lorrgs-vocabulary.txt")],
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
            [Path.Combine(directory, "nsrt-vocabulary.txt")],
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
    public void NorthernSkyRaidTools_MergesTheSupplementaryVocabulary()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(directory, "CalloutPronunciations.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "nsrt-vocabulary.txt"), "DropPool\n");
        File.WriteAllText(Path.Combine(directory, "nsrt-extra-vocabulary.txt"),
            "# alerts with no upstream file\nTaunt\nMemory Game\n");

        NorthernSkyRaidToolsVocabularyProvider provider = new(
            [
                Path.Combine(directory, "nsrt-vocabulary.txt"),
                Path.Combine(directory, "nsrt-extra-vocabulary.txt")
            ],
            Path.Combine(directory, "CalloutPronunciations.json"));

        IReadOnlyList<CalloutRegistration> registrations = provider.Registrations;

        Assert.Equal(["DropPool", "Taunt", "Memory Game"],
            registrations.SelectMany(registration => registration.MediaKeys));
        Assert.Equal("memory_game.ogg",
            registrations.Single(r => r.MediaKeys.Contains("Memory Game")).SoundFile.FileName);
    }

    [Fact]
    public void NorthernSkyRaidTools_FoldsCalloutsInUnderPlainKeys()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(directory, "Callouts_Sounds.json"),
            """[{"FileName":"tranquility.ogg","Text":"Tranquility","DisplayName":"Tranquility"}]""");
        File.WriteAllText(Path.Combine(directory, "CalloutPronunciations.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "lorrgs-vocabulary.txt"),
            "Anti-Magic Shell\nDarkness\n");
        File.WriteAllText(Path.Combine(directory, "RetiredCallouts.json"), "[\"OldCallout\"]");
        File.WriteAllText(Path.Combine(directory, "nsrt-vocabulary.txt"), "DropPool\ndarkness\n");

        CalloutsVocabularyProvider callouts = new(
            Path.Combine(directory, "Callouts_Sounds.json"),
            Path.Combine(directory, "CalloutPronunciations.json"),
            [Path.Combine(directory, "lorrgs-vocabulary.txt")],
            Path.Combine(directory, "RetiredCallouts.json"));
        NorthernSkyRaidToolsVocabularyProvider provider = new(
            [Path.Combine(directory, "nsrt-vocabulary.txt")],
            Path.Combine(directory, "CalloutPronunciations.json"),
            callouts);

        IReadOnlyList<CalloutRegistration> registrations = provider.Registrations;

        CalloutRegistration folded = registrations.Single(r => r.MediaKeys.Contains("Anti-Magic Shell"));
        Assert.Same(
            callouts.Registrations.Single(r => r.SoundFile.DisplayName == "Anti-Magic Shell").SoundFile,
            folded.SoundFile);
        Assert.Contains(registrations, r => r.MediaKeys.SequenceEqual(["Tranquility"]));
        Assert.Contains(registrations, r => r.MediaKeys.SequenceEqual(["darkness"]));
        Assert.DoesNotContain(registrations, r => r.MediaKeys.Contains("Darkness"));
        Assert.DoesNotContain(provider.SoundFilesFor(directory), f => f.DisplayName == "Darkness");
    }

    [Fact]
    public void NorthernSkyRaidTools_KeepsARetiredCalloutOnlyWhileItsRecordingExists()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(directory, "Callouts_Sounds.json"), "[]");
        File.WriteAllText(Path.Combine(directory, "CalloutPronunciations.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "lorrgs-vocabulary.txt"), string.Empty);
        File.WriteAllText(Path.Combine(directory, "RetiredCallouts.json"), "[\"OldCallout\"]");
        File.WriteAllText(Path.Combine(directory, "nsrt-vocabulary.txt"), "DropPool\n");

        CalloutsVocabularyProvider callouts = new(
            Path.Combine(directory, "Callouts_Sounds.json"),
            Path.Combine(directory, "CalloutPronunciations.json"),
            [Path.Combine(directory, "lorrgs-vocabulary.txt")],
            Path.Combine(directory, "RetiredCallouts.json"));
        NorthernSkyRaidToolsVocabularyProvider provider = new(
            [Path.Combine(directory, "nsrt-vocabulary.txt")],
            Path.Combine(directory, "CalloutPronunciations.json"),
            callouts);

        CalloutRegistration retired = provider.Registrations
            .Single(r => r.MediaKeys.Contains("OldCallout"));
        Assert.True(retired.ReuseExistingAudioOnly);
        Assert.DoesNotContain(provider.SoundFilesFor(directory), f => f.DisplayName == "Old Callout");

        File.WriteAllBytes(Path.Combine(directory, "old_callout.ogg"), [1]);

        Assert.Contains(provider.SoundFilesFor(directory), f => f.DisplayName == "Old Callout");
    }

    [Fact]
    public void NorthernSkyRaidTools_RejectsACalloutThatContestsANativeRecording()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(directory, "Callouts_Sounds.json"), "[]");
        File.WriteAllText(Path.Combine(directory, "CalloutPronunciations.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "lorrgs-vocabulary.txt"), "Anti-Magic Shell\n");
        File.WriteAllText(Path.Combine(directory, "nsrt-vocabulary.txt"), "AntiMagicShell\n");

        CalloutsVocabularyProvider callouts = new(
            Path.Combine(directory, "Callouts_Sounds.json"),
            Path.Combine(directory, "CalloutPronunciations.json"),
            [Path.Combine(directory, "lorrgs-vocabulary.txt")],
            Path.Combine(directory, "RetiredCallouts.json"));
        NorthernSkyRaidToolsVocabularyProvider provider = new(
            [Path.Combine(directory, "nsrt-vocabulary.txt")],
            Path.Combine(directory, "CalloutPronunciations.json"),
            callouts);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => provider.Registrations);
        Assert.Contains("anti_magic_shell.ogg", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NorthernSkyRaidTools_RejectsACalloutWhoseKeyRendersADifferentFile()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(directory, "Callouts_Sounds.json"), "[]");
        File.WriteAllText(Path.Combine(directory, "CalloutPronunciations.json"),
            """{"Old Callout":{"FileName":"special.ogg"}}""");
        File.WriteAllText(Path.Combine(directory, "lorrgs-vocabulary.txt"), "OldCallout\n");
        File.WriteAllText(Path.Combine(directory, "nsrt-vocabulary.txt"), "Old Callout\n");

        CalloutsVocabularyProvider callouts = new(
            Path.Combine(directory, "Callouts_Sounds.json"),
            Path.Combine(directory, "CalloutPronunciations.json"),
            [Path.Combine(directory, "lorrgs-vocabulary.txt")],
            Path.Combine(directory, "RetiredCallouts.json"));
        NorthernSkyRaidToolsVocabularyProvider provider = new(
            [Path.Combine(directory, "nsrt-vocabulary.txt")],
            Path.Combine(directory, "CalloutPronunciations.json"),
            callouts);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => provider.Registrations);
        Assert.Contains("shares its key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Callouts_ToleratesAMissingRetiredCalloutsFile()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(directory, "Callouts_Sounds.json"), "[]");
        File.WriteAllText(Path.Combine(directory, "CalloutPronunciations.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "lorrgs-vocabulary.txt"), string.Empty);

        CalloutsVocabularyProvider provider = new(
            Path.Combine(directory, "Callouts_Sounds.json"),
            Path.Combine(directory, "CalloutPronunciations.json"),
            [Path.Combine(directory, "lorrgs-vocabulary.txt")],
            Path.Combine(directory, "RetiredCallouts.json"));

        Assert.Empty(provider.Registrations);
    }

    [Fact]
    public void ForeverNsrt_RegistersNoAlertOnlyName()
    {
        IReadOnlyDictionary<string, PronunciationOverride> overrides =
            CalloutPronunciation.LoadOverrides(FindRepoFile(OverridesPath));
        HashSet<string> sharedFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (string fileName in (string[])["nsrt-vocabulary.txt", "nsrt-note-vocabulary.txt", "callout-vocabulary.txt"])
        {
            sharedFiles.UnionWith(NorthernSkyRaidToolsVocabulary.Parse(File.ReadLines(FindRepoFile(fileName)))
                .Select(name => CalloutPronunciation.DescribeSoundFile(name, overrides).FileName));
        }

        HashSet<string> alertOnly = new(
            NorthernSkyRaidToolsVocabulary.Parse(File.ReadLines(FindRepoFile("nsrt-alert-vocabulary.txt")))
                .Where(name => !sharedFiles.Contains(CalloutPronunciation.DescribeSoundFile(name, overrides).FileName)),
            StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(alertOnly);

        IEnumerable<string> registered = ForeverNsrt().Registrations
            .SelectMany(r => r.MediaKeys);

        Assert.DoesNotContain(registered, alertOnly.Contains);
    }

    [Fact]
    public void ForeverNsrt_RegistersNoRebuffOrDodge()
    {
        string[] displayNames = [.. ForeverNsrt().Registrations.Select(r => r.SoundFile.DisplayName)];

        Assert.DoesNotContain(displayNames, name => name.StartsWith("Rebuff ", StringComparison.Ordinal));
        Assert.DoesNotContain(displayNames, name => name != "Dodge" && name.Contains("Dodge", StringComparison.Ordinal));
        Assert.DoesNotContain("Drop-Pool", displayNames);
    }

    [Fact]
    public void ForeverNsrt_RegistersNotePhrases()
    {
        string[] displayNames = [.. ForeverNsrt().Registrations.Select(r => r.SoundFile.DisplayName)];

        Assert.Contains("Preposition at {star}", displayNames);
        Assert.Contains("Soak {rt1}", displayNames);
    }

    [Fact]
    public void ForeverCallouts_ExcludesCuratedAndLorrgs()
    {
        string[] displayNames = [.. ForeverCallouts().Registrations.Select(r => r.SoundFile.DisplayName)];

        Assert.DoesNotContain("Convoke", displayNames);
        Assert.DoesNotContain("Anti-Magic Zone", displayNames);
        Assert.Contains("Adds", displayNames);
    }

    [Fact]
    public void RetailProviders_Unchanged()
    {
        CalloutsVocabularyProvider callouts = RetailCallouts();
        IReadOnlyList<CalloutRegistration> retail = new NorthernSkyRaidToolsVocabularyProvider(
            [.. NorthernSkyRaidToolsVocabulary.VocabularyFileNames.Select(FindRepoFile)],
            FindRepoFile(OverridesPath),
            callouts).Registrations;
        IReadOnlyList<CalloutRegistration> expected = NorthernSkyRaidToolsVocabulary.Load(
            [.. NorthernSkyRaidToolsVocabulary.VocabularyFileNames.Select(FindRepoFile)],
            CalloutPronunciation.LoadOverrides(FindRepoFile(OverridesPath)),
            callouts.Registrations);

        Assert.Equal(expected.Count, retail.Count);
        Assert.Equal(
            expected.SelectMany(r => r.MediaKeys),
            retail.SelectMany(r => r.MediaKeys));
        Assert.Contains(retail, r => r.SoundFile.DisplayName.StartsWith("Rebuff ", StringComparison.Ordinal));
        Assert.Contains(callouts.Registrations, r => r.SoundFile.DisplayName == "Convoke");
    }

    private static CalloutsVocabularyProvider RetailCallouts() =>
        new(
            FindRepoFile("src/WoWVoxPack.AddOns.Callouts/Callouts_Sounds.json"),
            FindRepoFile(OverridesPath),
            [.. CalloutVocabulary.VocabularyFileNames.Select(FindRepoFile)],
            FindRepoFile("src/WoWVoxPack.AddOns.Callouts/RetiredCallouts.json"));

    private static CalloutsVocabularyProvider ForeverCallouts() =>
        new(
            null,
            FindRepoFile(OverridesPath),
            [.. CalloutVocabulary.ForeverVocabularyFileNames.Select(FindRepoFile)],
            null);

    private static NorthernSkyRaidToolsVocabularyProvider ForeverNsrt() =>
        new(
            [.. NorthernSkyRaidToolsVocabulary.ForeverVocabularyFileNames.Select(FindRepoFile)],
            FindRepoFile(OverridesPath),
            ForeverCallouts());

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
