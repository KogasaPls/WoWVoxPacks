using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class SoundFileTests
{
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
}
