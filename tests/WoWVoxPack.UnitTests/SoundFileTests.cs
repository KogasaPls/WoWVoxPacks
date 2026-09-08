using System.Text.Json;

using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class SoundFileTests
{
    [Fact]
    public void Composition_SurvivesTheManifestRoundTrip()
    {
        SoundFile composed = Countdown(2.15);

        string json = JsonSerializer.Serialize(new List<SoundFile> { composed },
            SoundFileJsonContext.Default.ListSoundFile);
        SoundFile loaded = Assert.Single(
            JsonSerializer.Deserialize(json, SoundFileJsonContext.Default.ListSoundFile)!);

        Assert.Equal(composed.Composition, loaded.Composition);
        Assert.Null(loaded.Text);
        Assert.Contains("\"Composition\"", json);
    }

    [Fact]
    public void ContentComparer_TreatsAMovedOnsetAsADifferentRecording()
    {
        Assert.True(SoundFileContentEqualityComparer.Default.Equals(Countdown(2.15), Countdown(2.15)));
        Assert.False(SoundFileContentEqualityComparer.Default.Equals(Countdown(2.15), Countdown(2.5)));
        Assert.False(SoundFileContentEqualityComparer.Default.Equals(
            Countdown(2.15), new SoundFile("5seconds321.ogg", displayName: "5seconds321")));
    }

    [Fact]
    public void Constructor_NormalizesFileNameToLowercaseOgg()
    {
        SoundFile soundFile = new("Boss/Alert.WAV");

        Assert.Equal("boss/alert.ogg", soundFile.FileName);
        Assert.Equal("Boss/Alert", soundFile.DisplayName);
        Assert.Equal("Boss/Alert", soundFile.FormattedDisplayName);
    }

    [Fact]
    public void Constructor_PreservesExplicitDisplayNames()
    {
        SoundFile soundFile = new("alert", displayName: "Alert", formattedDisplayName: "Alert!");

        Assert.Equal("Alert", soundFile.DisplayName);
        Assert.Equal("Alert!", soundFile.FormattedDisplayName);
    }

    [Fact]
    public void Key_IsTheDisplayName_UnlessSetExplicitly()
    {
        SoundFile named = new("alert.ogg", displayName: "Alert");
        SoundFile keyed = new("338353.ogg", displayName: "Goresplatter") { ExplicitKey = "338353" };

        Assert.Equal("Alert", named.Key);
        Assert.Equal("338353", keyed.Key);
    }

    [Fact]
    public void ParseIpaHints_LiftsEveryHintOutOfTheText()
    {
        (string text, IReadOnlyList<Pronunciation> pronunciations) =
            SoundFile.ParseIpaHints("Taivan=ˈtaɪvɑːn incoming");

        Assert.Equal("Taivan incoming", text);
        Assert.Equal([new Pronunciation("Taivan", "ˈtaɪvɑːn")], pronunciations);
    }

    [Fact]
    public void ParseIpaHints_KeepsTheHintOutOfWhatIsSpoken()
    {
        (string text, IReadOnlyList<Pronunciation> pronunciations) = SoundFile.ParseIpaHints("Tempest Winds=wɪndz");

        Assert.Equal("Tempest Winds", text);
        Assert.Equal([new Pronunciation("Winds", "wɪndz")], pronunciations);
    }

    [Fact]
    public void ParseIpaHints_LeavesPlainTextAlone()
    {
        (string text, IReadOnlyList<Pronunciation> pronunciations) = SoundFile.ParseIpaHints("Gale Force");

        Assert.Equal("Gale Force", text);
        Assert.Empty(pronunciations);
    }

    [Fact]
    public void StripIpaHints_LeavesEveryWordSpelledOut()
    {
        Assert.Equal("Winds of Northrend", SoundFile.StripIpaHints("Winds=wɪndz of Northrend"));
        Assert.Equal("Gale Force", SoundFile.StripIpaHints("Gale Force"));
    }

    private static SoundFile Countdown(double firstOnset) =>
        new("5seconds321.ogg", displayName: "5seconds321",
            composition: new SoundComposition(5.7,
            [
                new SoundCompositionPart("three.ogg", firstOnset),
                new SoundCompositionPart("two.ogg", firstOnset + 1),
                new SoundCompositionPart("one.ogg", firstOnset + 2)
            ]));
}
