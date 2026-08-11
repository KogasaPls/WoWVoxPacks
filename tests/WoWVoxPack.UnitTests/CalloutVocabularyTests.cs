using WoWVoxPack.AddOns.Callouts;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class CalloutVocabularyTests
{
    private static readonly Dictionary<string, PronunciationOverride> NoOverrides =
        new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Merge_KeepsCuratedEntriesWithNoMediaKeys()
    {
        SoundFile tranquility = new("tranquility.ogg", text: "Tranquility", displayName: "Tranquility");

        CalloutRegistration entry = Assert.Single(
            CalloutVocabulary.Merge([tranquility], [], NoOverrides));

        Assert.Equal("Tranquility", entry.SoundFile.DisplayName);
        Assert.Empty(entry.MediaKeys);
    }

    [Fact]
    public void Merge_CollapsesCollisionsAndKeepsTheSourceMediaKey()
    {
        SoundFile spread = new("spread.ogg", text: "Spread", displayName: "Spread");

        IReadOnlyList<CalloutRegistration> merged =
            CalloutVocabulary.Merge([spread], ["Spread"], NoOverrides);

        CalloutRegistration entry = Assert.Single(merged);
        Assert.Equal("spread.ogg", entry.SoundFile.FileName);
        Assert.Equal(["Spread"], entry.MediaKeys);
    }

    [Fact]
    public void Merge_KeepsSingularAndPluralAsDistinctEntries()
    {
        SoundFile soaks = new("soaks.ogg", text: "Soaks", displayName: "Soaks");

        IReadOnlyList<CalloutRegistration> merged =
            CalloutVocabulary.Merge([soaks], ["Soak"], NoOverrides);

        Assert.Equal(2, merged.Count);
        Assert.Contains(merged, e => e.SoundFile.DisplayName == "Soaks");
        Assert.Contains(merged, e => e.SoundFile.DisplayName == "Soak");
    }

    [Fact]
    public void Merge_SpellsNumbersWithoutChangingTheSourceMediaKey()
    {
        CalloutRegistration one = Assert.Single(CalloutVocabulary.Merge([], ["1"], NoOverrides));

        Assert.Equal("One", one.SoundFile.DisplayName);
        Assert.Equal("one.ogg", one.SoundFile.FileName);
        Assert.Equal(["1"], one.MediaKeys);
    }

    [Fact]
    public void Merge_SkipsExcludedNames()
    {
        Dictionary<string, PronunciationOverride> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Macro"] = new PronunciationOverride(Exclude: true)
        };

        IReadOnlyList<CalloutRegistration> merged =
            CalloutVocabulary.Merge([], ["Soak", "Macro"], overrides);

        Assert.Equal("Soak", Assert.Single(merged).SoundFile.DisplayName);
    }

    [Fact]
    public void Merge_ExpandsTheIpaConventionIntoSsml()
    {
        Dictionary<string, PronunciationOverride> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Fung"] = new PronunciationOverride(Text: "Fung=fʌŋ")
        };

        CalloutRegistration fung = Assert.Single(CalloutVocabulary.Merge([], ["Fung"], overrides));

        // Left unexpanded, Google TTS would say the '=' and the phonemes out loud.
        Assert.Equal("Fung", fung.SoundFile.DisplayName);
        Assert.Null(fung.SoundFile.Text);
        Assert.Contains("<phoneme", fung.SoundFile.Ssml);
        Assert.Contains("fʌŋ", fung.SoundFile.Ssml);
    }

    [Fact]
    public void Merge_LeavesPlainOverrideTextAlone()
    {
        Dictionary<string, PronunciationOverride> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["RunOut"] = new PronunciationOverride(Text: "Run Out")
        };

        CalloutRegistration entry = Assert.Single(CalloutVocabulary.Merge([], ["RunOut"], overrides));

        Assert.Equal("Run Out", entry.SoundFile.DisplayName);
        Assert.Equal("Run Out", entry.SoundFile.Text);
        Assert.Null(entry.SoundFile.Ssml);
    }

    [Fact]
    public void Merge_UsesAnExplicitCompatibilityFileName()
    {
        Dictionary<string, PronunciationOverride> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Invoke Yu'lon, the Jade Serpent"] =
                new PronunciationOverride(FileName: "invoke_yulon_the_jade_serpent.ogg")
        };

        CalloutRegistration entry = Assert.Single(
            CalloutVocabulary.Merge([], ["Invoke Yu'lon, the Jade Serpent"], overrides));

        Assert.Equal("invoke_yulon_the_jade_serpent.ogg", entry.SoundFile.FileName);
        Assert.Equal("Invoke Yu'lon, the Jade Serpent", entry.SoundFile.DisplayName);
    }

    [Fact]
    public void Merge_KeepsRetiredNamesRegisteredWhenAbsentFromCurrentSources()
    {
        // NSRT dropped the name upstream, but saved addon profiles still store the key string.
        // If it stops resolving, Fetch(..., true) returns nil and those addons play nothing,
        // with no message.
        CalloutRegistration entry = Assert.Single(
            CalloutVocabulary.Merge([], [], NoOverrides, retiredSoundNames: ["OldCallout"]));

        Assert.Equal("Old Callout", entry.SoundFile.DisplayName);
        Assert.True(entry.ReuseExistingAudioOnly);
    }

    [Fact]
    public void Merge_RetiredEntryReproducesTheSameContentItHadWhileLive()
    {
        // A retired entry must be indistinguishable, byte-for-byte, from what the same name
        // produced while NSRT still carried it: SoundFileManifest.FilesToCreate treats any
        // content difference as changed and re-renders it, which must cost nothing here.
        Dictionary<string, PronunciationOverride> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Fung"] = new PronunciationOverride(Text: "Fung=fʌŋ")
        };

        CalloutRegistration whileLive = Assert.Single(CalloutVocabulary.Merge([], ["Fung"], overrides));
        CalloutRegistration retired = Assert.Single(
            CalloutVocabulary.Merge([], [], overrides, retiredSoundNames: ["Fung"]));

        Assert.Equal(whileLive.SoundFile.FileName, retired.SoundFile.FileName);
        Assert.Equal(whileLive.SoundFile.DisplayName, retired.SoundFile.DisplayName);
        Assert.Equal(whileLive.SoundFile.Text, retired.SoundFile.Text);
        Assert.Equal(whileLive.SoundFile.Ssml, retired.SoundFile.Ssml);
        Assert.False(whileLive.ReuseExistingAudioOnly);
        Assert.True(retired.ReuseExistingAudioOnly);
    }

    [Fact]
    public void Merge_RetiredNamesDoNotDuplicateAnEntryStillPresentUpstream()
    {
        SoundFile soak = new("soak.ogg", text: "Soak", displayName: "Soak");

        IReadOnlyList<CalloutRegistration> merged =
            CalloutVocabulary.Merge([soak], [], NoOverrides, retiredSoundNames: ["Soak"]);

        Assert.Single(merged);
    }

    [Fact]
    public void Merge_KeepsARetiredNameThatUpstreamRenamedByCaseAlone()
    {
        // LibSharedMedia keys are case-sensitive Lua strings, so "Soak" and "soak" are two
        // separate keys. Merging case-insensitively would fold the retired one into the live
        // slot and silently stop registering the key that already shipped, with no count change
        // to notice and nothing failing.
        IReadOnlyList<CalloutRegistration> merged =
            CalloutVocabulary.Merge([], ["soak"], NoOverrides, retiredSoundNames: ["Soak"]);

        Assert.Equal(2, merged.Count);
        Assert.Contains(merged, e => e.SoundFile.DisplayName == "soak");
        Assert.Contains(merged, e => e.SoundFile.DisplayName == "Soak");
        Assert.False(Assert.Single(merged, e => e.SoundFile.DisplayName == "soak").ReuseExistingAudioOnly);
        Assert.True(Assert.Single(merged, e => e.SoundFile.DisplayName == "Soak").ReuseExistingAudioOnly);

        // Same recording: reviving a case-only rename must not bill a TTS render.
        Assert.Single(merged.Select(e => e.SoundFile.FileName).Distinct(StringComparer.Ordinal));

        // The addon builder and the manifest both key sounds case-insensitively, so the revived
        // entry needs a key of its own or it would be deduplicated back out.
        Assert.Equal(2, merged.Select(e => e.SoundFile.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Merge_RetiredNamesIgnoreExcludeOverrides()
    {
        // Exclude stops a name from being registered anew; it must not undo a key that already
        // shipped, or a later curator edit could silently reintroduce the exact bug this guards
        // against.
        Dictionary<string, PronunciationOverride> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Macro"] = new PronunciationOverride(Exclude: true)
        };

        IReadOnlyList<CalloutRegistration> merged =
            CalloutVocabulary.Merge([], [], overrides, retiredSoundNames: ["Macro"]);

        Assert.Equal("Macro", Assert.Single(merged).SoundFile.DisplayName);
    }
}
