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
    public void GetSsml_UsesPhonemeMarkupForWordsWithIpa()
    {
        string ssml = SoundFile.GetSsml("Taivan=ˈtaɪvɑːn incoming");

        Assert.Contains("<phoneme alphabet=\"IPA\" ph=\"ˈtaɪvɑːn\">Taivan=ˈtaɪvɑːn </phoneme>", ssml);
        Assert.Contains("incoming", ssml);
    }
}
