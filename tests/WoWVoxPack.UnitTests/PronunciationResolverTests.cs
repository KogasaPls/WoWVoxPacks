using Microsoft.Extensions.Logging.Abstractions;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class PronunciationResolverTests
{
    [Fact]
    public void Resolve_AppliesOneExactRuleToEveryMatchingIdAndAddon()
    {
        PronunciationCatalog catalog = new(
            [new ExactNameRule("Axegrinder", Text: "Axe grinder")], [], []);
        AddOnDraft first = Draft("BigWigs_Voice",
            new SoundFile("1283832.ogg", text: "Axegrinder", displayName: "Axegrinder")
                { ExplicitKey = "1283832" });
        AddOnDraft second = Draft("AnotherAddon",
            new SoundFile("axegrinder.ogg", text: "Axegrinder", displayName: "Axegrinder"));

        SoundFile[] sounds = new PronunciationResolver(catalog,
                NullLogger<PronunciationResolver>.Instance)
            .Resolve([first, second]).SelectMany(addOn => addOn.SoundFiles).ToArray();

        Assert.All(sounds, sound => Assert.Equal("Axe grinder", sound.Text));
    }

    [Fact]
    public void Resolve_AppliesGlobalPhraseUsingActualAliasSpelling()
    {
        PronunciationCatalog catalog = new([], [
            new PhraseRule("Chi-Ji", "tʃiːdʒiː", ["Chi Ji"])
        ], []);
        AddOnDraft draft = Draft("Callouts",
            new SoundFile("chi.ogg", text: "Invoke Chi Ji", displayName: "Invoke Chi Ji"));

        SoundFile sound = Assert.Single(new PronunciationResolver(catalog,
            NullLogger<PronunciationResolver>.Instance).Resolve([draft]).Single().SoundFiles);

        Assert.Equal(new Pronunciation("Chi Ji", "tʃiːdʒiː"), Assert.Single(sound.Pronunciations!));
    }

    [Fact]
    public void Resolve_NamesUpstreamOriginWhenCandidatesConflict()
    {
        SoundFile sound = new("1.ogg", text: "Bomb", displayName: "Bomb")
        {
            ImportedPronunciations =
            [new PronunciationHint(new Pronunciation("Bomb", "bɑm"), "upstream:spells.txt:4")]
        };
        PronunciationCatalog catalog = new([], [new PhraseRule("Bomb", "bɒm")], []);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new PronunciationResolver(catalog, NullLogger<PronunciationResolver>.Instance)
                .Resolve([Draft("BigWigs_Voice", sound)]));

        Assert.Contains("upstream:spells.txt:4", error.Message);
        Assert.Contains("Bomb", error.Message);
    }

    [Fact]
    public void Resolve_UsesScopedExceptionToReplaceAConflict()
    {
        SoundFile sound = new("1.ogg", text: "Bomb", displayName: "Bomb")
        {
            ExplicitKey = "1",
            ImportedPronunciations =
            [new PronunciationHint(new Pronunciation("Bomb", "bɑm"), "upstream:spells.txt:4")]
        };
        PronunciationCatalog catalog = new([], [new PhraseRule("Bomb", "bɒm")], [
            new PronunciationExceptionRule("BigWigs_Voice", "1", "Bomb", Ipa: "bʌm",
                Reason: "This recording deliberately uses the encounter-specific vowel.")
        ]);

        SoundFile resolved = Assert.Single(new PronunciationResolver(catalog,
            NullLogger<PronunciationResolver>.Instance)
            .Resolve([Draft("BigWigs_Voice", sound)]).Single().SoundFiles);

        Assert.Equal(new Pronunciation("Bomb", "bʌm"), Assert.Single(resolved.Pronunciations!));
    }

    [Theory]
    [InlineData("Fung", "Fung", "fʌŋ")]
    [InlineData("Innervate", "Innervate", "ˈɪnɚveɪt")]
    [InlineData("Invoke Chi-Ji", "Chi-Ji", "tʃiːdʒiː")]
    [InlineData("Invoke Yu'lon", "Yu'lon", "ˈjuːlɒn")]
    [InlineData("Rebuff Arcane Intellect", "Rebuff", "ˌriːˈbʌf")]
    public void Resolve_UsesTheRootCatalogForSharedCalloutPronunciations(
        string text,
        string phrase,
        string ipa)
    {
        PronunciationCatalog catalog = PronunciationCatalog.Load(FindRepoFile("pronunciations.json"));

        SoundFile sound = Assert.Single(new PronunciationResolver(catalog,
            NullLogger<PronunciationResolver>.Instance)
            .Resolve([Draft("Callouts", new SoundFile("callout.ogg", text, displayName: text))])
            .Single().SoundFiles);

        Assert.Contains(new Pronunciation(phrase, ipa), sound.Pronunciations!);
    }

    [Fact]
    public void Resolve_TreatsCapitalizationOnlyTextDifferencesAsTheSameSpokenContent()
    {
        AddOnDraft draft = Draft("BigWigs_Voice",
            new SoundFile("235578.ogg", "Grasp from Beyond", displayName: "Grasp from Beyond")
                { ExplicitKey = "235578" },
            new SoundFile("443042.ogg", "Grasp From Beyond", displayName: "Grasp From Beyond")
                { ExplicitKey = "443042" });

        AddOn addOn = new PronunciationResolver(PronunciationCatalog.Empty,
            NullLogger<PronunciationResolver>.Instance).Resolve([draft]).Single();

        Assert.Equal(2, addOn.SoundFiles.Count());
    }

    [Fact]
    public void Resolve_TreatsTerminalPunctuationOnlyDifferencesAsTheSameSpokenContent()
    {
        AddOnDraft draft = Draft("BigWigs_Voice",
            new SoundFile("240319.ogg", "Hatching", displayName: "Hatching")
                { ExplicitKey = "240319" },
            new SoundFile("453937.ogg", "Hatching...", displayName: "Hatching...")
                { ExplicitKey = "453937" });

        AddOn addOn = new PronunciationResolver(PronunciationCatalog.Empty,
            NullLogger<PronunciationResolver>.Instance).Resolve([draft]).Single();

        Assert.Equal(2, addOn.SoundFiles.Count());
    }

    [Fact]
    public void Resolve_RejectsAnExactPronunciationPhraseMissingFromResolvedText()
    {
        PronunciationCatalog catalog = new([
            new ExactNameRule("Alert", Pronunciations: [new Pronunciation("Missing", "mɪsɪŋ")])
        ], [], []);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new PronunciationResolver(catalog, NullLogger<PronunciationResolver>.Instance)
                .Resolve([Draft("Callouts", new SoundFile("alert.ogg", "Alert", displayName: "Alert"))]));

        Assert.Contains("Missing", error.Message);
        Assert.Contains("catalog:exact:Alert", error.Message);
    }

    [Fact]
    public void Resolve_DoesNotCompareSpokenContentBetweenVoices()
    {
        AddOnDraft neural = DraftForVoice("Callouts", VoiceName.Neural2_C,
            new SoundFile("alert.ogg", "Voice-specific neural text", displayName: "Alert"));
        AddOnDraft studio = DraftForVoice("Callouts", VoiceName.Studio_O,
            new SoundFile("alert.ogg", "Voice-specific studio text", displayName: "Alert"));

        IReadOnlyList<AddOn> addOns = new PronunciationResolver(PronunciationCatalog.Empty,
            NullLogger<PronunciationResolver>.Instance).Resolve([neural, studio]);

        Assert.Equal(2, addOns.Count);
    }

    [Fact]
    public void Resolve_DoesNotLetAnUnrelatedExceptionWaiveSpokenTextMismatch()
    {
        PronunciationCatalog catalog = new([], [], [
            new PronunciationExceptionRule("Callouts", "first", "Unrelated", Ipa: "ʌnɹɪleɪtɪd",
                Reason: "This phrase is unrelated to the conflicting text.")
        ]);
        AddOnDraft draft = Draft("Callouts",
            new SoundFile("first.ogg", "First text", displayName: "Alert") { ExplicitKey = "first" },
            new SoundFile("second.ogg", "Second text", displayName: "Alert") { ExplicitKey = "second" });

        Assert.Throws<InvalidOperationException>(() =>
            new PronunciationResolver(catalog, NullLogger<PronunciationResolver>.Instance).Resolve([draft]));
    }

    private static AddOnDraft Draft(string addOnId, params SoundFile[] soundFiles)
    {
        return DraftForVoice(addOnId, VoiceName.Neural2_C, soundFiles);
    }

    private static AddOnDraft DraftForVoice(
        string addOnId,
        VoiceName voice,
        params SoundFile[] soundFiles)
    {
        AddOnSettings settings = new()
        {
            Title = addOnId,
            Version = "12.0.7",
            Author = "Tester"
        };

        return new AddOnBuilder(settings, new TtsSettings { Voice = voice }, addOnId)
            .AddSoundFiles(soundFiles)
            .BuildDraft("/tmp/output");
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
