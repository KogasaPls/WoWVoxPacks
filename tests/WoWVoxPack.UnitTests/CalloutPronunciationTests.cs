using WoWVoxPack.AddOns.Callouts;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class CalloutPronunciationTests
{
    [Theory]
    [InlineData("Soak", "Soak")]
    [InlineData("MindControl", "Mind Control")]
    [InlineData("DropPool", "Drop Pool")]
    [InlineData("HealAbsorb", "Heal Absorb")]
    [InlineData("RunOut", "Run Out")]
    [InlineData("1", "One")]
    [InlineData("10", "Ten")]
    public void ToDisplayName_SplitsPascalCaseAndSpellsNumbers(string soundName, string expected) =>
        Assert.Equal(expected, CalloutPronunciation.ToDisplayName(soundName));

    [Theory]
    [InlineData("Soak", "soak.ogg")]
    [InlineData("Mind Control", "mind_control.ogg")]
    [InlineData("Yu'lon", "yu_lon.ogg")]
    public void ToFileName_LowercasesAndCollapsesSeparators(string displayName, string expected) =>
        Assert.Equal(expected, CalloutPronunciation.ToFileName(displayName));

    [Fact]
    public void LoadOverrides_ReadsAComposition()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                """{"5seconds321":{"Composition":{"Duration":5.7,"Parts":[{"Key":"3","At":2.15},{"Key":"2","At":3.15}]}}}""");

            CompositionOverride composition =
                CalloutPronunciation.LoadOverrides(path)["5seconds321"].Composition!;

            Assert.Equal(5.7, composition.Duration);
            Assert.Equal([new CompositionPartOverride("3", 2.15), new CompositionPartOverride("2", 3.15)],
                composition.Parts);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Parts name media keys, so they follow the same spelling and file rules as any key.</summary>
    [Fact]
    public void DescribeSoundFile_ComposesFromTheRecordingsItsPartsResolveTo()
    {
        Dictionary<string, PronunciationOverride> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["5seconds321"] = new(Composition: new CompositionOverride(5.7,
                [new CompositionPartOverride("3", 2.15), new CompositionPartOverride("Go", 4.15)])),
            ["Go"] = new(FileName: "go_now.ogg", Text: "Go now")
        };

        SoundFile sound = CalloutPronunciation.DescribeSoundFile("5seconds321", overrides);

        Assert.Equal("5seconds321.ogg", sound.FileName);
        Assert.Null(sound.Text);
        Assert.Null(sound.Ssml);
        Assert.Equal(
            new SoundComposition(5.7,
                [new SoundCompositionPart("three.ogg", 2.15), new SoundCompositionPart("go_now.ogg", 4.15)]),
            sound.Composition);
    }

    [Fact]
    public void DescribeSoundFile_SpellsPartFileNamesTheWaySoundFilesAreSpelled()
    {
        Dictionary<string, PronunciationOverride> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Count"] = new(Composition: new CompositionOverride(2, [new CompositionPartOverride("Go", 1)])),
            ["Go"] = new(FileName: "Go_Now.OGG")
        };

        SoundFile go = CalloutPronunciation.DescribeSoundFile("Go", overrides);
        SoundFile count = CalloutPronunciation.DescribeSoundFile("Count", overrides);

        Assert.Equal(go.FileName, Assert.Single(count.Composition!.Parts).FileName);
    }

    [Fact]
    public void DescribeSoundFile_RefusesAPartThatIsItselfComposed()
    {
        Dictionary<string, PronunciationOverride> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Outer"] = new(Composition: new CompositionOverride(2, [new CompositionPartOverride("Inner", 1)])),
            ["Inner"] = new(Composition: new CompositionOverride(1, [new CompositionPartOverride("1", 0)]))
        };

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
            () => CalloutPronunciation.DescribeSoundFile("Outer", overrides));

        Assert.Contains("Inner", refusal.Message);
    }

    [Fact]
    public void LoadOverrides_ReadsACompatibilityFileName()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                """{"Invoke Yu'lon, the Jade Serpent":{"FileName":"invoke_yulon_the_jade_serpent.ogg"}}""");

            IReadOnlyDictionary<string, PronunciationOverride> overrides =
                CalloutPronunciation.LoadOverrides(path);

            Assert.Equal(
                "invoke_yulon_the_jade_serpent.ogg",
                overrides["Invoke Yu'lon, the Jade Serpent"].FileName);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
